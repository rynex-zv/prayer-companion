namespace PrayAdFree.Core.Models;

public readonly record struct AdhanAlarmPayload(
    PrayerId Prayer,
    string SoundKey,
    DateTime BasePrayerTime,
    DateTime NotifyTime) {

    private const string Prefix = "alarm";

    public static string Build(
        PrayerId prayer,
        string soundKey,
        DateTime basePrayerTime,
        DateTime notifyTime) {
        var safeSoundKey = string.IsNullOrWhiteSpace(soundKey) ? "adhan_default" : soundKey.Trim();
        return string.Join('|',
            Prefix,
            (int)prayer,
            Uri.EscapeDataString(safeSoundKey),
            basePrayerTime.ToBinary(),
            notifyTime.ToBinary());
    }

    public static bool TryParse(string? value, out AdhanAlarmPayload payload) {
        payload = default;
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        var parts = value.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length != 5 || !string.Equals(parts[0], Prefix, StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        if (!int.TryParse(parts[1], out var prayerRaw) || !Enum.IsDefined(typeof(PrayerId), prayerRaw)) {
            return false;
        }

        var soundKey = Uri.UnescapeDataString(parts[2]);
        if (string.IsNullOrWhiteSpace(soundKey)) {
            return false;
        }

        if (!long.TryParse(parts[3], out var baseBinary) || !long.TryParse(parts[4], out var notifyBinary)) {
            return false;
        }

        DateTime basePrayerTime;
        DateTime notifyTime;
        try {
            basePrayerTime = DateTime.FromBinary(baseBinary);
            notifyTime = DateTime.FromBinary(notifyBinary);
        } catch {
            return false;
        }

        payload = new AdhanAlarmPayload((PrayerId)prayerRaw, soundKey, basePrayerTime, notifyTime);
        return true;
    }
}
