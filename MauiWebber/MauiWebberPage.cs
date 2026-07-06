using System.Text.Json;
using System.Diagnostics;
#if ANDROID
using AndroidWebView = Android.Webkit.WebView;
#endif
#if WINDOWS
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
#endif

namespace MauiWebber;

public class MauiWebberPage : ContentPage {
    private const string RpcScheme = "mauiwebber";
    private const string RpcHost = "rpc";
    private const string RpcSentinelHost = "mauiwebber.local";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WebView _webView = new();
    private readonly MauiWebberUpdater _updater;
    private readonly IMauiWebberRpcHandler _rpcHandler;
    private readonly IMauiWebberLogger _logger;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly SemaphoreSlim _scriptGate = new(1, 1);
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

    public Task<bool> TryHandleBackNavigationAsync() {
        return DispatchNavigationCommandAsync("back");
    }

    public Task<bool> TryHandleForwardNavigationAsync() {
        return DispatchNavigationCommandAsync("forward");
    }

    protected override bool OnBackButtonPressed() {
        _ = TryHandleBackNavigationAsync();
        return true;
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
#if WINDOWS
        if (_webView.Handler?.PlatformView is WebView2 windowsWebView) {
            windowsWebView.CoreWebView2Initialized -= OnWindowsWebView2Initialized;
            windowsWebView.CoreWebView2Initialized += OnWindowsWebView2Initialized;
            windowsWebView.WebMessageReceived -= OnWindowsWebMessageReceived;
            windowsWebView.WebMessageReceived += OnWindowsWebMessageReceived;
            if (windowsWebView.CoreWebView2 != null) {
                EnableWindowsWebMessages(windowsWebView.CoreWebView2);
            }
        }
#endif
    }

#if WINDOWS
    private void OnWindowsWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args) {
        if (args.Exception != null || sender.CoreWebView2 == null) {
            _logger.Log("WebView2.Init.Failed", args.Exception?.Message ?? "unknown");
            return;
        }

        EnableWindowsWebMessages(sender.CoreWebView2);
    }

    private void EnableWindowsWebMessages(CoreWebView2 coreWebView2) {
        coreWebView2.Settings.IsWebMessageEnabled = true;
        _logger.Log("WebView2.MessageHandler.Attached", $"ms={_stopwatch.ElapsedMilliseconds}");
    }

    private async void OnWindowsWebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args) {
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
#endif

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
            try {
                _logger.Log("Bridge.Inject.Start", $"ms={_stopwatch.ElapsedMilliseconds}");
                await InjectBridgeAsync().ConfigureAwait(false);
                _logger.Log("Bridge.Inject.End", $"ms={_stopwatch.ElapsedMilliseconds}");
                var diagnostics = await MainThread.InvokeOnMainThreadAsync(() =>
                    _webView.EvaluateJavaScriptAsync("""
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
            } catch (Exception ex) {
                _logger.LogException(ex, "MauiWebber.Bridge.Inject");
            }
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

    private async Task HandleRpcRequestAsync(MauiWebberRpcRequest request) {
        if (string.Equals(request.Method, "mauiWebber.trace", StringComparison.Ordinal)) {
            _logger.Log("JsTrace", $"ms={_stopwatch.ElapsedMilliseconds};payload={request.Payload}");
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
    }

    private Task InjectBridgeAsync() {
        const string script = """
            (function(){
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
              window.dispatchEvent(new CustomEvent('mauiwebber:ready'));
              setTimeout(function(){
                window.mauiWebber.call('mauiWebber.trace', { name: 'bridgeReady', performanceNow: performance.now() });
              }, 0);
            })();
            """;
        return _webView.EvaluateJavaScriptAsync(script);
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

            var result = await MainThread.InvokeOnMainThreadAsync(() => _webView.EvaluateJavaScriptAsync(script)).ConfigureAwait(false);
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
            await Task.Delay(50).ConfigureAwait(false);
            var envelope = new {
                __mauiWebberResponse = true,
                id,
                response
            };
            var json = JsonSerializer.Serialize(envelope, JsonOptions);
            var posted = await MainThread.InvokeOnMainThreadAsync(() => {
                if (_webView.Handler?.PlatformView is WebView2 windowsWebView && windowsWebView.CoreWebView2 != null) {
                    windowsWebView.CoreWebView2.PostWebMessageAsJson(json);
                    return true;
                }

                return false;
            }).ConfigureAwait(false);
            if (!posted) {
                var script = $"window.mauiWebber&&window.mauiWebber.__resolve({JsonSerializer.Serialize(id)}, {JsonSerializer.Serialize(response, JsonOptions)});";
                await MainThread.InvokeOnMainThreadAsync(() => {
                    return _webView.EvaluateJavaScriptAsync(script);
                }).ConfigureAwait(false);
            }
#else
            var script = $"window.mauiWebber&&window.mauiWebber.__resolve({JsonSerializer.Serialize(id)}, {JsonSerializer.Serialize(response, JsonOptions)});";
            await MainThread.InvokeOnMainThreadAsync(() => _webView.EvaluateJavaScriptAsync(script)).ConfigureAwait(false);
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
