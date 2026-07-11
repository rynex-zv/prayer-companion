using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public static class WebStateDefaults {
    public const string DefaultRemoteWebUrl = "http://pray.rynex.nl/";
    public const string DefaultTasbihRepeatMode = "Continue";
    public const string DefaultTasbihItemText = "SubhanAllah";
    public const int DefaultTasbihTargetCount = 33;

    public static WebState Build() {
        var state = new WebState {
            Language = "en",
            ThemeMode = "system",
            AccentColor = "teal",
            TextSize = 100,
            OnboardingCompleted = false,
            Country = "Netherlands",
            CountryCode = "NL",
            City = "Amsterdam",
            Latitude = 52.3676,
            Longitude = 4.9041,
            Heading = 95,
            ManualHeading = 100,
            HeadingMode = "auto",
            ReadingMode = "compass",
            FilterMode = "none",
            ClockFormat = "24h",
            RemoteWebUrl = DefaultRemoteWebUrl,
            SelectedTasbihPresetId = "after-prayer",
            TasbihPresets = BuildWebTasbihPresets()
        };
        return state;
    }

    public static void ApplyDefaults(WebState state) {
        var defaults = Build();
        if (string.IsNullOrWhiteSpace(state.Language)) {
            state.Language = defaults.Language;
        }

        if (string.IsNullOrWhiteSpace(state.ThemeMode)) {
            state.ThemeMode = defaults.ThemeMode;
        }

        if (string.IsNullOrWhiteSpace(state.AccentColor)) {
            state.AccentColor = defaults.AccentColor;
        }

        if (state.TextSize <= 0) {
            state.TextSize = defaults.TextSize;
        }

        if (string.IsNullOrWhiteSpace(state.Country)) {
            state.Country = defaults.Country;
        }

        if (string.IsNullOrWhiteSpace(state.CountryCode)) {
            state.CountryCode = defaults.CountryCode;
        }

        if (string.IsNullOrWhiteSpace(state.City)) {
            state.City = defaults.City;
        }

        if (state.Latitude == 0 && state.Longitude == 0) {
            state.Latitude = defaults.Latitude;
            state.Longitude = defaults.Longitude;
        }

        if (string.IsNullOrWhiteSpace(state.HeadingMode)) {
            state.HeadingMode = defaults.HeadingMode;
        }

        if (string.IsNullOrWhiteSpace(state.ReadingMode)) {
            state.ReadingMode = defaults.ReadingMode;
        }

        if (string.IsNullOrWhiteSpace(state.FilterMode)) {
            state.FilterMode = defaults.FilterMode;
        }

        if (string.IsNullOrWhiteSpace(state.ClockFormat)) {
            state.ClockFormat = defaults.ClockFormat;
        }

        if (string.IsNullOrWhiteSpace(state.RemoteWebUrl)) {
            state.RemoteWebUrl = defaults.RemoteWebUrl;
        }

        if (state.TasbihPresets.Count == 0) {
            state.TasbihPresets = BuildWebTasbihPresets();
        }

        if (!state.TasbihPresets.Any(item => item.Id == state.SelectedTasbihPresetId)) {
            state.SelectedTasbihPresetId = state.TasbihPresets[0].Id;
        }
    }

    private static List<WebTasbihPreset> BuildWebTasbihPresets() {
        var tasbih = TasbihDefaults.BuildDefaults();
        return tasbih.Presets.Select((preset, index) => new WebTasbihPreset(
            Id: index switch {
                0 => "after-prayer",
                1 => "hundred",
                2 => "salawat",
                _ => $"preset-{index + 1}"
            },
            Name: preset.Name,
            RepeatMode: preset.RepeatMode == TasbihRepeatMode.RepeatReset ? "Sequence" :
                preset.RepeatMode == TasbihRepeatMode.RepeatContinue ? "Loop" : "None",
            Items: preset.Items.Select(item => new WebTasbihItem(item.Text, item.TargetCount)).ToList())).ToList();
    }
}
