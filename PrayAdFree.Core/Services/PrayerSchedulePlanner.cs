using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public sealed class PrayerSchedulePlanner {
    public IReadOnlyList<PrayerNotification> BuildSchedule(PrayerDay day, NotificationSettings settings) {
        if (!settings.EnableAdhan) {
            return Array.Empty<PrayerNotification>();
        }

        var list = new List<PrayerNotification>();
        foreach (var prayer in Enum.GetValues<PrayerId>()) {
            if (prayer == PrayerId.Imsak || prayer == PrayerId.Sunrise) {
                continue;
            }

            var time = day.Timings.Get(prayer);
            list.Add(new PrayerNotification(prayer, time));
        }

        return list;
    }
}

public sealed record PrayerNotification(PrayerId Prayer, DateTime Time);
