using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Models;

public sealed class PrayerTimeRow {
    public PrayerId Id { get; init; }
    public string Name { get; init; } = "";
    public string Time { get; init; } = "";
    public string BaseTime { get; init; } = "";
    public bool ShowBaseTime { get; init; }
    public bool IsNext { get; init; }
}
