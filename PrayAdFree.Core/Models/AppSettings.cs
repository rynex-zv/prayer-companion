namespace PrayAdFree.Core.Models;

public sealed class AppSettings {
    public LocationSettings Location { get; init; } = new LocationSettings();
    public CalculationMethod Method { get; init; } = CalculationMethod.Auto;
    public Madhhab Madhhab { get; init; } = Madhhab.Shafi;
    public HighLatitudeRule HighLatitudeRule { get; init; } = HighLatitudeRule.MiddleOfTheNight;
    public PrayerOffsets Offsets { get; init; } = PrayerOffsets.Default;
    public FastingOffsets FastingOffsets { get; init; } = FastingOffsets.Default;
    public FastingReminderSettings FastingReminders { get; init; } = FastingReminderSettings.Default;
    public NotificationSettings Notifications { get; init; } = new NotificationSettings();
    public ClockFormat ClockFormat { get; init; } = ClockFormat.Auto;
    public int TextScale { get; init; }
    public string Language { get; init; } = "auto";
    public bool LanguageSelected { get; init; }
    public ThemeMode ThemeMode { get; init; } = ThemeMode.Auto;
    public ThemeVariant ThemeVariant { get; init; } = ThemeVariant.A;
    public int AccentIndex { get; init; }
}
