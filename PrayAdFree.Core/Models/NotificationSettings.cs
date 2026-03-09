using System.Collections.Generic;

namespace PrayAdFree.Core.Models;

public sealed class NotificationSettings {
    public bool EnableAdhan { get; init; } = true;
    public bool EnableVibration { get; init; } = true;
    public bool HideOnCloseOnWindows { get; init; }
    public bool RunBackgroundServiceOnWindows { get; init; }
    public int MinutesBefore { get; init; }
    public double AdhanVolume { get; init; } = 1.0;
    public string SoundKey { get; init; } = "adhan_default";
    public IReadOnlyList<CustomAdhanSound> CustomSounds { get; init; } = new List<CustomAdhanSound>();
    public IReadOnlyList<AdhanPrayerOverride> PrayerOverrides { get; init; } = new List<AdhanPrayerOverride>();
    public VibrationStrength VibrationStrength { get; init; } = VibrationStrength.Medium;
    public VibrationPattern VibrationPattern { get; init; } = VibrationPattern.Short;
    public AdhanReminderScope ReminderScope { get; init; } = AdhanReminderScope.All;
    public PrayerId ReminderPrayer { get; init; } = PrayerId.Fajr;
    public List<int> ReminderOffsetsMinutes { get; init; } = new();
}
