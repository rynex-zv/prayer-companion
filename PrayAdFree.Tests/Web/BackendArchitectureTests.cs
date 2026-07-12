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
