using System.Text.Json;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public sealed class MauiWebberAssetTests {
    [Theory]
    [InlineData("PrayAdFree/Resources/Raw/web")]
    [InlineData("Pray.web/dist")]
    public void WebberManifest_ReferencesExistingFiles(string relativeRoot) {
        var root = Path.Combine(GetRepoRoot(), relativeRoot.Replace('/', Path.DirectorySeparatorChar));
        var manifestPath = Path.Combine(root, "webber-manifest.json");

        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var contractVersion = manifest.RootElement.GetProperty("contractVersion").GetInt32();
        var entry = manifest.RootElement.GetProperty("entry").GetString();

        Assert.Equal(WebContractExporter.SchemaVersion, contractVersion);
        Assert.False(string.IsNullOrWhiteSpace(entry));
        Assert.True(File.Exists(Path.Combine(root, entry!)), $"Missing manifest entry: {entry}");

        foreach (var file in manifest.RootElement.GetProperty("files").EnumerateArray()) {
            var path = file.GetProperty("path").GetString();
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(File.Exists(Path.Combine(root, path!.Replace('/', Path.DirectorySeparatorChar))), $"Missing manifest file: {path}");
        }
    }

    [Fact]
    public void EmbeddedTodayWebUi_UsesNativeLabelsAndPerformanceTrace() {
        var webRoot = Path.Combine(GetRepoRoot(), "PrayAdFree", "Resources", "Raw", "web");
        var html = File.ReadAllText(Path.Combine(webRoot, "index.html"));
        var js = string.Join(Environment.NewLine, GetManifestFiles(webRoot, ".js").Select(File.ReadAllText));
        var sourceRoot = Path.Combine(GetRepoRoot(), "Pray.web", "src");
        var nativeClientSource = File.ReadAllText(Path.Combine(sourceRoot, "native", "mauiWebberClient.ts"));
        var todaySource = File.ReadAllText(Path.Combine(sourceRoot, "routes", "index.tsx"));

        Assert.Contains("id=\"app\"", html, StringComparison.Ordinal);
        Assert.Contains("today.getSnapshot", js, StringComparison.Ordinal);
        Assert.Contains("today.refresh", js, StringComparison.Ordinal);
        Assert.Contains("app.getShellSnapshot", js, StringComparison.Ordinal);
        Assert.Contains("mauiWebber.trace", nativeClientSource, StringComparison.Ordinal);
        Assert.Contains("renderComplete", todaySource, StringComparison.Ordinal);
    }

    [Fact]
    public void ArabicAndEnglishLocalization_ProvideTodayWebLabels() {
        foreach (var language in new[] { "en", "ar" }) {
            var path = Path.Combine(GetRepoRoot(), "PrayAdFree", "Resources", "Raw", "i18n", $"{language}.json");
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
                ?? throw new InvalidOperationException($"Failed to parse {path}");

            foreach (var key in new[] { "nextPrayer", "TimeLeft", "TodayPrayTimesLabel", "iftar", "imsak", "refresh", "Refreshing", "LastUpdated", "LastUpdatedFormat", "BaseTimeLabel" }) {
                Assert.True(values.TryGetValue(key, out var value), $"{language}.json missing key '{key}'");
                Assert.False(string.IsNullOrWhiteSpace(value), $"{language}.json key '{key}' is empty");
                if (language == "ar") {
                    Assert.NotEqual(key, value);
                }
            }
        }
    }

    [Fact]
    public void AndroidDebugBuild_EmbedsAssembliesIntoApk() {
        var project = File.ReadAllText(Path.Combine(GetRepoRoot(), "PrayAdFree", "PrayAdFree.csproj"));

        Assert.Contains("'$(TargetFramework)' == 'net10.0-android' and '$(Configuration)' == 'Debug'", project, StringComparison.Ordinal);
        Assert.Contains("<EmbedAssembliesIntoApk>true</EmbedAssembliesIntoApk>", project, StringComparison.Ordinal);
    }

    [Fact]
    public void MauiBuild_RunsPhoneFrontendBuildByDefault() {
        var project = File.ReadAllText(Path.Combine(GetRepoRoot(), "PrayAdFree", "PrayAdFree.csproj"));

        Assert.Contains("Name=\"BuildPhoneFrontend\"", project, StringComparison.Ordinal);
        Assert.Contains("BeforeTargets=\"PrepareForBuild\"", project, StringComparison.Ordinal);
        Assert.Contains("$(SkipFrontendBuild)' != 'true'", project, StringComparison.Ordinal);
        Assert.Contains("run build -- $(FrontendBuildArgs)", project, StringComparison.Ordinal);
        Assert.Contains("<FrontendBuildArgs Condition=\"'$(FrontendBuildArgs)' == ''\">--phone</FrontendBuildArgs>", project, StringComparison.Ordinal);
    }

    private static string GetRepoRoot() {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }

    private static IEnumerable<string> GetManifestFiles(string webRoot, string extension) {
        var manifestPath = Path.Combine(webRoot, "webber-manifest.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));

        return manifest.RootElement.GetProperty("files")
            .EnumerateArray()
            .Select(file => file.GetProperty("path").GetString())
            .Where(path => path != null && path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.Combine(webRoot, path!.Replace('/', Path.DirectorySeparatorChar)))
            .ToList();
    }
}
