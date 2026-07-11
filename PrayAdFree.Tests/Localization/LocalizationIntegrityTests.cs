using System.Text.Json;

namespace PrayAdFree.Tests;

public sealed class LocalizationIntegrityTests {
    private static readonly string[] Languages = ["en", "ar", "fr", "tr", "es"];
    private static readonly string[] CriticalKeys = [
        "today",
        "calendar",
        "qibla",
        "tasbih",
        "settings",
        "notifications",
        "adhanReminders",
        "primaryAdhanType",
        "ReminderType",
        "reminderType_Adhan",
        "reminderType_Notification",
        "reminderType_Silent",
        "reminderType_Alarm",
        "PermissionsTitle",
        "PermissionsSubtitle",
        "PermissionStatus_Enabled",
        "PermissionStatus_Disabled",
        "PermissionAction_Request",
        "PermissionAction_OpenSettings",
        "PermissionsNotificationsTitle",
        "PermissionsNotificationsDescription",
        "PermissionsExactAlarmTitle",
        "PermissionsExactAlarmDescription",
        "PermissionsLocationTitle",
        "PermissionsLocationDescription",
        "AlarmRemindersTitle",
        "AlarmRemindersBuiltIn",
        "AlarmRemindersUser",
        "AlarmReminderNewPlaceholder",
        "AlarmReminderEnable",
        "AlarmReminderDisable",
        "AlarmReminderEdit",
        "AlarmReminderEditTitle",
        "AlarmReminderEditHint",
        "AlarmScreenTitle",
        "AlarmStopButton",
        "AlarmSnoozeButton",
        "testAlarm",
        "StopAdhan"
    ];

    [Fact]
    public void AllLocalizationJsonFiles_ParseSuccessfully() {
        foreach (var path in Directory.GetFiles(GetI18nDirectory(), "*.json")) {
            var text = File.ReadAllText(path);
            var ex = Record.Exception(() => JsonDocument.Parse(text));
            Assert.Null(ex);
        }
    }

    [Fact]
    public void CriticalKeys_ExistInAllLanguages() {
        foreach (var language in Languages) {
            var data = LoadLanguage(language);
            foreach (var key in CriticalKeys) {
                Assert.True(data.ContainsKey(key), $"{language}.json missing key '{key}'");
            }
        }
    }

    [Fact]
    public void ArabicCriticalValues_AreNotRawKeysOrQuestionMarks() {
        var ar = LoadLanguage("ar");
        foreach (var key in CriticalKeys) {
            var value = ar[key];
            Assert.False(string.IsNullOrWhiteSpace(value), $"ar.json key '{key}' is empty");
            Assert.NotEqual(key, value);
            Assert.DoesNotContain("???", value, StringComparison.Ordinal);
        }
    }

    private static Dictionary<string, string> LoadLanguage(string languageCode) {
        var path = Path.Combine(GetI18nDirectory(), $"{languageCode}.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? throw new InvalidOperationException($"Failed to parse {path}");
    }

    private static string GetI18nDirectory() {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return Path.Combine(repoRoot, "PrayAdFree", "Resources", "Raw", "i18n");
    }
}
