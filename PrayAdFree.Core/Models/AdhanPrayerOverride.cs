namespace PrayAdFree.Core.Models;

public sealed class AdhanPrayerOverride {
    public PrayerId Prayer { get; init; }
    public string? SoundKey { get; init; }
    public bool? EnableVibration { get; init; }
}
