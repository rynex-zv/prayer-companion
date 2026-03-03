namespace PrayAdFree.Core.Models;

public sealed class FastingOffsets {
    public int ImsakAdvanceMinutes { get; init; }
    public int IftarDelayMinutes { get; init; }

    public static FastingOffsets Default => new FastingOffsets();
}
