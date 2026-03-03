namespace PrayAdFree.Core.Models;

public sealed class PrayerDay {
    public DateOnly Date { get; init; }
    public PrayerTimings Timings { get; init; } = new PrayerTimings();
    public HijriDate Hijri { get; init; } = new HijriDate();
    public string TimeZoneId { get; init; } = "";
}
