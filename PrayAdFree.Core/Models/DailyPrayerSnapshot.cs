using System.Collections.Generic;

namespace PrayAdFree.Core.Models;

public sealed class DailyPrayerSnapshot {
    public PrayerId NextPrayerId { get; init; }
    public DateTime NextPrayerTime { get; init; }
    public DateTime? NextPrayerBaseTime { get; init; }
    public bool IsNextPrayerTomorrow { get; init; }
    public IReadOnlyList<DailyPrayerSnapshotEntry> Entries { get; init; } = [];
}
