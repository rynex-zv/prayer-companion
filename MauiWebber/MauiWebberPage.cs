using System.Text.Json;
using System.Diagnostics;
using Microsoft.Maui.Devices.Sensors;
#if ANDROID
using AndroidWebView = Android.Webkit.WebView;
using Java.Interop;
#endif
#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
#endif

namespace MauiWebber;

public class MauiWebberPage : ContentPage {
    private const string LocalContentHost = "app.prayadfree.local";
    private const string RpcScheme = "mauiwebber";
    private const string RpcHost = "rpc";
    private const string RpcSentinelHost = "mauiwebber.local";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WebView _webView = new();
    private readonly MauiWebberUpdater _updater;
    private readonly IMauiWebberRpcHandler _rpcHandler;
    private readonly IMauiWebberLogger _logger;
    private readonly string? _initialRoute;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly SemaphoreSlim _scriptGate = new(1, 1);
    private bool _loaded;
    private bool _firstTodaySnapshotLogged;
    private int _shakeCount;
    private DateTimeOffset _shakeSequenceStartedAt;
    private DateTimeOffset _lastShakeAt;
#if ANDROID
    private AndroidMauiWebberBridge? _androidBridge;
#endif
#if WINDOWS
    private CoreWebView2? _windowsCoreWebView2;
    private WebView2? _windowsLayoutView;
#endif

    public MauiWebberPage(
        MauiWebberUpdater updater,
        IMauiWebberRpcHandler rpcHandler,
        IMauiWebberLogger? logger = null,
        string? initialRoute = null) {
        _updater = updater;
        _rpcHandler = rpcHandler;
        _logger = logger ?? NullMauiWebberLogger.Instance;
        _initialRoute = initialRoute;
        Content = _webView;
        // React/ARIA remains the accessibility authority. On Windows the
        // visible MAUI WebView2 host is used directly so the user sees the
        // same DOM that receives native bridge calls.
        _webView.HandlerChanged += OnWebViewHandlerChanged;
        _webView.Navigating += OnNavigating;
        _webView.Navigated += OnNavigated;
        MauiWebberEventHub.Published += OnApplicationEvent;
        Unloaded += OnPageUnloaded;
    }

    private void OnPageUnloaded(object? sender, EventArgs e) {
        _logger.Log("Page.Unloaded", $"ms={_stopwatch.ElapsedMilliseconds}");
        MauiWebberEventHub.Published -= OnApplicationEvent;
#if WINDOWS
        if (_windowsCoreWebView2 != null) {
            _windowsCoreWebView2.WebMessageReceived -= OnWindowsWebMessageReceived;
            _windowsCoreWebView2.NavigationCompleted -= OnWindowsNavigationCompleted;
        }
        _windowsCoreWebView2 = null;
        _windowsLayoutView = null;
#endif
    }

    private void OnApplicationEvent(object? sender, object value) {
        _ = PublishApplicationEventAsync(value);
    }

    private async Task PublishApplicationEventAsync(object value) {
        try {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            _logger.Log("ApplicationEvent.Dispatch", $"ms={_stopwatch.ElapsedMilliseconds};payload={json}");
            var script = $"window.dispatchEvent(new CustomEvent('mauiwebber:app-event',{{detail:{json}}}));";
            await MainThread.InvokeOnMainThreadAsync(() => EvaluateJavaScriptAsync(script)).ConfigureAwait(false);
            _logger.Log("ApplicationEvent.Delivered", $"ms={_stopwatch.ElapsedMilliseconds}");
        } catch (Exception ex) {
            _logger.LogException(ex, "MauiWebber.PublishApplicationEvent");
        }
    }

    public Task<bool> TryHandleBackNavigationAsync() {
        return DispatchNavigationCommandAsync("back");
    }

    public Task<bool> TryHandleForwardNavigationAsync() {
        return DispatchNavigationCommandAsync("forward");
    }

    public async Task<bool> NavigateToRouteAsync(string route, TimeSpan? timeout = null) {
        if (string.IsNullOrWhiteSpace(route)) return false;
        var deadlineUtc = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(12));
        var routeJson = JsonSerializer.Serialize(route.StartsWith('/') ? route : $"/{route}", JsonOptions);
        while (DateTime.UtcNow < deadlineUtc) {
            if (_loaded) {
                try {
                    var result = await MainThread.InvokeOnMainThreadAsync(() => EvaluateJavaScriptAsync($$"""
                        (function(){
                          if (!window.prayerCompanion || typeof window.prayerCompanion.navigate !== 'function') return 'false';
                          window.prayerCompanion.navigate({{routeJson}});
                          return 'true';
                        })();
                        """)).ConfigureAwait(false);
                    if (IsJavaScriptTrue(result)) {
                        _logger.Log("RouteNavigation", $"route={route};handled=true");
                        return true;
                    }
                } catch (Exception ex) {
                    _logger.LogException(ex, "MauiWebber.RouteNavigation");
                }
            }
            await Task.Delay(100).ConfigureAwait(false);
        }
        _logger.Log("RouteNavigation", $"route={route};handled=false;reason=timeout");
        return false;
    }

    protected override bool OnBackButtonPressed() {
        _ = TryHandleBackNavigationAsync();
        return true;
    }

    private void OnWebViewHandlerChanged(object? sender, EventArgs e) {
#if ANDROID
        if (_webView.Handler?.PlatformView is AndroidWebView androidWebView) {
            AndroidWebView.SetWebContentsDebuggingEnabled(true);
            androidWebView.Settings.JavaScriptEnabled = true;
            androidWebView.Settings.AllowFileAccess = true;
            androidWebView.Settings.AllowContentAccess = true;
#pragma warning disable CA1416
            androidWebView.Settings.AllowFileAccessFromFileURLs = true;
            androidWebView.Settings.AllowUniversalAccessFromFileURLs = true;
#pragma warning restore CA1416
            _androidBridge ??= new AndroidMauiWebberBridge(this);
            androidWebView.AddJavascriptInterface(_androidBridge, "mauiWebberNative");
        }
#endif
#if WINDOWS
        if (_webView.Handler?.PlatformView is WebView2 windowsWebView) {
            _windowsLayoutView = windowsWebView;
            windowsWebView.Opacity = 1;
            windowsWebView.IsHitTestVisible = true;
            windowsWebView.IsTabStop = true;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(windowsWebView, "Pray Ad Free web content");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAccessibilityView(
                windowsWebView,
                Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Content);
        }
#endif
    }

#if WINDOWS
    private async Task EnsureWindowsWebViewAsync() {
        if (_windowsCoreWebView2 != null) {
            return;
        }

        var windowsWebView = _windowsLayoutView
            ?? throw new InvalidOperationException("Windows WebView2 handler did not initialize; refusing insecure file: navigation.");
        Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", "--force-renderer-accessibility");
        await windowsWebView.EnsureCoreWebView2Async();
        _windowsCoreWebView2 = windowsWebView.CoreWebView2
            ?? throw new InvalidOperationException("Windows CoreWebView2 did not initialize; refusing insecure file: navigation.");
        _windowsCoreWebView2.WebMessageReceived += OnWindowsWebMessageReceived;
        _windowsCoreWebView2.NavigationCompleted += OnWindowsNavigationCompleted;
        EnableWindowsWebMessages(_windowsCoreWebView2);
        windowsWebView.Focus(FocusState.Programmatic);
        _logger.Log("WebView2.VisibleHost.Attached", $"ms={_stopwatch.ElapsedMilliseconds}");
    }

    private void EnableWindowsWebMessages(CoreWebView2 coreWebView2) {
        coreWebView2.Settings.IsWebMessageEnabled = true;
        coreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
        _logger.Log("WebView2.MessageHandler.Attached", $"ms={_stopwatch.ElapsedMilliseconds}");
    }

    private async void OnWindowsWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args) {
        try {
            var message = args.TryGetWebMessageAsString();
            _logger.Log("WebView2.MessageReceived", $"ms={_stopwatch.ElapsedMilliseconds};length={message?.Length ?? 0}");
            if (!string.IsNullOrWhiteSpace(message)) {
                await HandleRpcJsonAsync(message).ConfigureAwait(false);
            }
        } catch (Exception ex) {
            _logger.LogException(ex, "MauiWebber.WebView2.MessageReceived");
        }
    }

    private async void OnWindowsNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args) {
        var url = sender.Source;
        _logger.Log("WebView.Navigated", $"ms={_stopwatch.ElapsedMilliseconds};success={args.IsSuccess};status={args.WebErrorStatus};url={url}");
        await HandleNavigationCompletedAsync(args.IsSuccess, url).ConfigureAwait(false);
    }

#endif

    protected override async void OnAppearing() {
        base.OnAppearing();
        StartShakeDetection();
        if (_loaded) {
            return;
        }

        _loaded = true;
        try {
            var startupFile = await _updater.ResolveStartupFileAsync().ConfigureAwait(false);
            await MainThread.InvokeOnMainThreadAsync(async () => {
#if WINDOWS
            for (var attempt = 0; attempt < 80 && _windowsLayoutView == null; attempt++) {
                if (_webView.Handler?.PlatformView is WebView2 windowsWebView) {
                    _windowsLayoutView = windowsWebView;
                } else {
                    await Task.Delay(25).ConfigureAwait(true);
                }
            }
            if (_windowsLayoutView == null) {
                throw new InvalidOperationException("Windows WebView2 handler did not initialize; refusing insecure file: navigation.");
            }
            await EnsureWindowsWebViewAsync();
            var sourceUrl = BuildSourceUrl(startupFile);
            _windowsCoreWebView2!.Navigate(sourceUrl);
            _logger.Log("WebView.SourceSet", $"ms={_stopwatch.ElapsedMilliseconds};url={sourceUrl}");
#else
            _webView.Source = new UrlWebViewSource {
                Url = BuildSourceUrl(startupFile)
            };
            _logger.Log("WebView.SourceSet", $"ms={_stopwatch.ElapsedMilliseconds};url={((UrlWebViewSource)_webView.Source).Url}");
#endif
            });
            _ = _updater.CheckForUpdatesAsync();
        } catch (Exception ex) {
            _logger.LogException(ex, "MauiWebber.Startup");
            _logger.Log("WebView.ReleaseBlocker", $"reason=startup-failed;error={ex.Message}");
        }
    }

    protected override void OnDisappearing() {
        StopShakeDetection();
        base.OnDisappearing();
    }

    private bool _shakeDetectionStarted;

    private void StartShakeDetection() {
        if (_shakeDetectionStarted)
            return;

        var accelerometer = Accelerometer.Default;

        if (!accelerometer.IsSupported) {
            _logger.LogInformation(
                "Accelerometer is not supported on this device/platform." );

            return;
        }

        if (accelerometer.IsMonitoring)
            return;

        try {
            accelerometer.ReadingChanged += OnAccelerometerReadingChanged;
            accelerometer.Start( SensorSpeed.Game );

            _shakeDetectionStarted = true;
        } catch (FeatureNotSupportedException ex) {
            accelerometer.ReadingChanged -= OnAccelerometerReadingChanged;

            _logger.LogException(
                ex ,
                "MauiWebber.Shake.NotSupported" );
        } catch (Exception ex) {
            accelerometer.ReadingChanged -= OnAccelerometerReadingChanged;

            _logger.LogException(
                ex ,
                "MauiWebber.Shake.Start" );
        }
    }
    private void StopShakeDetection() {
        var accelerometer = Accelerometer.Default;

        try {
            if (_shakeDetectionStarted && accelerometer.IsMonitoring)
                accelerometer.Stop();
        } catch (Exception ex) {
            _logger.LogException(
                ex ,
                "MauiWebber.Shake.Stop" );
        } finally {
            accelerometer.ReadingChanged -= OnAccelerometerReadingChanged;

            _shakeDetectionStarted = false;
            _shakeCount = 0;
            _lastShakeAt = default;
            _shakeSequenceStartedAt = default;
        }
    }
    private void OnAccelerometerReadingChanged(
    object? sender ,
    AccelerometerChangedEventArgs e ) {
        var acceleration = e.Reading.Acceleration;

        if (acceleration.Length() < 2.25f)
            return;

        var now = DateTimeOffset.UtcNow;

        if ((now - _lastShakeAt).TotalMilliseconds < 400)
            return;

        if (_shakeSequenceStartedAt == default ||
            (now - _shakeSequenceStartedAt).TotalSeconds > 12) {
            _shakeSequenceStartedAt = now;
            _shakeCount = 0;
        }

        _lastShakeAt = now;
        _shakeCount++;

        if (_shakeCount < 5)
            return;

        _shakeCount = 0;
        _shakeSequenceStartedAt = default;

        Dispatcher.Dispatch( () =>
        {
            _ = DispatchShakeUnlockAsync();
        } );
    }

    private async Task DispatchShakeUnlockAsync() {
        try {
            await MainThread.InvokeOnMainThreadAsync(() => EvaluateJavaScriptAsync(
                "window.dispatchEvent(new Event('prayercompanion:shake-unlock'));"
            )).ConfigureAwait(false);
            _logger.Log("Shake.Unlock", "count=5");
        } catch (Exception ex) {
            _logger.LogException(ex, "MauiWebber.Shake.Unlock");
        }
    }

    private async void OnNavigated(object? sender, WebNavigatedEventArgs e) {
        _logger.Log("WebView.Navigated", $"ms={_stopwatch.ElapsedMilliseconds};result={e.Result};url={e.Url}");
        await HandleNavigationCompletedAsync(e.Result == WebNavigationResult.Success, e.Url).ConfigureAwait(false);
    }

    private async Task HandleNavigationCompletedAsync(bool succeeded, string url) {
        if (succeeded) {
            try {
                _logger.Log("Bridge.Inject.Start", $"ms={_stopwatch.ElapsedMilliseconds}");
                await InjectBridgeAsync().ConfigureAwait(false);
                await NavigateToInitialRouteAsync().ConfigureAwait(false);
                _logger.Log("Bridge.Inject.End", $"ms={_stopwatch.ElapsedMilliseconds}");
                var diagnostics = await MainThread.InvokeOnMainThreadAsync(() =>
                    EvaluateJavaScriptAsync("""
                        (function(){
                          var app = document.getElementById('app');
                          return JSON.stringify({
                            title: document.title,
                            bodyTextLength: (document.body && document.body.innerText || '').length,
                            appHtmlLength: (app && app.innerHTML || '').length,
                            scripts: document.scripts.length
                          });
                        })();
                        """)).ConfigureAwait(false);
                _logger.Log("Bridge.PageDiagnostics", $"ms={_stopwatch.ElapsedMilliseconds};result={diagnostics}");
                if (await WaitForReactRootAsync().ConfigureAwait(false)) {
                    await CaptureWindowsVisualEvidenceAsync().ConfigureAwait(false);
                    _logger.Log("WebView.RuntimeHealthy", $"ms={_stopwatch.ElapsedMilliseconds};url={url}");
                    return;
                }

                _logger.Log("WebView.RuntimeUnhealthy", $"ms={_stopwatch.ElapsedMilliseconds};url={url}");
            } catch (Exception ex) {
                _logger.LogException(ex, "MauiWebber.Bridge.Inject");
            }

            _logger.Log("WebView.ReleaseBlocker", $"reason=runtime-unhealthy;url={url}");
            return;
        }

        _logger.Log("WebView.ReleaseBlocker", $"reason=navigation-failed;url={url}");
    }

    private async Task<bool> WaitForReactRootAsync() {
        for (var attempt = 1; attempt <= 12; attempt++) {
            const string script = """
                    (function(){
                      var app = document.getElementById('app');
                      return !!(app && app.childElementCount > 0 && app.innerHTML.length > 20);
                    })();
                    """;
            var result = await MainThread.InvokeOnMainThreadAsync(async () => {
#if WINDOWS
                if (_windowsCoreWebView2 != null) {
                    return await _windowsCoreWebView2.ExecuteScriptAsync(script);
                }
#endif
                return await _webView.EvaluateJavaScriptAsync(script);
            }).ConfigureAwait(false);
            if (string.Equals(result?.Trim().Trim('"'), "true", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            await Task.Delay(250).ConfigureAwait(false);
        }

        return false;
    }

    private async void OnNavigating(object? sender, WebNavigatingEventArgs e) {
        if (!e.Url.StartsWith($"{RpcScheme}://", StringComparison.OrdinalIgnoreCase)) {
            _logger.Log("WebView.Navigating", $"ms={_stopwatch.ElapsedMilliseconds};url={e.Url}");
        }

        if (!Uri.TryCreate(e.Url, UriKind.Absolute, out var uri) || !IsRpcUri(uri)) {
            return;
        }

        e.Cancel = true;
        await HandleRpcAsync(uri).ConfigureAwait(false);
    }

    private static bool IsRpcUri(Uri uri) {
        if (string.Equals(uri.Scheme, RpcScheme, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(uri.Host, RpcHost, StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(uri.Host, RpcSentinelHost, StringComparison.OrdinalIgnoreCase) &&
               uri.AbsolutePath.StartsWith("/rpc/", StringComparison.OrdinalIgnoreCase);
    }

    private async Task HandleRpcAsync(Uri uri) {
        try {
            var path = uri.AbsolutePath;
            var encoded = string.Equals(uri.Host, RpcSentinelHost, StringComparison.OrdinalIgnoreCase)
                ? path["/rpc/".Length..]
                : path.TrimStart('/');
            var json = Uri.UnescapeDataString(encoded);
            await HandleRpcJsonAsync(json).ConfigureAwait(false);
        } catch (Exception ex) {
            _logger.LogException(ex, "MauiWebber.Rpc.Uri");
        }
    }

    private async Task HandleRpcJsonAsync(string json) {
        MauiWebberRpcRequest? request = null;
        try {
            request = JsonSerializer.Deserialize<MauiWebberRpcRequest>(json, JsonOptions);
            if (request == null || string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Method)) {
                return;
            }

            await HandleRpcRequestAsync(request).ConfigureAwait(false);
        } catch (Exception ex) {
            _logger.LogException(ex, $"MauiWebber.Rpc.{request?.Method ?? "unknown"}");
            if (request?.Id != null) {
                await ResolveAsync(request.Id, new MauiWebberRpcResponse { Ok = false, Error = ex.Message }).ConfigureAwait(false);
            }
        }
    }

#if ANDROID
    private sealed class AndroidMauiWebberBridge : Java.Lang.Object {
        private readonly MauiWebberPage _page;

        public AndroidMauiWebberBridge(MauiWebberPage page) {
            _page = page;
        }

        [Android.Webkit.JavascriptInterface]
        [Export("postMessage")]
        public void PostMessage(string json) {
            _page._logger.Log("AndroidBridge.MessageReceived", $"length={json?.Length ?? 0}");
            _ = _page.HandleRpcJsonAsync(json ?? string.Empty);
        }
    }
#endif

    private async Task HandleRpcRequestAsync(MauiWebberRpcRequest request) {
        if (string.Equals(request.Method, "mauiWebber.trace", StringComparison.Ordinal)) {
            _logger.Log("JsTrace", $"ms={_stopwatch.ElapsedMilliseconds};payload={request.Payload}");
            return;
        }

        if (string.Equals(request.Method, "mauiWebber.clearSiteData", StringComparison.Ordinal)) {
            await ClearNativeSiteDataAsync().ConfigureAwait(false);
            await ResolveAsync(request.Id, new MauiWebberRpcResponse {
                Ok = true,
                Data = new { cleared = true }
            }).ConfigureAwait(false);
            return;
        }

        if (string.Equals(request.Method, "mauiWebber.pullRemote", StringComparison.Ordinal)) {
            var result = await _updater.PullRemoteAndActivateAsync(CancellationToken.None).ConfigureAwait(false);
            await ResolveAsync(request.Id, new MauiWebberRpcResponse {
                Ok = !string.Equals(result.Status, "error", StringComparison.Ordinal),
                Data = new {
                    source = "remote",
                    status = result.Status,
                    version = result.Version,
                    lastPulledVersion = result.Version,
                    url = string.IsNullOrWhiteSpace(result.StartupFile) ? null : ToSourceUrl(result.StartupFile)
                },
                Error = result.Error
            }).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(result.StartupFile)) {
                await MainThread.InvokeOnMainThreadAsync(async () => {
                    await Task.Delay(250).ConfigureAwait(true);
                    SetWebViewSource(result.StartupFile, "WebView.RemoteSourceSet");
                }).ConfigureAwait(false);
            }
            return;
        }

        if (string.Equals(request.Method, "mauiWebber.getRemoteUrl", StringComparison.Ordinal)) {
            await ResolveAsync(request.Id, new MauiWebberRpcResponse {
                Ok = true,
                Data = new {
                    url = _updater.RemoteBaseUrl.AbsoluteUri,
                    manifestUrl = _updater.ManifestUrl.AbsoluteUri
                }
            }).ConfigureAwait(false);
            return;
        }

        if (string.Equals(request.Method, "mauiWebber.setRemoteUrl", StringComparison.Ordinal)) {
            try {
                string? rawUrl = null;
                if (request.Payload.ValueKind == JsonValueKind.Object &&
                    request.Payload.TryGetProperty("url", out var urlElement) &&
                    urlElement.ValueKind == JsonValueKind.String) {
                    rawUrl = urlElement.GetString();
                }

                var normalized = _updater.SetRemoteBaseUrl(rawUrl);
                await ResolveAsync(request.Id, new MauiWebberRpcResponse {
                    Ok = true,
                    Data = new {
                        url = normalized.AbsoluteUri,
                        manifestUrl = _updater.ManifestUrl.AbsoluteUri
                    }
                }).ConfigureAwait(false);
            } catch (Exception ex) {
                await ResolveAsync(request.Id, new MauiWebberRpcResponse {
                    Ok = false,
                    Error = ex.Message
                }).ConfigureAwait(false);
            }
            return;
        }

        if (string.Equals(request.Method, "mauiWebber.useEmbedded", StringComparison.Ordinal)) {
            _updater.UseEmbeddedOnNextStartup();
            var startupFile = await _updater.ResolveStartupFileAsync().ConfigureAwait(false);
            await ResolveAsync(request.Id, new MauiWebberRpcResponse {
                Ok = true,
                Data = new {
                    source = "embedded",
                    url = ToSourceUrl(startupFile)
                }
            }).ConfigureAwait(false);
            await MainThread.InvokeOnMainThreadAsync(async () => {
                await Task.Delay(250).ConfigureAwait(true);
                SetWebViewSource(startupFile, "WebView.EmbeddedSourceSet");
            }).ConfigureAwait(false);
            return;
        }

        var rpcStarted = _stopwatch.ElapsedMilliseconds;
        var isFirstTodaySnapshot = false;
        if (string.Equals(request.Method, "today.getSnapshot", StringComparison.Ordinal) && !_firstTodaySnapshotLogged) {
            _firstTodaySnapshotLogged = true;
            isFirstTodaySnapshot = true;
            _logger.Log("Today.GetSnapshot.First.Start", $"ms={rpcStarted}");
        }

        var data = await _rpcHandler.HandleAsync(request.Method, request.Payload, CancellationToken.None).ConfigureAwait(false);
        var rpcElapsed = _stopwatch.ElapsedMilliseconds - rpcStarted;
        _logger.Log("Rpc.Handled", $"method={request.Method};ms={rpcElapsed}");
        if (rpcElapsed > 300 && !IsInteractiveOperation(request.Method)) {
            _logger.Log("Rpc.BudgetExceeded", $"method={request.Method};ms={rpcElapsed};budgetMs=300");
        }
        if (isFirstTodaySnapshot) {
            _logger.Log("Today.GetSnapshot.First.End", $"ms={_stopwatch.ElapsedMilliseconds};elapsed={rpcElapsed}");
        }
        await ResolveAsync(request.Id, new MauiWebberRpcResponse { Ok = true, Data = data }).ConfigureAwait(false);
    }

    private static bool IsInteractiveOperation(string method) =>
        method.StartsWith("permissions.", StringComparison.Ordinal) ||
        method.StartsWith("location.", StringComparison.Ordinal) ||
        method.StartsWith("adhan.sound.", StringComparison.Ordinal) ||
        method.StartsWith("external.", StringComparison.Ordinal) ||
        method is "mauiWebber.pullRemote" or "mauiWebber.clearSiteData";

    private void SetWebViewSource(string startupFile, string logName) {
#if WINDOWS
        if (_windowsCoreWebView2 == null) {
            throw new InvalidOperationException("Windows WebView2 is unavailable; refusing navigation fallback.");
        }
        var sourceUrl = ToSourceUrl(startupFile);
        _windowsCoreWebView2.Navigate(sourceUrl);
        _logger.Log(logName, $"ms={_stopwatch.ElapsedMilliseconds};url={sourceUrl}");
#else
        _webView.Source = new UrlWebViewSource {
            Url = ToSourceUrl(startupFile)
        };
        _logger.Log(logName, $"ms={_stopwatch.ElapsedMilliseconds};url={((UrlWebViewSource)_webView.Source).Url}");
#endif
    }

    private async Task ClearNativeSiteDataAsync() {
#if ANDROID
        await MainThread.InvokeOnMainThreadAsync(() => {
            if (_webView.Handler?.PlatformView is AndroidWebView androidWebView) {
                androidWebView.ClearCache(true);
                androidWebView.ClearHistory();
            }
        }).ConfigureAwait(false);
#elif WINDOWS
        await MainThread.InvokeOnMainThreadAsync(async () => {
            if (_windowsCoreWebView2 != null) {
                await _windowsCoreWebView2.Profile.ClearBrowsingDataAsync(
                    CoreWebView2BrowsingDataKinds.DiskCache);
            }
        }).ConfigureAwait(false);
#elif IOS || MACCATALYST
        // Cache Storage and service workers are cleared by the web client. Do not
        // invoke broad website-data deletion because it removes authoritative storage.
        await Task.CompletedTask;
#else
        await Task.CompletedTask;
#endif
        _logger.Log("WebView.SiteDataCleared", "reconstructable-cache-only");
    }

    private string ToSourceUrl(string startupFile) {
#if WINDOWS
        if (Uri.TryCreate(startupFile, UriKind.Absolute, out var absolute) && absolute.IsFile &&
            _windowsCoreWebView2 != null) {
            var directory = Path.GetDirectoryName(absolute.LocalPath) ?? throw new InvalidOperationException("Web bundle directory is missing.");
            _windowsCoreWebView2.SetVirtualHostNameToFolderMapping(
                LocalContentHost,
                directory,
                CoreWebView2HostResourceAccessKind.Allow);
            return $"https://{LocalContentHost}/{Uri.EscapeDataString(Path.GetFileName(absolute.LocalPath))}";
        }
#endif
        return Uri.TryCreate(startupFile, UriKind.Absolute, out var uri)
            ? uri.AbsoluteUri
            : new Uri(startupFile).AbsoluteUri;
    }

    private string BuildSourceUrl(string startupFile) {
        var source = ToSourceUrl(startupFile);
        var initialRoute = EffectiveInitialRoute();
        if (string.IsNullOrWhiteSpace(initialRoute)) {
            return source;
        }

        var builder = new UriBuilder(source) { Fragment = initialRoute.TrimStart('#') };
        return builder.Uri.AbsoluteUri;
    }

    private async Task NavigateToInitialRouteAsync() {
        var initialRoute = EffectiveInitialRoute();
        if (string.IsNullOrWhiteSpace(initialRoute)) {
            return;
        }

        var routeJson = JsonSerializer.Serialize(initialRoute, JsonOptions);
        await MainThread.InvokeOnMainThreadAsync(() => EvaluateJavaScriptAsync($$"""
            (function(){
              if (window.prayerCompanion && typeof window.prayerCompanion.navigate === 'function') {
                window.prayerCompanion.navigate({{routeJson}});
              }
            })();
            """)).ConfigureAwait(false);
    }

    private string? EffectiveInitialRoute() =>
        _initialRoute ?? Environment.GetEnvironmentVariable("PRAY_VISUAL_ROUTE");

    private async Task CaptureWindowsVisualEvidenceAsync() {
#if WINDOWS
        var path = Environment.GetEnvironmentVariable("PRAY_VISUAL_CAPTURE_PATH");
        if (string.IsNullOrWhiteSpace(path) || _windowsCoreWebView2 == null) return;
        // Let route transitions, fonts, and entry animations settle before the
        // diagnostic capture. This code only runs when the explicit visual QA
        // environment variable is present.
        await Task.Delay(1500).ConfigureAwait(false);
        await MainThread.InvokeOnMainThreadAsync(async () => {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            await _windowsCoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
            stream.Seek(0);
            using var reader = new Windows.Storage.Streams.DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync((uint)stream.Size);
            var bytes = new byte[stream.Size];
            reader.ReadBytes(bytes);
            await File.WriteAllBytesAsync(path, bytes);
        }).ConfigureAwait(false);
        _logger.Log("WebView.VisualEvidence.Captured", $"path={path};ms={_stopwatch.ElapsedMilliseconds}");
#else
        await Task.CompletedTask;
#endif
    }

    private async Task<string?> EvaluateJavaScriptAsync(string script) {
#if WINDOWS
        if (_windowsCoreWebView2 == null) {
            throw new InvalidOperationException("Windows WebView2 is unavailable; refusing script fallback.");
        }

        return await _windowsCoreWebView2.ExecuteScriptAsync(script);
#else
        return await _webView.EvaluateJavaScriptAsync(script);
#endif
    }

    private Task InjectBridgeAsync() {
        var appendJsLog = _updater.Options.AppendJsLog ? "true" : "false";
        var script = $$"""
            (function(){
              const appendJSlog = {{appendJsLog}};

              function installJsLogListener() {
                if (!appendJSlog || window.__mauiWebberJsLogAttached) return;
                window.__mauiWebberJsLogAttached = true;

                function normalizeArg(arg) {
                  if (arg instanceof Error) {
                    return { name: arg.name, message: arg.message, stack: arg.stack };
                  }
                  if (typeof arg === 'string' || typeof arg === 'number' || typeof arg === 'boolean' || arg == null) {
                    return arg;
                  }
                  try {
                    return JSON.parse(JSON.stringify(arg));
                  } catch (_) {
                    try { return String(arg); } catch (_) { return '[unserializable]'; }
                  }
                }

                function append(level, args) {
                  try {
                    if (!window.mauiWebber || typeof window.mauiWebber.call !== 'function') return;
                    window.mauiWebber.call('mauiWebber.trace', {
                      name: 'console.' + level,
                      level: level,
                      args: Array.prototype.slice.call(args || []).map(normalizeArg),
                      location: String(window.location && window.location.href || ''),
                      at: performance.now()
                    });
                  } catch (_) {
                  }
                }

                ['debug', 'log', 'info', 'warn', 'error'].forEach(function(level) {
                  var original = console[level];
                  console[level] = function() {
                    if (typeof original === 'function') {
                      original.apply(console, arguments);
                    }
                    append(level, arguments);
                  };
                });

                window.addEventListener('error', function(event) {
                  append('error', [{
                    message: event.message,
                    source: event.filename,
                    line: event.lineno,
                    column: event.colno,
                    error: normalizeArg(event.error)
                  }]);
                });

                window.addEventListener('unhandledrejection', function(event) {
                  append('error', [{
                    message: 'Unhandled promise rejection',
                    reason: normalizeArg(event.reason)
                  }]);
                });
              }

              function dispatchNavigation(direction) {
                var event = new CustomEvent('mauiwebber:navigation', {
                  cancelable: true,
                  detail: { direction: direction, handled: false }
                });
                window.dispatchEvent(event);
                return event.defaultPrevented || event.detail.handled === true;
              }

              function browserCanGo(direction) {
                if (direction === 'back') return window.history.length > 1;
                return false;
              }

              function browserGo(direction) {
                if (direction === 'back' && browserCanGo(direction)) {
                  window.history.back();
                  return true;
                }
                if (direction === 'forward') {
                  window.history.forward();
                  return true;
                }
                return false;
              }

              function runNavigation(direction) {
                var nav = window.mauiWebber && window.mauiWebber.navigation;
                var handler = nav && nav[direction];
                if (typeof handler === 'function' && handler() === true) return true;
                if (dispatchNavigation(direction)) return true;
                return browserGo(direction);
              }

              if (window.mauiWebber) {
                if (window.chrome && window.chrome.webview && typeof window.chrome.webview.addEventListener === 'function' && !window.mauiWebber.__nativeResponseListener) {
                  window.mauiWebber.__nativeResponseListener = function(message) {
                    var data = message && message.data;
                    if (typeof data === 'string') {
                      try { data = JSON.parse(data); } catch (_) { return; }
                    }

                    if (!data || data.__mauiWebberResponse !== true || !window.mauiWebber.__resolve) return;
                    window.__lastNativeResponse = data;
                    window.mauiWebber.__resolve(data.id, data.response);
                  };
                  window.chrome.webview.addEventListener('message', window.mauiWebber.__nativeResponseListener);
                }
                window.mauiWebber.__navigate = runNavigation;
                installJsLogListener();
                return;
              }
              const callbacks = {};
              function receiveResponse(message) {
                var data = message && message.data;
                if (typeof data === 'string') {
                  try { data = JSON.parse(data); } catch (_) { return; }
                }

                if (!data || data.__mauiWebberResponse !== true) return;
                window.__lastNativeResponse = data;
                window.mauiWebber.__resolve(data.id, data.response);
              }

              if (window.chrome && window.chrome.webview && typeof window.chrome.webview.addEventListener === 'function') {
                window.chrome.webview.addEventListener('message', receiveResponse);
              }

              function sendMessage(request) {
                if (window.mauiWebberNative && typeof window.mauiWebberNative.postMessage === 'function') {
                  window.mauiWebberNative.postMessage(decodeURIComponent(request));
                  return;
                }

                if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {
                  window.chrome.webview.postMessage(decodeURIComponent(request));
                  return;
                }

                var frame = document.createElement('iframe');
                frame.style.display = 'none';
                frame.src = 'https://mauiwebber.local/rpc/' + request;
                document.documentElement.appendChild(frame);
                setTimeout(function() {
                  if (frame.parentNode) frame.parentNode.removeChild(frame);
                }, 1000);
              }

              function traceBridgeResolve(id, found, pendingBefore, pendingAfter) {
                if (typeof id === 'string' && id.indexOf('trace-') === 0) return;
                var payload = {
                  id: 'trace-' + Date.now().toString(36) + Math.random().toString(36).slice(2),
                  method: 'mauiWebber.trace',
                  payload: {
                    name: 'bridge.resolve',
                    id: id,
                    found: found,
                    pendingBefore: pendingBefore,
                    pendingAfter: pendingAfter,
                    at: performance.now()
                  }
                };
                sendMessage(encodeURIComponent(JSON.stringify(payload)));
              }

              window.mauiWebber = {
                call: function(method, payload) {
                  const id = Date.now().toString(36) + Math.random().toString(36).slice(2);
                  const request = encodeURIComponent(JSON.stringify({ id, method, payload: payload || {} }));
                  if (method === 'mauiWebber.trace') {
                    sendMessage(request);
                    return Promise.resolve({ ok: true, data: { accepted: true } });
                  }
                  return new Promise(function(resolve) {
                    callbacks[id] = resolve;
                    sendMessage(request);
                  });
                },
                __resolve: function(id, response) {
                  const pendingBefore = Object.keys(callbacks).length;
                  const callback = callbacks[id];
                  if (!callback) {
                    traceBridgeResolve(id, false, pendingBefore, Object.keys(callbacks).length);
                    return;
                  }
                  delete callbacks[id];
                  traceBridgeResolve(id, true, pendingBefore, Object.keys(callbacks).length);
                  callback(response);
                },
                __drain: function() {
                  return '[]';
                },
                __debugPending: function() {
                  return Object.keys(callbacks);
                },
                __navigate: runNavigation,
                navigation: null
              };
              installJsLogListener();
              window.dispatchEvent(new CustomEvent('mauiwebber:ready'));
              setTimeout(function(){
                window.mauiWebber.call('mauiWebber.trace', { name: 'bridgeReady', performanceNow: performance.now() });
              }, 0);
            })();
            """;
        return EvaluateJavaScriptAsync(script);
    }

    private async Task<bool> DispatchNavigationCommandAsync(string direction) {
        if (!_loaded) {
            return false;
        }

        try {
            var command = JsonSerializer.Serialize(direction);
            var script = $$"""
                (function(){
                  var direction = {{command}};
                  if (!window.mauiWebber || typeof window.mauiWebber.__navigate !== 'function') {
                    return 'false';
                  }

                  return window.mauiWebber.__navigate(direction) === true ? 'true' : 'false';
                })();
                """;

            var result = await MainThread.InvokeOnMainThreadAsync(() => EvaluateJavaScriptAsync(script)).ConfigureAwait(false);
            var handled = IsJavaScriptTrue(result);
            _logger.Log("NavigationCommand", $"direction={direction};handled={handled};result={result}");
            return handled;
        } catch (Exception ex) {
            _logger.LogException(ex, $"MauiWebber.Navigation.{direction}");
            return false;
        }
    }

    private static bool IsJavaScriptTrue(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        var normalized = value.Trim().Trim('"');
        return string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ResolveAsync(string id, MauiWebberRpcResponse response) {
        var started = _stopwatch.ElapsedMilliseconds;
        _logger.Log("Resolve.Start", $"id={id};ok={response.Ok};ms={started}");
        await _scriptGate.WaitAsync().ConfigureAwait(false);
        try {
#if WINDOWS
            var envelope = new {
                __mauiWebberResponse = true,
                id,
                response
            };
            var json = JsonSerializer.Serialize(envelope, JsonOptions);
            var posted = await MainThread.InvokeOnMainThreadAsync(() => {
                if (_windowsCoreWebView2 != null) {
                    _windowsCoreWebView2.PostWebMessageAsJson(json);
                    return true;
                }

                return false;
            }).ConfigureAwait(false);
            if (!posted) {
                var script = $"window.mauiWebber&&window.mauiWebber.__resolve({JsonSerializer.Serialize(id)}, {JsonSerializer.Serialize(response, JsonOptions)});";
                await MainThread.InvokeOnMainThreadAsync(() => {
                    return EvaluateJavaScriptAsync(script);
                }).ConfigureAwait(false);
            }
#else
            var script = $"window.mauiWebber&&window.mauiWebber.__resolve({JsonSerializer.Serialize(id)}, {JsonSerializer.Serialize(response, JsonOptions)});";
            await MainThread.InvokeOnMainThreadAsync(() => EvaluateJavaScriptAsync(script)).ConfigureAwait(false);
#endif
            _logger.Log("Resolve.End", $"id={id};elapsed={_stopwatch.ElapsedMilliseconds - started}");
        } catch (Exception ex) {
            _logger.LogException(ex, "MauiWebber.Resolve");
            throw;
        } finally {
            _scriptGate.Release();
        }
    }
}
