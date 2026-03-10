using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public static class NextPrayerCalculator {
    private static readonly PrayerId[] Ordered = {
        PrayerId.Fajr,
        PrayerId.Sunrise,
        PrayerId.Dhuhr,
        PrayerId.Asr,
        PrayerId.Maghrib,
        PrayerId.Isha
    };

    public static (PrayerId id, DateTime time) GetNext(PrayerDay day, DateTime now) {
        var candidates = Ordered
            .Select(prayer => (id: prayer, time: ToNextOccurrence(day.Timings.Get(prayer), now)))
            .OrderBy(item => item.time)
            .ToList();

        return candidates.Count > 0
            ? candidates[0]
            : (PrayerId.Fajr, ToNextOccurrence(day.Timings.Fajr, now));
    }

    private static DateTime ToNextOccurrence(DateTime time, DateTime now) {
        while (time <= now) {
            time = time.AddDays(1);
        }

        return time;
    }
}
