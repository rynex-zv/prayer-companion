namespace PrayAdFree.Core.Models;

public sealed class WidgetSnapshotResult {
    public DailyPrayerSnapshot DailyPrayer { get; init; } = new();
    public FastingSnapshot Fasting { get; init; } = new();
}
