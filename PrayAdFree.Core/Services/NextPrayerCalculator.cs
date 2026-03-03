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
        foreach (var prayer in Ordered) {
            var time = day.Timings.Get(prayer);
            if (time > now) {
                return (prayer, time);
            }
        }

        return (PrayerId.Fajr, day.Timings.Fajr.AddDays(1));
    }
}
