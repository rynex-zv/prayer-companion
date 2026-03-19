namespace PrayAdFree.Core.Models;

public sealed class FastingSnapshot {
    public DateTime ImsakTime { get; init; }
    public DateTime IftarTime { get; init; }
    public DateTime NextTargetTime { get; init; }
    public bool IsImsakNext { get; init; }
    public bool IsIftarNext { get; init; }
    public TimeSpan Remaining { get; init; }
}
