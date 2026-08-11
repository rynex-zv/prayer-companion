using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public static class WebStateDefaults {
    public const string DefaultRemoteWebUrl = "http://pray.rynex.nl/";
    public const string DefaultTasbihRepeatMode = "Continue";
    public const string DefaultTasbihItemText = "Tasbih_SubhanAllah";
    public const int DefaultTasbihTargetCount = 33;

    public static WebState Build() {
        var state = new WebState {
            Language = "en",
            ThemeMode = "system",
            AccentColor = "teal",
            TextSize = 100,
            OnboardingCompleted = false,
            Country = string.Empty,
            CountryCode = string.Empty,
            City = string.Empty,
            Latitude = 0,
            Longitude = 0,
            TimeZoneId = "UTC",
            LocationSource = string.Empty,
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

        ClearLegacyAmsterdamDefault(state);

        var hasCoordinates = state.Latitude != 0 || state.Longitude != 0;
        if (!hasCoordinates) {
            state.Country = string.Empty;
            state.CountryCode = string.Empty;
            state.City = string.Empty;
            state.LocationSource = string.Empty;
        } else {
            ClearMismatchedCatalogLocation(state);
        }

        if (string.IsNullOrWhiteSpace(state.TimeZoneId)) {
            state.TimeZoneId = defaults.TimeZoneId;
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

    private static void ClearLegacyAmsterdamDefault(WebState state) {
        if (state.UseGps) {
            return;
        }

        var isLegacyDefault =
            string.Equals(state.CountryCode, "NL", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(state.Country, "Netherlands", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(state.City, "Amsterdam", StringComparison.OrdinalIgnoreCase) &&
            Math.Abs(state.Latitude - 52.3676) < 0.000001 &&
            Math.Abs(state.Longitude - 4.9041) < 0.000001;
        if (!isLegacyDefault) {
            return;
        }

        state.CountryCode = string.Empty;
        state.Country = string.Empty;
        state.City = string.Empty;
        state.Latitude = 0;
        state.Longitude = 0;
        state.TimeZoneId = "UTC";
        state.LocationSource = string.Empty;
    }

    private static void ClearMismatchedCatalogLocation(WebState state) {
        var selected = WebCatalog.FindPlace(state.CountryCode, state.Country, state.City);
        if (selected is null) {
            return;
        }

        var nearest = WebCatalog.FindNearestPlace(state.Latitude, state.Longitude, 50);
        if (nearest is not null &&
            string.Equals(nearest.CountryCode, selected.CountryCode, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(nearest.City, selected.City, StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        state.Country = string.Empty;
        state.CountryCode = string.Empty;
        state.City = string.Empty;
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
