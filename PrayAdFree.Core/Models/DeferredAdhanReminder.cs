namespace PrayAdFree.Core.Models;

public sealed class DeferredAdhanReminder {
    public DateTime NotifyTime { get; init; }
    public DateTime BasePrayerTime { get; init; }
    public PrayerId Prayer { get; init; } = PrayerId.Fajr;
    public string SoundKey { get; init; } = "adhan_default";
    public bool OpenAlarmScreen { get; init; }
}
