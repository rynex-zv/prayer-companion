namespace PrayAdFree.Core.Models;

public sealed class DailyPrayerSnapshotEntry {
    public PrayerId Prayer { get; init; }
    public DateTime AdjustedTime { get; init; }
    public DateTime BaseTime { get; init; }
    public bool ShowBaseTime { get; init; }
    public bool IsNext { get; init; }
}
