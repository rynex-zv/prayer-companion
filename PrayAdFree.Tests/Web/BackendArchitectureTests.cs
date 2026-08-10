namespace PrayAdFree.Tests.Web;

public sealed class BackendArchitectureTests {
    [Fact]
    public void Rpc_handlers_do_not_reference_viewmodels() {
        var root = FindRepoRoot();
        foreach (var name in new[] { "WebAppRpcHandler.cs", "TodayWebRpcHandler.cs" }) {
            var source = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Services", name));
            Assert.DoesNotContain("Pray_Ad_Free.ViewModels", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ViewModel ", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Web_rpc_handler_is_transport_only() {
        var root = FindRepoRoot();
        var transport = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Services", "WebAppTransportRpcHandler.cs"));
        var application = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Services", "WebAppRpcHandler.cs"));
        Assert.Contains("NativeAppBackend", transport, StringComparison.Ordinal);
        Assert.DoesNotContain("SettingsRepository", transport, StringComparison.Ordinal);
        Assert.DoesNotContain("PrayerDataService", transport, StringComparison.Ordinal);
        Assert.DoesNotContain("switch", transport, StringComparison.Ordinal);
        Assert.Contains("public sealed class NativeAppBackend", application, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class WebAppRpcHandler", application, StringComparison.Ordinal);
        Assert.DoesNotContain("ImmediateApplicationTransactionFactory", application, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_projection_ports_resolve_to_application_services_not_viewmodels() {
        var root = FindRepoRoot();
        var registrations = File.ReadAllText(Path.Combine(root, "PrayAdFree", "MauiProgram.cs"));
        foreach (var mapping in new[] {
            "ITodayProjectionSource>(sp => sp.GetRequiredService<TodayApplicationService>())",
            "ICalendarProjectionSource>(sp => sp.GetRequiredService<CalendarApplicationService>())",
            "IQiblaProjectionSource>(sp => sp.GetRequiredService<QiblaApplicationService>())",
            "ITasbihProjectionSource>(sp => sp.GetRequiredService<TasbihApplicationService>())"
        }) Assert.Contains(mapping, registrations, StringComparison.Ordinal);

        Assert.DoesNotContain("ITodayProjectionSource>(sp => sp.GetRequiredService<HomeViewModel>())", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("ICalendarProjectionSource>(sp => sp.GetRequiredService<CalendarViewModel>())", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("IQiblaProjectionSource>(sp => sp.GetRequiredService<QiblaViewModel>())", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("ITasbihProjectionSource>(sp => sp.GetRequiredService<TasbihViewModel>())", registrations, StringComparison.Ordinal);
    }

    [Fact]
    public void Application_projection_services_have_no_ui_thread_or_device_dependencies() {
        var root = FindRepoRoot();
        foreach (var name in new[] { "TodayApplicationService.cs", "CalendarApplicationService.cs", "QiblaApplicationService.cs", "TasbihApplicationService.cs" }) {
            var source = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Services", name));
            Assert.Contains("ApplicationService", source, StringComparison.Ordinal);
            Assert.DoesNotContain("MainThread", source, StringComparison.Ordinal);
            Assert.DoesNotContain("new Command", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Vibration.Default", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Microsoft.Maui.ApplicationModel", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Microsoft.Maui.Devices", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Xaml_projection_viewmodels_only_adapt_commands_and_device_feedback() {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "PrayAdFree", "ViewModels", "ProjectionViewModels.cs"));
        Assert.Contains("HomeViewModel : TodayApplicationService", source, StringComparison.Ordinal);
        Assert.Contains("CalendarViewModel : CalendarApplicationService", source, StringComparison.Ordinal);
        Assert.Contains("QiblaViewModel : QiblaApplicationService", source, StringComparison.Ordinal);
        Assert.Contains("TasbihViewModel : TasbihApplicationService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveSettings", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetMonthAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateLocationAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ScheduleNotificationsAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Browser_store_is_memory_only_and_legacy_keys_are_migration_inputs_only() {
        var root = FindRepoRoot();
        var appStore = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "state", "appStore.ts"));
        var backend = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "native", "browserAppBackend.ts"));
        var wasm = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "native", "wasmCoreClient.ts"));
        Assert.DoesNotContain("localStorage", appStore, StringComparison.Ordinal);
        Assert.DoesNotContain("localStorage", wasm, StringComparison.Ordinal);
        Assert.Contains("removeItem(\"pray.web.core.state\")", backend, StringComparison.Ordinal);
        Assert.Contains("removeItem(\"prayer-companion:app-state:v1\")", backend, StringComparison.Ordinal);
        Assert.Contains("SCHEMA_VERSION", backend, StringComparison.Ordinal);
        Assert.DoesNotContain("app.importState", backend, StringComparison.Ordinal);
        Assert.DoesNotContain("app.exportState", backend, StringComparison.Ordinal);
        Assert.Contains("executeWasmCore(state", backend, StringComparison.Ordinal);
        Assert.Contains("operationQueue", backend, StringComparison.Ordinal);
        Assert.Contains("CallWithState", wasm, StringComparison.Ordinal);
    }

    [Fact]
    public void Browser_wasm_bridge_is_deterministic_and_has_no_process_global_dispatcher() {
        var root = FindRepoRoot();
        var bridge = File.ReadAllText(Path.Combine(root, "PrayAdFree.WebBridge", "WebRpcBridge.cs"));
        var engine = File.ReadAllText(Path.Combine(root, "PrayAdFree.Core", "Services", "WebCoreExecutionEngine.cs"));
        Assert.DoesNotContain("static readonly WebCoreRpcDispatcher", bridge, StringComparison.Ordinal);
        Assert.Contains("CallWithState", bridge, StringComparison.Ordinal);
        Assert.Contains("new WebCoreRpcDispatcher(persisted.State, persisted.Revision)", engine, StringComparison.Ordinal);
        Assert.Contains("WebExecutionResult", engine, StringComparison.Ordinal);
    }

    [Fact]
    public void Cache_eviction_preserves_authoritative_browser_repository_and_ui_state_is_memory_only() {
        var root = FindRepoRoot();
        var reset = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "lib", "siteDataReset.ts"));
        var calendar = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "routes", "calendar.tsx"));
        var nativeHost = File.ReadAllText(Path.Combine(root, "MauiWebber", "MauiWebberPage.cs"));
        Assert.DoesNotContain("indexedDB", reset, StringComparison.Ordinal);
        Assert.DoesNotContain("localStorage", reset, StringComparison.Ordinal);
        Assert.DoesNotContain("clearIndexedDb", reset, StringComparison.Ordinal);
        Assert.DoesNotContain("localStorage", calendar, StringComparison.Ordinal);
        Assert.Contains("clearCacheStorage", reset, StringComparison.Ordinal);
        Assert.DoesNotContain("DeleteAllData", nativeHost, StringComparison.Ordinal);
        Assert.DoesNotContain("AllWebsiteDataTypes", nativeHost, StringComparison.Ordinal);
        Assert.DoesNotContain("RemoveAllCookies", nativeHost, StringComparison.Ordinal);
        Assert.Contains("CoreWebView2BrowsingDataKinds.DiskCache", nativeHost, StringComparison.Ordinal);
    }

    [Fact]
    public void Windows_web_content_uses_a_stable_https_origin_and_accessible_host() {
        var root = FindRepoRoot();
        var host = File.ReadAllText(Path.Combine(root, "MauiWebber", "MauiWebberPage.cs"));
        Assert.Contains("app.prayadfree.local", host, StringComparison.Ordinal);
        Assert.Contains("SetVirtualHostNameToFolderMapping", host, StringComparison.Ordinal);
        Assert.DoesNotContain("TryUseFallbackAsync", host, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveAfterNavigationFailureAsync", host, StringComparison.Ordinal);
        Assert.Contains("WebView.ReleaseBlocker", host, StringComparison.Ordinal);
        Assert.Contains("CreateCoreWebView2ControllerAsync", host, StringComparison.Ordinal);
        Assert.Contains("CoreWebView2ControllerWindowReference.CreateFromWindowHandle", host, StringComparison.Ordinal);
        Assert.Contains("NotifyParentWindowPositionChanged", host, StringComparison.Ordinal);
        var uiaTest = File.ReadAllText(Path.Combine(root, "tools", "Test-WindowsAccessibility.ps1"));
        Assert.Contains("ControlType]::Document", uiaTest, StringComparison.Ordinal);
        Assert.Contains("NamedInteractiveControls", uiaTest, StringComparison.Ordinal);
    }

    [Fact]
    public void Phone_bundle_and_native_injection_share_one_console_and_response_listener() {
        var root = FindRepoRoot();
        var build = File.ReadAllText(Path.Combine(root, "Pray.web", "scripts", "build.mjs"));
        var host = File.ReadAllText(Path.Combine(root, "MauiWebber", "MauiWebberPage.cs"));

        Assert.Contains("window.__mauiWebberJsLogAttached = true", build, StringComparison.Ordinal);
        Assert.Contains("__nativeResponseListener: receiveResponse", build, StringComparison.Ordinal);
        Assert.Contains("!window.mauiWebber.__nativeResponseListener", host, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_and_browser_backends_share_strict_input_rejection() {
        var root = FindRepoRoot();
        var native = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Services", "WebAppRpcHandler.cs"));
        var browser = File.ReadAllText(Path.Combine(root, "PrayAdFree.Core", "Services", "WebCoreRpcDispatcher.cs"));

        foreach (var source in new[] { native, browser }) {
            Assert.Contains("AppInputContract.RequiredChoice", source, StringComparison.Ordinal);
            Assert.Contains("Unknown settings section", source, StringComparison.Ordinal);
        }
        Assert.Contains("AppInputContract.RequiredIndex", native, StringComparison.Ordinal);
        Assert.Contains("Unknown Tasbih preset ID", browser, StringComparison.Ordinal);
    }

    [Fact]
    public void Automation_readiness_api_tracks_bootstrap_state_changes() {
        var root = FindRepoRoot();
        var shell = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "components", "AppShell.tsx"));

        Assert.Contains("isReady: () => getAppState().bootstrapStatus === \"ready\"", shell, StringComparison.Ordinal);
        Assert.Contains("bootstrapAppState().catch", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_derived_caches_are_versioned_and_input_keyed() {
        var root = FindRepoRoot();
        var prayer = File.ReadAllText(Path.Combine(root, "PrayAdFree.Core", "Services", "PrayerTimesService.cs"));
        var today = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Services", "TodayWebRpcHandler.cs"));
        var geo = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Services", "GeoService.cs"));
        var scheduling = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Services", "LocalNotificationScheduler.cs"));
        Assert.Contains("CalculationVersion", prayer, StringComparison.Ordinal);
        Assert.Contains("ToString(\"R\"", prayer, StringComparison.Ordinal);
        Assert.Contains("SnapshotCacheSchemaVersion", today, StringComparison.Ordinal);
        Assert.Contains("BuildSnapshotInputKey", today, StringComparison.Ordinal);
        Assert.Contains("GeoCacheDocument", geo, StringComparison.Ordinal);
        Assert.Contains("ScheduleReconciliationVersion", scheduling, StringComparison.Ordinal);
        Assert.Contains("requestPermissions", scheduling, StringComparison.Ordinal);
        Assert.Contains("alarmDecision.Permissions", scheduling, StringComparison.Ordinal);
    }

    [Fact]
    public void Onboarding_commits_the_visible_default_language_before_completion() {
        var root = FindRepoRoot();
        var onboarding = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "routes", "onboarding.tsx"));
        Assert.Contains("const selectedLanguage = language || data.language || \"en\"", onboarding, StringComparison.Ordinal);
        Assert.Contains("await setLanguage(selectedLanguage)", onboarding, StringComparison.Ordinal);
        Assert.True(
            onboarding.IndexOf("await setLanguage(selectedLanguage)", StringComparison.Ordinal) <
            onboarding.IndexOf("onboarding.complete", StringComparison.Ordinal));
    }

    [Fact]
    public void Phase_eight_retired_ui_compatibility_surfaces_cannot_return() {
        var root = FindRepoRoot();
        Assert.False(File.Exists(Path.Combine(root, "Pray.web", "src", "client", "legacyClient.ts")));
        Assert.False(File.Exists(Path.Combine(root, "Pray.web", "src", "hooks", "useSnapshot.ts")));
        Assert.False(File.Exists(Path.Combine(root, "Pray.web", "src", "hooks", "useStoredSnapshot.ts")));

        foreach (var directory in new[] { "routes", "components" }) {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "Pray.web", "src", directory), "*.tsx", SearchOption.AllDirectories)) {
                var source = File.ReadAllText(file);
                Assert.DoesNotContain("legacyClient", source, StringComparison.Ordinal);
                Assert.DoesNotContain("settings.invoke", source, StringComparison.Ordinal);
                Assert.DoesNotContain("settings.setField", source, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Cold_start_has_bundled_labels_and_cannot_throw_before_bootstrap() {
        var root = FindRepoRoot();
        var appStore = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "state", "appStore.ts"));
        Assert.Contains("bundledEnglishLabels", appStore, StringComparison.Ordinal);
        Assert.DoesNotContain("throw new Error(`Missing app label", appStore, StringComparison.Ordinal);
        Assert.Contains("bootstrapStatus", appStore, StringComparison.Ordinal);
    }

    private static string FindRepoRoot() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "storage-edit.md"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
