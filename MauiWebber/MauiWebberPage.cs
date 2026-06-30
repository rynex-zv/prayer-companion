using System.Text.Json;
using System.Diagnostics;
#if ANDROID
using AndroidWebView = Android.Webkit.WebView;
#endif

namespace MauiWebber;

public class MauiWebberPage : ContentPage {
    private const string RpcScheme = "mauiwebber";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WebView _webView = new();
    private readonly MauiWebberUpdater _updater;
    private readonly IMauiWebberRpcHandler _rpcHandler;
    private readonly IMauiWebberLogger _logger;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private bool _loaded;
    private bool _firstTodaySnapshotLogged;
    private bool _navigationFallbackTried;

    public MauiWebberPage(MauiWebberUpdater updater, IMauiWebberRpcHandler rpcHandler, IMauiWebberLogger? logger = null) {
        _updater = updater;
        _rpcHandler = rpcHandler;
        _logger = logger ?? NullMauiWebberLogger.Instance;
        Content = _webView;
        _webView.HandlerChanged += OnWebViewHandlerChanged;
        _webView.Navigating += OnNavigating;
        _webView.Navigated += OnNavigated;
    }

    private void OnWebViewHandlerChanged(object? sender, EventArgs e) {
#if ANDROID
        if (_webView.Handler?.PlatformView is AndroidWebView androidWebView) {
            androidWebView.Settings.JavaScriptEnabled = true;
            androidWebView.Settings.AllowFileAccess = true;
            androidWebView.Settings.AllowContentAccess = true;
#pragma warning disable CA1416
            androidWebView.Settings.AllowFileAccessFromFileURLs = true;
            androidWebView.Settings.AllowUniversalAccessFromFileURLs = true;
#pragma warning restore CA1416
        }
#endif
    }

    protected override async void OnAppearing() {
        base.OnAppearing();
        if (_loaded) {
            return;
        }

        _loaded = true;
        var startupFile = await _updater.ResolveStartupFileAsync().ConfigureAwait(false);
        await MainThread.InvokeOnMainThreadAsync(() => {
            _webView.Source = new UrlWebViewSource {
                Url = Uri.TryCreate(startupFile, UriKind.Absolute, out var uri)
                    ? uri.AbsoluteUri
                    : new Uri(startupFile).AbsoluteUri
            };
            _logger.Log("WebView.SourceSet", $"ms={_stopwatch.ElapsedMilliseconds};url={((UrlWebViewSource)_webView.Source).Url}");
        });
        _ = _updater.CheckForUpdatesAsync();
    }

    private async void OnNavigated(object? sender, WebNavigatedEventArgs e) {
        _logger.Log("WebView.Navigated", $"ms={_stopwatch.ElapsedMilliseconds};result={e.Result};url={e.Url}");
        if (e.Result == WebNavigationResult.Success) {
            await InjectBridgeAsync().ConfigureAwait(false);
            return;
        }

        if (_navigationFallbackTried) {
            return;
        }

        _navigationFallbackTried = true;
        var fallback = await _updater.ResolveAfterNavigationFailureAsync(e.Url).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(fallback)) {
            await MainThread.InvokeOnMainThreadAsync(() => {
                _webView.Source = new UrlWebViewSource {
                    Url = Uri.TryCreate(fallback, UriKind.Absolute, out var uri)
                        ? uri.AbsoluteUri
                        : new Uri(fallback).AbsoluteUri
                };
                _logger.Log("WebView.FallbackSourceSet", $"ms={_stopwatch.ElapsedMilliseconds};url={((UrlWebViewSource)_webView.Source).Url}");
            });
        }
    }

    private async void OnNavigating(object? sender, WebNavigatingEventArgs e) {
        if (!e.Url.StartsWith($"{RpcScheme}://", StringComparison.OrdinalIgnoreCase)) {
            _logger.Log("WebView.Navigating", $"ms={_stopwatch.ElapsedMilliseconds};url={e.Url}");
        }

        if (!Uri.TryCreate(e.Url, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, RpcScheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, "rpc", StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        e.Cancel = true;
        await HandleRpcAsync(uri).ConfigureAwait(false);
    }

    private async Task HandleRpcAsync(Uri uri) {
        MauiWebberRpcRequest? request = null;
        try {
            var json = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
            request = JsonSerializer.Deserialize<MauiWebberRpcRequest>(json, JsonOptions);
            if (request == null || string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Method)) {
                return;
            }

            if (string.Equals(request.Method, "mauiWebber.trace", StringComparison.Ordinal)) {
                _logger.Log("JsTrace", $"ms={_stopwatch.ElapsedMilliseconds};payload={request.Payload}");
                await ResolveAsync(request.Id, new MauiWebberRpcResponse { Ok = true, Data = new { accepted = true } }).ConfigureAwait(false);
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
            if (isFirstTodaySnapshot) {
                _logger.Log("Today.GetSnapshot.First.End", $"ms={_stopwatch.ElapsedMilliseconds};elapsed={rpcElapsed}");
            }
            await ResolveAsync(request.Id, new MauiWebberRpcResponse { Ok = true, Data = data }).ConfigureAwait(false);
        } catch (Exception ex) {
            _logger.LogException(ex, $"MauiWebber.Rpc.{request?.Method ?? "unknown"}");
            if (request?.Id != null) {
                await ResolveAsync(request.Id, new MauiWebberRpcResponse { Ok = false, Error = ex.Message }).ConfigureAwait(false);
            }
        }
    }

    private Task InjectBridgeAsync() {
        const string script = """
            (function(){
              if (window.mauiWebber) return;
              const callbacks = {};
              window.mauiWebber = {
                call: function(method, payload) {
                  const id = Date.now().toString(36) + Math.random().toString(36).slice(2);
                  const request = encodeURIComponent(JSON.stringify({ id, method, payload: payload || {} }));
                  return new Promise(function(resolve) {
                    callbacks[id] = resolve;
                    window.location.href = 'mauiwebber://rpc/' + request;
                  });
                },
                __resolve: function(id, response) {
                  const callback = callbacks[id];
                  if (!callback) return;
                  delete callbacks[id];
                  callback(response);
                }
              };
              window.dispatchEvent(new CustomEvent('mauiwebber:ready'));
              setTimeout(function(){
                window.mauiWebber.call('mauiWebber.trace', { name: 'bridgeReady', performanceNow: performance.now() });
              }, 0);
            })();
            """;
        return _webView.EvaluateJavaScriptAsync(script);
    }

    private Task ResolveAsync(string id, MauiWebberRpcResponse response) {
        var script = $"window.mauiWebber&&window.mauiWebber.__resolve({JsonSerializer.Serialize(id)}, {JsonSerializer.Serialize(response, JsonOptions)});";
        return MainThread.InvokeOnMainThreadAsync(() => _webView.EvaluateJavaScriptAsync(script));
    }
}
