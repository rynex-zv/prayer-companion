namespace PrayAdFree.Core.Models;

public sealed class HijriDate {
    public string Day { get; init; } = "";
    public string Month { get; init; } = "";
    public string Year { get; init; } = "";
    public string Date => $"{Day} {Month} {Year}";
}
