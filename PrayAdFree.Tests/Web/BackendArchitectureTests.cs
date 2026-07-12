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

    private static string FindRepoRoot() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "storage-edit.md"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
