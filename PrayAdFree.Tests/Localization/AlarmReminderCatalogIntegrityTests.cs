using System.Text.Json;

namespace PrayAdFree.Tests.Localization;

public sealed class AlarmReminderCatalogIntegrityTests {
    private static readonly string[] Languages = ["en", "ar", "fr", "tr", "es"];

    [Fact]
    public void AllAlarmReminderCatalogFiles_ParseSuccessfully() {
        foreach (var language in Languages) {
            var text = File.ReadAllText(Path.Combine(GetCatalogDirectory(), $"{language}.json"));
            var ex = Record.Exception(() => JsonDocument.Parse(text));
            Assert.Null(ex);
        }
    }

    [Fact]
    public void ArabicAlarmReminderCatalog_DoesNotContainMojibake() {
        var text = File.ReadAllText(Path.Combine(GetCatalogDirectory(), "ar.json"));
        Assert.DoesNotContain("Ø", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Ù", text, StringComparison.Ordinal);
    }

    private static string GetCatalogDirectory() {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return Path.Combine(repoRoot, "PrayAdFree", "Resources", "Raw", "alarm_reminders");
    }
}
