namespace PrayAdFree.Core.Models;

public sealed class NotificationSettings {
    public bool EnableAdhan { get; init; } = true;
    public bool EnableVibration { get; init; } = true;
    public int MinutesBefore { get; init; }
    public string SoundKey { get; init; } = "adhan_makkah";
}
