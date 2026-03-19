using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public sealed class WidgetSnapshotFactory {
    private readonly DailyPrayerSnapshotFactory _dailyPrayerSnapshotFactory = new();
    private readonly FastingSnapshotFactory _fastingSnapshotFactory = new();

    public WidgetSnapshotResult Build(PrayerDay today, PrayerDay? tomorrow, AppSettings settings, DateTime now) {
        ArgumentNullException.ThrowIfNull(today);
        ArgumentNullException.ThrowIfNull(settings);

        return new WidgetSnapshotResult {
            DailyPrayer = _dailyPrayerSnapshotFactory.Build(today, settings, now),
            Fasting = _fastingSnapshotFactory.Build(today, tomorrow, settings, now)
        };
    }
}
