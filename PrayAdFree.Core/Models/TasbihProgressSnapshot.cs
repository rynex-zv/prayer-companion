namespace PrayAdFree.Core.Models;

public sealed class TasbihProgressSnapshot {
    public string CurrentText { get; init; } = "";
    public int LocalCount { get; init; }
    public int LocalTarget { get; init; }
    public int TotalTarget { get; init; }
    public bool IsEmpty { get; init; }
}
