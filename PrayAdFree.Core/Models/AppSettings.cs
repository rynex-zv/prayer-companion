namespace PrayAdFree.Core.Models;

public sealed class AppSettings {
    public LocationSettings Location { get; init; } = new LocationSettings();
    public CalculationMethod Method { get; init; } = CalculationMethod.Auto;
    public Madhhab Madhhab { get; init; } = Madhhab.Shafi;
    public HighLatitudeRule HighLatitudeRule { get; init; } = HighLatitudeRule.MiddleOfTheNight;
    public SunAngleSettings SunAngles { get; init; } = new SunAngleSettings();
    public PrayerOffsets Offsets { get; init; } = PrayerOffsets.Default;
    public FastingOffsets FastingOffsets { get; init; } = FastingOffsets.Default;
    public FastingReminderSettings FastingReminders { get; init; } = FastingReminderSettings.Default;
    public NotificationSettings Notifications { get; init; } = new NotificationSettings();
    public AlarmRemindersSettings AlarmReminders { get; init; } = new AlarmRemindersSettings();
    public QiblaPreferences Qibla { get; init; } = new QiblaPreferences();
    public ClockFormat ClockFormat { get; init; } = ClockFormat.Auto;
    public int TextScale { get; init; }
    public TasbihSettings Tasbih { get; init; } = new TasbihSettings();
    public string Language { get; init; } = "auto";
    public bool LanguageSelected { get; init; }
    public ThemeMode ThemeMode { get; init; } = ThemeMode.Auto;
    public ThemeVariant ThemeVariant { get; init; } = ThemeVariant.B;
    public int AccentIndex { get; init; }
}
