using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public static class WebContractExporter {
    public static object Export() {
        var defaults = WebStateDefaults.Build();
        var languages = WebCatalog.Languages.Select(item => item.Code).ToArray();
        var labels = languages.ToDictionary(
            language => language,
            language => WebCatalog.Labels(language),
            StringComparer.Ordinal);

        return new {
            schemaVersion = 1,
            generatedFrom = "PrayAdFree.Core",
            repository = "PrayAdFree",
            templateRoles = new {
                core = new { role = "core", currentPath = "PrayAdFree.Core", description = "Business logic and RPC source of truth" },
                webClient = new { role = "web.client", currentPath = "Pray.web", description = "Lovable-editable React client" },
                webBridge = new { role = "web.bridge", currentPath = "PrayAdFree.WebBridge", description = "WASM connector only" },
                appHost = new { role = "app.host", currentPath = "PrayAdFree", description = "MAUI phone and Windows host" },
                coreTests = new { role = "core.tests", currentPath = "PrayAdFree.Tests", description = "Core and contract tests" }
            },
            rpcMethods = RpcMethods,
            defaults = new {
                language = defaults.Language,
                themeMode = defaults.ThemeMode,
                accentColor = defaults.AccentColor,
                textSize = defaults.TextSize,
                onboardingCompleted = defaults.OnboardingCompleted,
                country = defaults.Country,
                countryCode = defaults.CountryCode,
                city = defaults.City,
                latitude = defaults.Latitude,
                longitude = defaults.Longitude,
                headingMode = defaults.HeadingMode,
                readingMode = defaults.ReadingMode,
                filterMode = defaults.FilterMode,
                clockFormat = defaults.ClockFormat,
                remoteWebUrl = defaults.RemoteWebUrl,
                selectedTasbihPresetId = defaults.SelectedTasbihPresetId,
                tasbihRepeatMode = WebStateDefaults.DefaultTasbihRepeatMode,
                tasbihItemText = WebStateDefaults.DefaultTasbihItemText,
                tasbihTargetCount = WebStateDefaults.DefaultTasbihTargetCount,
                adhan = WebCatalog.AdhanDefaults,
                notifications = WebCatalog.NotificationDefaults
            },
            catalog = new {
                languages = WebCatalog.Languages,
                accentColors = WebCatalog.AccentColors,
                shellTabs = WebCatalog.ShellTabs,
                headingModes = WebCatalog.HeadingModes,
                qiblaReadingModes = WebCatalog.QiblaReadingModes,
                qiblaFilterModes = WebCatalog.QiblaFilterModes,
                countries = WebCatalog.Countries,
                places = WebCatalog.Places,
                browserPermissionItems = WebCatalog.BrowserPermissionItems,
                about = WebCatalog.AboutInfo,
                adhanSounds = WebCatalog.DefaultAdhanSounds,
                builtInAlarmReminders = WebCatalog.BuiltInAlarmReminders,
                tasbihPresets = defaults.TasbihPresets
            },
            labels
        };
    }

    public static IReadOnlyList<string> RpcMethods { get; } = new[] {
        "app.getShellSnapshot",
        "app.getLocalization",
        "app.getLanguageObject",
        "app.setLanguage",
        "app.setTheme",
        "app.navigate",
        "app.importState",
        "app.exportState",
        "today.getSnapshot",
        "today.refresh",
        "calendar.getSnapshot",
        "calendar.setMonth",
        "calendar.today",
        "calendar.nextMonth",
        "calendar.previousMonth",
        "qibla.getSnapshot",
        "qibla.updateHeading",
        "qibla.setHeadingMode",
        "qibla.adjustManualHeading",
        "qibla.commitManualHeading",
        "qibla.setDisplayMode",
        "qibla.setVisualFilter",
        "tasbih.getSnapshot",
        "tasbih.increment",
        "tasbih.reset",
        "tasbih.selectPreset",
        "settings.getSnapshot",
        "settings.setField",
        "settings.patch",
        "settings.invoke",
        "onboarding.getSnapshot",
        "onboarding.complete",
        "mauiWebber.getRemoteUrl",
        "mauiWebber.setRemoteUrl",
        "mauiWebber.pullRemote",
        "mauiWebber.useEmbedded"
    };
}
