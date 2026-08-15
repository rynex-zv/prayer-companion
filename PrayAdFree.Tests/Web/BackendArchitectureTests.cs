using PrayAdFree.Core.Services;

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
        Assert.Contains("EnsureCoreWebView2Async", host, StringComparison.Ordinal);
        Assert.Contains("--force-renderer-accessibility", host, StringComparison.Ordinal);
        Assert.DoesNotContain("--force-renderer-accessibility=complete", host, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateCoreWebView2ControllerAsync", host, StringComparison.Ordinal);
        Assert.DoesNotContain("CoreWebView2ControllerWindowReference.CreateFromWindowHandle", host, StringComparison.Ordinal);
        Assert.DoesNotContain("windowsWebView.Opacity = 0", host, StringComparison.Ordinal);
        var uiaTest = File.ReadAllText(Path.Combine(root, "tools", "Test-WindowsAccessibility.ps1"));
        Assert.Contains("ControlType]::Document", uiaTest, StringComparison.Ordinal);
        Assert.Contains("NamedInteractiveControls", uiaTest, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_automation_builds_are_isolated_from_user_profile_data() {
        var root = FindRepoRoot();
        var project = File.ReadAllText(Path.Combine(root, "PrayAdFree", "PrayAdFree.csproj"));
        var runtime = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Services", "AutomationRuntime.cs"));
        var backend = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Services", "WebAppRpcHandler.cs"));
        var webBuild = File.ReadAllText(Path.Combine(root, "Pray.web", "scripts", "build.mjs"));
        var webConfig = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "automation", "config.ts"));
        var webMain = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "main.tsx"));
        var webRunner = File.ReadAllText(Path.Combine(root, "Pray.web", "scripts", "run-automation.mjs"));
        var browserBackend = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "native", "browserAppBackend.ts"));

        Assert.Contains("<EffectivePrayAutomation Condition=\"'$(Configuration)' == 'Debug' and '$(PrayAutomation)' == 'true'\">true</EffectivePrayAutomation>", project, StringComparison.Ordinal);
        Assert.Contains("<DefineConstants>$(DefineConstants);PRAY_AUTOMATION</DefineConstants>", project, StringComparison.Ordinal);
        Assert.Contains("<ApplicationId>com.rynex.prayer.automation</ApplicationId>", project, StringComparison.Ordinal);
        Assert.Contains("EnvironmentVariables=\"PRAY_AUTOMATION=$(EffectivePrayAutomation)\"", project, StringComparison.Ordinal);
        Assert.Contains("public static bool TestsEnabled { get; set; } =", runtime, StringComparison.Ordinal);
        Assert.Contains("Environment.GetEnvironmentVariable(\"PRAY_AUTOMATION\")", runtime, StringComparison.Ordinal);
        Assert.Contains("public static bool IsEnabled => CompiledForAutomation && TestsEnabled", runtime, StringComparison.Ordinal);
        Assert.Contains("Path.Combine(FileSystem.AppDataDirectory, \"AutomationState\")", runtime, StringComparison.Ordinal);
        Assert.Contains("Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), \"PrayAdFree\", \"app_settings.json\")", runtime, StringComparison.Ordinal);
        Assert.Contains("#if DEBUG && PRAY_AUTOMATION", backend, StringComparison.Ordinal);
        Assert.Contains("Build Debug with PrayAutomation=true and set PRAY_AUTOMATION=true", backend, StringComparison.Ordinal);
        Assert.Contains("VITE_PRAY_AUTOMATION_WINDOWS", webBuild, StringComparison.Ordinal);
        Assert.Contains("if (!enabled(import.meta.env.VITE_PRAY_AUTOMATION)) return false", webConfig, StringComparison.Ordinal);
        Assert.Contains("export const automationRoute = \"/test\"", webConfig, StringComparison.Ordinal);
        Assert.Contains("if (!automationRouteActive()) return false", webConfig, StringComparison.Ordinal);
        var testRoute = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "routes", "test.tsx"));
        Assert.Contains("import.meta.env.VITE_PRAY_AUTOMATION !== \"true\"", testRoute, StringComparison.Ordinal);
        Assert.Contains("throw redirect({ to: \"/\" })", testRoute, StringComparison.Ordinal);
        Assert.Contains("import.meta.env.VITE_PRAY_AUTOMATION === \"true\" && automationEnabled()", webMain, StringComparison.Ordinal);
        Assert.Contains("await import(\"./automation/runner\")", webMain, StringComparison.Ordinal);
        Assert.Contains("`${baseUrl}/test", webRunner, StringComparison.Ordinal);
        Assert.Contains("const DATABASE = import.meta.env.VITE_PRAY_AUTOMATION === \"true\" && automationEnabled()", browserBackend, StringComparison.Ordinal);
    }

    [Fact]
    public void Location_automatically_uses_gps_when_granted_and_ip_otherwise() {
        var root = FindRepoRoot();
        var adapter = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "native", "webPlatformAdapter.ts"));
        var resumePolicy = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "native", "locationResumePolicy.ts"));
        var store = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "state", "appStore.ts"));
        var shell = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "components", "AppShell.tsx"));
        var native = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Services", "WebAppRpcHandler.cs"));

        Assert.Contains("resolveAutomaticLocationSource(permissions.location", adapter, StringComparison.Ordinal);
        Assert.Contains("if (permission === \"granted\") return \"gps\"", resumePolicy, StringComparison.Ordinal);
        Assert.Contains("if (permission === \"denied\") return \"ip\"", resumePolicy, StringComparison.Ordinal);
        Assert.Contains("current?.useGps || current?.locationSource === \"gps\"", resumePolicy, StringComparison.Ordinal);
        Assert.Contains("canReuseConfirmedGpsLocation(current.data)", adapter, StringComparison.Ordinal);
        Assert.Contains("refreshAppLocation(\"auto\")", store, StringComparison.Ordinal);
        Assert.Contains("locationRefreshPromise", store, StringComparison.Ordinal);
        Assert.Contains("[\"/\", \"/calendar\", \"/settings/locations\"]", shell, StringComparison.Ordinal);
        Assert.Contains("5 * 60 * 1000", shell, StringComparison.Ordinal);
        Assert.Contains("IsLocationPermissionGrantedAsync", native, StringComparison.Ordinal);
        Assert.Contains("RefreshIpLocationAsync", native, StringComparison.Ordinal);
        Assert.Contains("vpnWarning = locationSource == \"ip\"", native, StringComparison.Ordinal);
    }

    [Fact]
    public void Browser_location_and_permission_changes_reach_the_live_react_ui() {
        var root = FindRepoRoot();
        var backend = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "native", "browserAppBackend.ts"));
        var adapter = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "native", "webPlatformAdapter.ts"));
        var store = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "state", "appStore.ts"));
        var today = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "routes", "index.tsx"));
        var permissions = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "routes", "settings.permissions.tsx"));
        var onboarding = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "routes", "onboarding.tsx"));
        var shell = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "components", "AppShell.tsx"));

        Assert.Contains("for (const event of response.events", backend, StringComparison.Ordinal);
        Assert.Contains("watchBrowserPermissionChanges", adapter, StringComparison.Ordinal);
        Assert.Contains("status.addEventListener(\"change\"", adapter, StringComparison.Ordinal);
        Assert.Contains("window.addEventListener(\"focus\"", adapter, StringComparison.Ordinal);
        Assert.Contains("previous !== permissions.location", store, StringComparison.Ordinal);
        Assert.Contains("refreshAppLocation(\"gps\")", today, StringComparison.Ordinal);
        Assert.Contains("refreshAppLocation(\"ip\")", today, StringComparison.Ordinal);
        Assert.Contains("today:location-choice", today, StringComparison.Ordinal);
        Assert.Contains("watchBrowserPermissionChanges", permissions, StringComparison.Ordinal);
        Assert.Contains("await confirmAppLocation(confirmedLocation)", onboarding, StringComparison.Ordinal);
        Assert.Contains("visibilitychange", shell, StringComparison.Ordinal);
        Assert.Contains("resumeAppState", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void Today_exposes_the_web_bundle_version_and_device_downloads_never_fail_silently() {
        var root = FindRepoRoot();
        var today = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "routes", "index.tsx"));
        var about = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "routes", "settings.about.tsx"));
        var manifest = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "public", "downloads", "manifest.json"));

        Assert.Contains("today:version", today, StringComparison.Ordinal);
        Assert.Contains("useWebVersion", today, StringComparison.Ordinal);
        Assert.Contains("about:download-native-app-status", about, StringComparison.Ordinal);
        Assert.Contains("detectDevicePlatform", about, StringComparison.Ordinal);
        Assert.Contains("contentType.includes(\"html\")", about, StringComparison.Ordinal);
        Assert.Contains("native-download-check", about, StringComparison.Ordinal);
        Assert.Contains("native-download-head", about, StringComparison.Ordinal);
        Assert.DoesNotContain("readWebBuild(candidate.version) <= readWebBuild(currentWebVersion)", about, StringComparison.Ordinal);
        Assert.DoesNotContain("currentWebVersion", about, StringComparison.Ordinal);
        Assert.Matches(@"PrayAdFree-Android-\d+\.\d+\.\d+-web\d+\.apk", manifest);
        Assert.Matches(@"PrayAdFree-Windows-x64-\d+\.\d+\.\d+-web\d+\.zip", manifest);
        Assert.Matches(@"\d+\.\d+\.\d+ \(web \d+\)", manifest);
        var webConfig = File.ReadAllText(Path.Combine(root, "Pray.web", "web.config"));
        Assert.Contains("application/vnd.android.package-archive", webConfig, StringComparison.Ordinal);
        Assert.Contains("application/zip", webConfig, StringComparison.Ordinal);
        Assert.Contains("<location path=\"downloads/manifest.json\">", webConfig, StringComparison.Ordinal);
    }

    [Fact]
    public void Android_native_version_has_one_visible_contract_across_project_manifest_and_downloads() {
        var root = FindRepoRoot();
        var project = File.ReadAllText(Path.Combine(root, "PrayAdFree", "PrayAdFree.csproj"));
        var androidManifest = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Platforms", "Android", "AndroidManifest.xml"));
        var downloadManifest = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "public", "downloads", "manifest.json"));

        Assert.Contains("<ApplicationDisplayVersion>0.0.506</ApplicationDisplayVersion>", project, StringComparison.Ordinal);
        Assert.Contains("<ApplicationVersion>8</ApplicationVersion>", project, StringComparison.Ordinal);
        Assert.Contains("android:versionCode=\"8\"", androidManifest, StringComparison.Ordinal);
        Assert.Contains("android:versionName=\"0.0.506\"", androidManifest, StringComparison.Ordinal);
        Assert.Contains("PrayAdFree-Android-0.0.506-web424.apk", downloadManifest, StringComparison.Ordinal);
        Assert.Contains("\"version\":  \"0.0.506 (web 424)\"", downloadManifest, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_custom_adhan_import_requires_a_name_and_returns_the_confirmed_projection() {
        var root = FindRepoRoot();
        var backend = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Services", "WebAppRpcHandler.cs"));
        var route = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "routes", "settings.adhan.tsx"));

        Assert.Contains("PromptForCustomAdhanSoundNameAsync", backend, StringComparison.Ordinal);
        Assert.Contains("CustomAdhanNamePrompt", backend, StringComparison.Ordinal);
        Assert.Contains("Name = customName.Trim()", backend, StringComparison.Ordinal);
        Assert.Contains("new PlatformOperationCompletion(BuildAdhanSettings(updated), \"settings.adhan\")", backend, StringComparison.Ordinal);
        Assert.Contains("Array.isArray(payload?.sounds)", route, StringComparison.Ordinal);
    }

    [Fact]
    public void Qibla_arrow_is_device_relative_and_sensor_updates_are_smoothed_and_serialized() {
        var root = FindRepoRoot();
        var service = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Services", "QiblaApplicationService.cs"));
        var route = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "routes", "qibla.tsx"));

        Assert.Contains("NeedleRotation = NormalizeHeading(Bearing - Heading)", service, StringComparison.Ordinal);
        Assert.Contains("NormalizeDelta(normalized - current)", service, StringComparison.Ordinal);
        Assert.Contains("headingRequestActive", route, StringComparison.Ordinal);
        Assert.Contains("delta * 0.18", route, StringComparison.Ordinal);
        Assert.Contains("screen?.orientation?.angle", route, StringComparison.Ordinal);
        Assert.Contains("qibla.startSensor", route, StringComparison.Ordinal);
        Assert.Contains("Compass.ReadingChanged", File.ReadAllText(Path.Combine(root, "PrayAdFree", "Services", "WebAppRpcHandler.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void Platform_specific_settings_projections_are_total_and_mobile_safe() {
        var root = FindRepoRoot();
        var locations = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "routes", "settings.locations.tsx"));
        var permissions = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "routes", "settings.permissions.tsx"));
        var notifications = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "routes", "settings.notifications.tsx"));
        var tasbih = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "routes", "settings.tasbih.tsx"));
        var native = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Services", "WebAppRpcHandler.cs"));

        Assert.Contains("Array.isArray(data.countries)", locations, StringComparison.Ordinal);
        Assert.Contains("Array.isArray(data.items)", permissions, StringComparison.Ordinal);
        Assert.Contains("data.showWindowsControls === true", notifications, StringComparison.Ordinal);
        Assert.Contains("DeviceInfo.Platform == DevicePlatform.WinUI", native, StringComparison.Ordinal);
        Assert.Contains("TasbihPreset_", native, StringComparison.Ordinal);
        Assert.Contains("grid-cols-1", tasbih, StringComparison.Ordinal);
    }

    [Fact]
    public void Remote_web_origin_is_https_only() {
        Assert.Equal("https://pray.rynex.nl/", WebStateDefaults.DefaultRemoteWebUrl);
        var dispatcher = new WebCoreRpcDispatcher();
        Assert.Throws<InvalidOperationException>(() => dispatcher.Dispatch(
            "mauiWebber.setRemoteUrl",
            System.Text.Json.JsonSerializer.SerializeToElement(new { url = "http://pray.rynex.nl/" })));
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
        var main = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "main.tsx"));

        Assert.Contains("isReady: () => getAppState().bootstrapStatus === \"ready\"", shell, StringComparison.Ordinal);
        Assert.Contains("bootstrapAppState().catch", shell, StringComparison.Ordinal);
        Assert.Contains("pathname === \"/test\"", shell, StringComparison.Ordinal);
        Assert.Contains("const shouldRunAutomation", main, StringComparison.Ordinal);
        Assert.Contains("latchAutomationRuntime()", main, StringComparison.Ordinal);
        Assert.True(
            main.IndexOf("latchAutomationRuntime()", StringComparison.Ordinal) <
            main.IndexOf("<RouterProvider", StringComparison.Ordinal));
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
    public void Android_silent_alarm_stays_alarm_and_uses_runtime_vibration() {
        var root = FindRepoRoot();
        var scheduling = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Services", "LocalNotificationScheduler.cs"));
        var playback = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Services", "AdhanPlaybackService.cs"));
        var capability = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Services", "AndroidAlarmCapabilityService.cs"));
        var sounds = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Services", "AdhanSoundLibrary.cs"));
        var fullscreen = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Platforms", "Android", "AndroidAlarmFullscreenNotifier.cs"));
        var normalizedScheduling = scheduling.ReplaceLineEndings("\n");
        var normalizedPlayback = playback.ReplaceLineEndings("\n");

        Assert.Contains("ResolvePrayerEffectiveSoundKey", sounds, StringComparison.Ordinal);
        Assert.Contains("ResolvePrayerEffectiveSoundKey(settings.Notifications, overrideSettings?.SoundKey)", scheduling, StringComparison.Ordinal);
        Assert.Contains("ReturningData = openAlarmScreen", scheduling, StringComparison.Ordinal);
        Assert.Contains("AdhanAlarmPayload.Build(item.Prayer, effectiveSoundKey, item.Time, item.Time)", scheduling, StringComparison.Ordinal);
        Assert.DoesNotContain("ReturningData = isSilent", scheduling, StringComparison.Ordinal);
        Assert.DoesNotContain("VibrationPattern = isSilent ? Array.Empty<long>()", scheduling, StringComparison.Ordinal);
        Assert.Contains("private static bool ResolveNotificationSilent", scheduling, StringComparison.Ordinal);
        Assert.Contains("_ = isSilent;\n        return false;\n#else", normalizedScheduling, StringComparison.Ordinal);
        Assert.Contains("PrayerSilentChannelId = \"prayer_silent_v4\"", scheduling, StringComparison.Ordinal);

        Assert.Contains("StartAndroidAlarmVibration(settings.Notifications, payload.Prayer)", playback, StringComparison.Ordinal);
        Assert.Contains("if (source != null)", playback, StringComparison.Ordinal);
        Assert.Contains("AdhanSoundLibrary.IsSilent(effectiveSoundKey) && !openAlarmScreen", playback, StringComparison.Ordinal);
        Assert.Contains("AndroidControlChannelId = \"adhan_playback_control_v2\"", playback, StringComparison.Ordinal);
        Assert.Contains("HandlePlaybackCompletedAsync(\"Android\")", playback, StringComparison.Ordinal);
        Assert.Contains("HandlePlaybackCompletedAsync(\"WindowsFailed\")", playback, StringComparison.Ordinal);
        Assert.Contains("StopCore(clearActiveState: !keepAlarm, stopAlarmVibration: !keepAlarm)", playback, StringComparison.Ordinal);
        Assert.DoesNotContain("private void OnAndroidCompletion(object? sender, EventArgs e) {\n        _ = StopAsync();", normalizedPlayback, StringComparison.Ordinal);
        var failedStart = normalizedPlayback.IndexOf("private void OnWindowsMediaFailed", StringComparison.Ordinal);
        var failedEnd = normalizedPlayback.IndexOf("private void StartWindowsNotificationMonitor", failedStart, StringComparison.Ordinal);
        var failedHandler = normalizedPlayback[failedStart..failedEnd];
        Assert.Contains("HandlePlaybackCompletedAsync(\"WindowsFailed\")", failedHandler, StringComparison.Ordinal);
        Assert.DoesNotContain("StopAsync()", failedHandler, StringComparison.Ordinal);

        Assert.Contains("schedulingMode == AlarmSchedulingMode.ExactAlarm", capability, StringComparison.Ordinal);
        Assert.Contains("return AlarmPresentationMode.FullscreenActivity", capability, StringComparison.Ordinal);
        Assert.DoesNotContain("screenOnAndUnlocked && permissions.DisplayOverAppsGranted", capability, StringComparison.Ordinal);
        Assert.DoesNotContain("return AlarmPresentationMode.Overlay", capability, StringComparison.Ordinal);
        Assert.Contains("adhan_alarm_fullscreen_v2", fullscreen, StringComparison.Ordinal);
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

    [Fact]
    public void Alarm_uses_the_main_react_webview_and_inactive_route_stays_on_alarm_page() {
        var root = FindRepoRoot();
        var playback = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Services", "AdhanPlaybackService.cs"));
        var backend = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Services", "WebAppRpcHandler.cs"));
        var webber = File.ReadAllText(Path.Combine(root, "MauiWebber", "MauiWebberPage.cs"));
        var alarm = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "routes", "alarm.tsx"));
        var appStore = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "state", "appStore.ts"));
        var shell = File.ReadAllText(Path.Combine(root, "Pray.web", "src", "components", "AppShell.tsx"));
        var maui = File.ReadAllText(Path.Combine(root, "PrayAdFree", "MauiProgram.cs"));
        var receiver = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Platforms", "Android", "AndroidAdhanAlarmReceiver.cs"));
        var coordinator = File.ReadAllText(Path.Combine(root, "PrayAdFree", "Platforms", "Android", "AndroidAlarmLaunchCoordinator.cs"));

        Assert.Contains("NavigateToRouteAsync(\"/alarm\"", playback, StringComparison.Ordinal);
        Assert.Contains("startup = new", backend, StringComparison.Ordinal);
        Assert.Contains("route = alarm.IsActive ? \"/alarm\" : \"/\"", backend, StringComparison.Ordinal);
        Assert.Contains("intent = alarm.IsActive ? \"alarm\"", backend, StringComparison.Ordinal);
        Assert.Contains("alarm = alarm.Snapshot", backend, StringComparison.Ordinal);
        Assert.DoesNotContain("PushModalAsync", playback, StringComparison.Ordinal);
        Assert.Contains("public async Task<bool> NavigateToRouteAsync", webber, StringComparison.Ordinal);
        Assert.DoesNotContain("navigate({ to: \"/\", replace: true })", alarm, StringComparison.Ordinal);
        Assert.Contains("data-selector-name=\"alarm:inactive\"", alarm, StringComparison.Ordinal);
        Assert.Contains("entryRefreshComplete", alarm, StringComparison.Ordinal);
        Assert.Contains("alarmActiveRef.current", alarm, StringComparison.Ordinal);
        Assert.Contains("void poll();", alarm, StringComparison.Ordinal);
        Assert.DoesNotContain("timer = window.setTimeout(poll, intervalMs)", alarm, StringComparison.Ordinal);
        Assert.Contains("startupRoute: response.data.startup?.route", appStore, StringComparison.Ordinal);
        Assert.Contains("startupIntent: response.data.startup?.intent", appStore, StringComparison.Ordinal);
        Assert.Contains("handledStartupIntent", shell, StringComparison.Ordinal);
        Assert.Contains("shell.startupIntent === \"alarm\"", shell, StringComparison.Ordinal);
        Assert.Contains("ActivateAlarmAsync(payload, settings, showAlarmScreen: false)", playback, StringComparison.Ordinal);
        Assert.Contains("AdhanPlaybackService.StartAlarmAudio", playback, StringComparison.Ordinal);
        Assert.Contains("HandlePlaybackCompletedAsync", playback, StringComparison.Ordinal);
        Assert.Contains("AndroidAlarmLaunchCoordinator.Enqueue(payload)", receiver, StringComparison.Ordinal);
        Assert.Contains("TryGetPendingPayload", coordinator, StringComparison.Ordinal);
        Assert.Contains("MainActivity.Intent receivedAtUtc", File.ReadAllText(Path.Combine(root, "PrayAdFree", "Platforms", "Android", "MainActivity.cs")), StringComparison.Ordinal);
        Assert.Contains("Coordinator.Dispatch start", coordinator, StringComparison.Ordinal);
        Assert.Contains("AndroidAlarmLaunchCoordinator.TryGetPendingPayload", backend, StringComparison.Ordinal);
        Assert.Contains("showAlarmScreen: false", backend, StringComparison.Ordinal);
        Assert.Contains("alreadyActive", playback, StringComparison.Ordinal);
        Assert.Contains("AdhanAlarmLaunch.AndroidReady", playback, StringComparison.Ordinal);
        Assert.Contains("Stopwatch.GetElapsedTime", playback, StringComparison.Ordinal);
        Assert.DoesNotContain("AddTransient<AdhanSnoozePage>", maui, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(root, "PrayAdFree", "Pages", "AdhanSnoozePage.cs")));
    }

    private static string FindRepoRoot() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "storage-edit.md"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
