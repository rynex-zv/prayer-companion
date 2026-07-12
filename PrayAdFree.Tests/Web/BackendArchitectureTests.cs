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
