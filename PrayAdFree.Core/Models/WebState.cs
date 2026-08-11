using PrayAdFree.Core.Services;

namespace PrayAdFree.Core.Models;

public sealed class WebState {
    public string Language { get; set; } = "en";
    public string ThemeMode { get; set; } = "system";
    public string AccentColor { get; set; } = "teal";
    public int TextSize { get; set; } = 100;
    public bool OnboardingCompleted { get; set; }
    public bool UseGps { get; set; }
    public string Country { get; set; } = "";
    public string CountryCode { get; set; } = "";
    public string City { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string TimeZoneId { get; set; } = "";
    public DateTime SelectedMonth { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public double Heading { get; set; }
    public double ManualHeading { get; set; }
    public string HeadingMode { get; set; } = "auto";
    public string ReadingMode { get; set; } = "compass";
    public string FilterMode { get; set; } = "none";
    public string ClockFormat { get; set; } = "24h";
    public string AdhanSettingsJson { get; set; } = "";
    public string NotificationSettingsJson { get; set; } = "";
    public string AlarmRemindersSettingsJson { get; set; } = "";
    public string RemoteWebUrl { get; set; } = "";
    public int TasbihCount { get; set; }
    public string SelectedTasbihPresetId { get; set; } = "";
    public List<WebTasbihPreset> TasbihPresets { get; set; } = new();

    public static WebState Default() => WebStateDefaults.Build();

    public void EnsureDefaults() {
        WebStateDefaults.ApplyDefaults(this);
    }

    public void ValidatePersisted() {
        _ = WebCatalog.NormalizeLanguage(Language);
        _ = WebCatalog.NormalizeTheme(ThemeMode);
        _ = WebCatalog.NormalizeAccent(AccentColor);
        _ = WebCatalog.ClampTextSize(TextSize);
        _ = AppInputContract.RequiredChoice(HeadingMode, nameof(HeadingMode), "auto", "manual");
        _ = AppInputContract.RequiredChoice(ReadingMode, nameof(ReadingMode), "compass", "map");
        _ = AppInputContract.RequiredChoice(FilterMode, nameof(FilterMode), "none", "night", "contrast");
        _ = AppInputContract.RequiredChoice(ClockFormat, nameof(ClockFormat), "auto", "12h", "24h");
        if (!double.IsFinite(Latitude) || !double.IsFinite(Longitude) || Math.Abs(Latitude) > 90 || Math.Abs(Longitude) > 180) {
            throw new InvalidDataException("Persisted location coordinates are invalid.");
        }
        if (string.IsNullOrWhiteSpace(TimeZoneId)) {
            throw new InvalidDataException("Persisted location time-zone ID is missing.");
        }
        if (!Uri.TryCreate(RemoteWebUrl, UriKind.Absolute, out var remote) || remote.Scheme is not ("http" or "https")) {
            throw new InvalidDataException("Persisted remote web URL is invalid.");
        }
        if (TasbihPresets.Count == 0 || !TasbihPresets.Any(item => item.Id == SelectedTasbihPresetId)) {
            throw new InvalidDataException("Persisted Tasbih selection is invalid.");
        }
        foreach (var preset in TasbihPresets) {
            if (string.IsNullOrWhiteSpace(preset.Id) || string.IsNullOrWhiteSpace(preset.Name) || preset.Items.Count == 0) {
                throw new InvalidDataException("Persisted Tasbih preset is incomplete.");
            }
            _ = AppInputContract.RequiredChoice(preset.RepeatMode, nameof(preset.RepeatMode), "Continue", "Loop", "Reset", "Sequence", "None");
            if (preset.Items.Any(item => string.IsNullOrWhiteSpace(item.Text) || item.TargetCount <= 0)) {
                throw new InvalidDataException("Persisted Tasbih item is invalid.");
            }
        }
    }
}

public sealed record WebTasbihPreset(string Id, string Name, string RepeatMode, List<WebTasbihItem> Items);

public sealed record WebTasbihItem(string Text, int TargetCount);
