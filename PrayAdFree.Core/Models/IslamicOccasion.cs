namespace PrayAdFree.Core.Models;

public sealed class IslamicOccasion {
    public string Id { get; init; } = "";
    public int HijriMonth { get; init; }
    public int HijriDay { get; init; }
    public string LabelKey { get; init; } = "";
    public string Importance { get; init; } = "minor"; // major | minor
    public string Color { get; init; } = "primary";     // token name
    public string Source { get; init; } = "base";       // base | shafi | hanafi | maliki | hanbali
}
