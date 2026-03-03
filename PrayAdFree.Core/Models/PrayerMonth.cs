namespace PrayAdFree.Core.Models;

public sealed class PrayerMonth {
    public int Year { get; init; }
    public int Month { get; init; }
    public string LocationKey { get; init; } = "";
    public string MethodKey { get; init; } = "";
    public DateTime FetchedOnUtc { get; init; }
    public IReadOnlyList<PrayerDay> Days { get; init; } = Array.Empty<PrayerDay>();
}
