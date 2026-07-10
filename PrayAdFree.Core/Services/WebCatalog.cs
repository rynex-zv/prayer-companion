using System.Reflection;
using System.Text.Json;
using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public static class WebCatalog {
    public static IReadOnlyList<WebLanguageOption> Languages { get; } = new[] {
        new WebLanguageOption("en", "English", "ltr"),
        new WebLanguageOption("ar", "العربية", "rtl"),
        new WebLanguageOption("fr", "Français", "ltr"),
        new WebLanguageOption("es", "Español", "ltr"),
        new WebLanguageOption("tr", "Türkçe", "ltr")
    };

    public static IReadOnlyList<string> AccentColors { get; } = new[] {
        "teal", "green", "blue", "amber", "rose"
    };

    public static IReadOnlyList<WebShellTabOption> ShellTabs { get; } = new[] {
        new WebShellTabOption("today", "today", "sun"),
        new WebShellTabOption("calendar", "calendar", "calendar"),
        new WebShellTabOption("qibla", "qibla", "compass"),
        new WebShellTabOption("tasbih", "tasbih", "circle"),
        new WebShellTabOption("settings", "settings", "settings")
    };

    public static IReadOnlyList<WebLabeledOption> HeadingModes { get; } = new[] {
        new WebLabeledOption("auto", "auto"),
        new WebLabeledOption("manual", "manual")
    };

    public static IReadOnlyList<WebLabeledOption> QiblaReadingModes { get; } = new[] {
        new WebLabeledOption("compass", "compass"),
        new WebLabeledOption("map", "map")
    };

    public static IReadOnlyList<WebLabeledOption> QiblaFilterModes { get; } = new[] {
        new WebLabeledOption("none", "filter_none"),
        new WebLabeledOption("night", "filter_night"),
        new WebLabeledOption("contrast", "filter_contrast")
    };

    public static WebAdhanDefaults AdhanDefaults { get; } = new(
        Volume: 80,
        CalculationMethod: "Auto",
        Madhhab: "Shafi",
        HighLatitudeRule: "MiddleOfTheNight",
        FajrAngle: 18,
        IshaAngle: 17,
        ClockFormat: "24h");

    public static WebNotificationDefaults NotificationDefaults { get; } = new(
        EnableAdhan: true,
        MobilePrimaryAdhanType: "Full",
        HideOnCloseWindows: false,
        RunBackgroundServiceWindows: false,
        Vibration: false,
        VibrationStrength: "Medium",
        VibrationPattern: "Default",
        MinutesBefore: 10);

    public static IReadOnlyList<WebPlaceOption> Places { get; } = new[] {
        new WebPlaceOption("Netherlands", "NL", "Amsterdam", 52.3676, 4.9041),
        new WebPlaceOption("Netherlands", "NL", "Rotterdam", 51.9244, 4.4777),
        new WebPlaceOption("Netherlands", "NL", "Utrecht", 52.0907, 5.1214),
        new WebPlaceOption("Saudi Arabia", "SA", "Makkah", 21.3891, 39.8579),
        new WebPlaceOption("Saudi Arabia", "SA", "Madinah", 24.5247, 39.5692),
        new WebPlaceOption("Saudi Arabia", "SA", "Riyadh", 24.7136, 46.6753)
    };

    public static IReadOnlyList<WebCountryOption> Countries => Places
        .GroupBy(item => new { item.CountryCode, item.Country })
        .Select(group => new WebCountryOption(
            group.Key.CountryCode,
            group.Key.Country,
            group.Select(item => item.City).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item).ToArray()))
        .OrderBy(item => item.Name)
        .ToArray();

    public static IReadOnlyList<WebPermissionItem> BrowserPermissionItems { get; } = new[] {
        new WebPermissionItem("location", "Location", "critical", "Browser geolocation can be requested here.", "Manual entry", "Available", "Grant"),
        new WebPermissionItem("notifications", "Notifications", "critical", "Browser notifications can be requested here.", "In-app messages", "Available", "Grant"),
        new WebPermissionItem("background", "Background activity", "optional", "Background native alarms are not available in browser web.", "Foreground only", "Not available", "Unavailable")
    };

    public static WebAboutInfo AboutInfo { get; } = new(
        Name: "Pray Ad Free",
        Maintainer: "Rynex",
        Email: "rynex@rynex.nl",
        Phone: "+31610331734",
        Website: "https://pray.rynex.nl",
        RemoteWebUrl: WebStateDefaults.DefaultRemoteWebUrl);

    public static IReadOnlyList<WebAdhanSoundOption> DefaultAdhanSounds { get; } = new[] {
        new WebAdhanSoundOption("makkah", "Makkah", true, false, false)
    };

    public static IReadOnlyList<WebReminderOption> BuiltInAlarmReminders { get; } = new[] {
        new WebReminderOption("wudu", "Make wudu before prayer", true),
        new WebReminderOption("qibla", "Face the Qibla", true)
    };

    public static IReadOnlyDictionary<string, string> Labels(string language) => LabelCatalog.ForLanguage(NormalizeLanguage(language));

    public static string Translate(string language, string key) =>
        Labels(language).TryGetValue(key, out var value) ? value : key;

    public static bool IsRtl(string language) => string.Equals(NormalizeLanguage(language), "ar", StringComparison.Ordinal);

    public static string NormalizeLanguage(string? language) =>
        Languages.Any(item => string.Equals(item.Code, language, StringComparison.Ordinal)) ? language! : "en";

    public static string NormalizeTheme(string? theme) => theme is "light" or "dark" ? theme : "system";

    public static string NormalizeAccent(string? accent) =>
        AccentColors.Contains(accent ?? "", StringComparer.Ordinal) ? accent! : "teal";

    public static int ClampTextSize(int value) => Math.Clamp(value, 75, 150);

    public static object[] LocalizedOptions(IEnumerable<WebLabeledOption> options, string language) =>
        options.Select(item => new { id = item.Id, label = Translate(language, item.LabelKey) }).ToArray<object>();

    public static object[] LocalizedShellTabs(string language) =>
        ShellTabs.Select(item => new { id = item.Id, label = Translate(language, item.LabelKey), icon = item.Icon }).ToArray<object>();

    public static string QiblaDisplayLabel(string language, string readingMode) =>
        Translate(language, readingMode == "map" ? "map" : "compass");

    public static string QiblaFilterLabel(string language, string filterMode) =>
        Translate(language, filterMode switch { "night" => "filter_night", "contrast" => "filter_contrast", _ => "filter_none" });

    public static string NativeActionMessageKey(string action) => action switch {
        "requestPermission" => "webPermissionRequestHandled",
        "requestAllPermissions" => "webPermissionsRequestHandled",
        "refreshGps" => "webGpsHandledByAdapter",
        "addCustomAdhanSound" or "testNotification" or "previewSound" or "removeCustomAdhanSound" => "webNativeAdhanUnavailable",
        _ => "webNativeActionUnavailable"
    };

    public static WebPlaceOption? FindPlace(string? countryCode, string? country, string? city) {
        return Places.FirstOrDefault(item =>
            (string.Equals(item.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(item.Country, country, StringComparison.OrdinalIgnoreCase)) &&
            string.Equals(item.City, city, StringComparison.OrdinalIgnoreCase));
    }

    private static class LabelCatalog {
        private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> Catalog = new(Load);

        public static IReadOnlyDictionary<string, string> ForLanguage(string language) {
            var catalog = Catalog.Value;
            return catalog.TryGetValue(language, out var labels) ? labels : catalog["en"];
        }

        private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Load() {
            var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
            foreach (var language in Languages.Select(item => item.Code)) {
                result[language] = LoadLanguage(language);
            }

            EnsureSameKeys(result);
            return result;
        }

        private static IReadOnlyDictionary<string, string> LoadLanguage(string language) {
            var resourceName = $"PrayAdFree.Core.Resources.i18n.{language}.json";
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Missing Core i18n resource: {resourceName}");
            var labels = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
                ?? throw new InvalidOperationException($"Invalid Core i18n resource: {resourceName}");
            return new Dictionary<string, string>(labels, StringComparer.Ordinal);
        }

        private static void EnsureSameKeys(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> catalog) {
            var englishKeys = catalog["en"].Keys.OrderBy(item => item, StringComparer.Ordinal).ToArray();
            foreach (var (language, labels) in catalog) {
                var keys = labels.Keys.OrderBy(item => item, StringComparer.Ordinal).ToArray();
                if (!englishKeys.SequenceEqual(keys)) {
                    throw new InvalidOperationException($"Core i18n resource '{language}' must have the same keys as 'en'.");
                }
            }
        }
    }
}

