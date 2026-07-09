using PrayAdFree.Core.Services;

namespace PrayAdFree.Core.Models;

public sealed class WebState {
    public string Language { get; set; } = "en";
    public string ThemeMode { get; set; } = "system";
    public string AccentColor { get; set; } = "teal";
    public int TextSize { get; set; } = 100;
    public bool OnboardingCompleted { get; set; } = true;
    public bool UseGps { get; set; }
    public string Country { get; set; } = "";
    public string CountryCode { get; set; } = "";
    public string City { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime SelectedMonth { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public double Heading { get; set; }
    public double ManualHeading { get; set; }
    public string HeadingMode { get; set; } = "auto";
    public string ReadingMode { get; set; } = "compass";
    public string FilterMode { get; set; } = "none";
    public string ClockFormat { get; set; } = "24h";
    public string RemoteWebUrl { get; set; } = "";
    public int TasbihCount { get; set; }
    public string SelectedTasbihPresetId { get; set; } = "";
    public List<WebTasbihPreset> TasbihPresets { get; set; } = new();

    public static WebState Default() => WebStateDefaults.Build();

    public void EnsureDefaults() {
        WebStateDefaults.ApplyDefaults(this);
    }
}

public sealed record WebTasbihPreset(string Id, string Name, string RepeatMode, List<WebTasbihItem> Items);

public sealed record WebTasbihItem(string Text, int TargetCount);
