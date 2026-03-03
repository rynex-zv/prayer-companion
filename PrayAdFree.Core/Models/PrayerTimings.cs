using System.Globalization;

namespace PrayAdFree.Core.Models;

public sealed class PrayerTimings {
    public DateTime Fajr { get; init; }
    public DateTime Sunrise { get; init; }
    public DateTime Dhuhr { get; init; }
    public DateTime Asr { get; init; }
    public DateTime Maghrib { get; init; }
    public DateTime Isha { get; init; }
    public DateTime Imsak { get; init; }

    public DateTime Get(PrayerId id) {
        return id switch {
            PrayerId.Fajr => Fajr,
            PrayerId.Sunrise => Sunrise,
            PrayerId.Dhuhr => Dhuhr,
            PrayerId.Asr => Asr,
            PrayerId.Maghrib => Maghrib,
            PrayerId.Isha => Isha,
            PrayerId.Imsak => Imsak,
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown prayer id.")
        };
    }

    public static DateTime ParseLocalDateTime(DateOnly date, string time24h, TimeZoneInfo timeZone) {
        var parsed = DateTime.ParseExact(time24h, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None);
        var local = new DateTime(date.Year, date.Month, date.Day, parsed.Hour, parsed.Minute, 0, DateTimeKind.Unspecified);
        var offset = timeZone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToLocalTime().DateTime;
    }
}
