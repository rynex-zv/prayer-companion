using System.Text.Json;

namespace PrayAdFree.Tests;

public sealed class MauiWebberAssetTests {
    [Theory]
    [InlineData("PrayAdFree/Resources/Raw/web")]
    [InlineData("Pray.web")]
    public void WebberManifest_ReferencesExistingFiles(string relativeRoot) {
        var root = Path.Combine(GetRepoRoot(), relativeRoot.Replace('/', Path.DirectorySeparatorChar));
        var manifestPath = Path.Combine(root, "webber-manifest.json");

        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var entry = manifest.RootElement.GetProperty("entry").GetString();

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
        var js = File.ReadAllText(Path.Combine(webRoot, "assets", "app.js"));

        Assert.Contains("id=\"nextPrayerLabel\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"timeLeftLabel\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"todayPrayerTimesLabel\"", html, StringComparison.Ordinal);
        Assert.Contains("labels.nextPrayer", js, StringComparison.Ordinal);
        Assert.Contains("labels.timeLeft", js, StringComparison.Ordinal);
        Assert.Contains("labels.todayPrayerTimes", js, StringComparison.Ordinal);
        Assert.Contains("mauiWebber.trace", js, StringComparison.Ordinal);
        Assert.Contains("renderComplete", js, StringComparison.Ordinal);
    }

    [Fact]
    public void ArabicAndEnglishLocalization_ProvideTodayWebLabels() {
        foreach (var language in new[] { "en", "ar" }) {
            var path = Path.Combine(GetRepoRoot(), "PrayAdFree", "Resources", "Raw", "i18n", $"{language}.json");
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
                ?? throw new InvalidOperationException($"Failed to parse {path}");

            foreach (var key in new[] { "NextPrayer", "TimeLeft", "TodayPrayTimesLabel", "Iftar", "Imsak", "Refresh", "Refreshing", "LastUpdated", "LastUpdatedFormat", "BaseTimeLabel" }) {
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

    private static string GetRepoRoot() {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }
}
