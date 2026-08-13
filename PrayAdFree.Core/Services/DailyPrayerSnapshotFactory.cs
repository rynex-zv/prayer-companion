using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public sealed class DailyPrayerSnapshotFactory {
    private static readonly PrayerId[] DefaultDisplayOrder = [
        PrayerId.Fajr,
        PrayerId.Sunrise,
        PrayerId.Dhuhr,
        PrayerId.Asr,
        PrayerId.Maghrib,
        PrayerId.Isha
    ];

    public DailyPrayerSnapshot Build(PrayerDay day, AppSettings settings, DateTime now) {
        return Build(day, null, settings, now);
    }

    public DailyPrayerSnapshot Build(PrayerDay day, PrayerDay? tomorrow, AppSettings settings, DateTime now) {
        ArgumentNullException.ThrowIfNull(day);
        ArgumentNullException.ThrowIfNull(settings);

        var remainingToday = DefaultDisplayOrder
            .Select(prayer => (id: prayer, time: day.Timings.Get(prayer)))
            .Where(item => item.time > now)
            .OrderBy(item => item.time)
            .FirstOrDefault();
        var showTomorrow = remainingToday == default && tomorrow is not null;
        var displayDay = showTomorrow ? tomorrow! : day;
        var (nextPrayerId, nextPrayerTime) = showTomorrow
            ? (PrayerId.Fajr, tomorrow!.Timings.Fajr)
            : remainingToday == default
                ? NextPrayerCalculator.GetNext(day, now)
                : remainingToday;
        var nextOffset = GetOffsetForPrayer(settings, nextPrayerId);
        var entries = new List<DailyPrayerSnapshotEntry>(DefaultDisplayOrder.Length);

        foreach (var prayer in DefaultDisplayOrder) {
            var adjustedTime = displayDay.Timings.Get(prayer);
            var offset = GetOffsetForPrayer(settings, prayer);
            entries.Add(new DailyPrayerSnapshotEntry {
                Prayer = prayer,
                AdjustedTime = adjustedTime,
                BaseTime = adjustedTime.AddMinutes(-offset),
                ShowBaseTime = offset != 0,
                IsNext = prayer == nextPrayerId
            });
        }

        return new DailyPrayerSnapshot {
            NextPrayerId = nextPrayerId,
            NextPrayerTime = nextPrayerTime,
            NextPrayerBaseTime = nextOffset == 0 ? null : nextPrayerTime.AddMinutes(-nextOffset),
            IsNextPrayerTomorrow = showTomorrow || nextPrayerTime.Date > now.Date,
            Entries = entries
        };
    }

    private static int GetOffsetForPrayer(AppSettings settings, PrayerId prayer) {
        return prayer switch {
            PrayerId.Fajr => settings.Offsets.Fajr,
            PrayerId.Sunrise => settings.Offsets.Sunrise,
            PrayerId.Dhuhr => settings.Offsets.Dhuhr,
            PrayerId.Asr => settings.Offsets.Asr,
            PrayerId.Maghrib => settings.Offsets.Maghrib,
            PrayerId.Isha => settings.Offsets.Isha,
            PrayerId.Imsak => settings.Offsets.Imsak,
            _ => 0
        };
    }
}
