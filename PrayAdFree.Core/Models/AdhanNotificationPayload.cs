using System;

namespace PrayAdFree.Core.Models;

public readonly record struct AdhanNotificationPayload(PrayerId Prayer, string SoundKey) {
    private const string Prefix = "play";

    public static string BuildPlay(PrayerId prayer, string soundKey) {
        var safeSoundKey = string.IsNullOrWhiteSpace(soundKey) ? "adhan_default" : soundKey.Trim();
        return $"{Prefix}|{(int)prayer}|{Uri.EscapeDataString(safeSoundKey)}";
    }

    public static bool TryParse(string? value, out AdhanNotificationPayload payload) {
        payload = default;
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        var parts = value.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || !string.Equals(parts[0], Prefix, StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        if (!int.TryParse(parts[1], out var prayerRaw) || !Enum.IsDefined(typeof(PrayerId), prayerRaw)) {
            return false;
        }

        var soundKey = Uri.UnescapeDataString(parts[2]);
        if (string.IsNullOrWhiteSpace(soundKey)) {
            return false;
        }

        payload = new AdhanNotificationPayload((PrayerId)prayerRaw, soundKey);
        return true;
    }
}
