namespace PrayAdFree.Core.Models;

public sealed class PrayerOffsets {
    public int Fajr { get; init; }
    public int Sunrise { get; init; }
    public int Dhuhr { get; init; }
    public int Asr { get; init; }
    public int Maghrib { get; init; }
    public int Isha { get; init; }
    public int Imsak { get; init; }

    public int GetOffset(PrayerId id) {
        return id switch {
            PrayerId.Fajr => Fajr,
            PrayerId.Sunrise => Sunrise,
            PrayerId.Dhuhr => Dhuhr,
            PrayerId.Asr => Asr,
            PrayerId.Maghrib => Maghrib,
            PrayerId.Isha => Isha,
            PrayerId.Imsak => Imsak,
            _ => 0
        };
    }

    public static PrayerOffsets Default => new PrayerOffsets();
}
