using System.Text.RegularExpressions;
using Microsoft.Maui.Storage;
using Pray_Ad_Free.Models;
using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Services;

public static class AdhanSoundLibrary {
    public const string DefaultBuiltinKey = "adhan_builtin_01";
    public const string UseGlobalKey = "use_global";

    private static readonly BuiltinSound[] BuiltinSounds = {
        new("adhan_builtin_01", "adhan_builtin_01.mp3", "Sound_Builtin_1"),
        new("adhan_builtin_02", "adhan_builtin_02.mp3", "Sound_Builtin_2"),
        new("adhan_builtin_03", "adhan_builtin_03.mp3", "Sound_Builtin_3"),
        new("adhan_builtin_04", "adhan_builtin_04.mp3", "Sound_Builtin_4"),
        new("adhan_builtin_05", "adhan_builtin_05.mp3", "Sound_Builtin_5"),
        new("adhan_builtin_06", "adhan_builtin_06.mp3", "Sound_Builtin_6"),
        new("adhan_builtin_07", "adhan_builtin_07.mp3", "Sound_Builtin_7"),
        new("adhan_builtin_08", "adhan_builtin_08.mp3", "Sound_Builtin_8"),
        new("adhan_builtin_09", "adhan_builtin_09.mp3", "Sound_Builtin_9")
    };

    public static IReadOnlyList<OptionItem<string>> BuildOptions(NotificationSettings settings, bool includeUseGlobal) {
        var list = new List<OptionItem<string>>();
        if (includeUseGlobal) {
            list.Add(new OptionItem<string>("use_global", LocalizationManager.Translate("UseGlobal")));
        }

        list.Add(new OptionItem<string>("adhan_default", LocalizationManager.Translate("Sound_Default")));
        list.Add(new OptionItem<string>("adhan_silent", LocalizationManager.Translate("Sound_Silent")));

        foreach (var builtin in BuiltinSounds) {
            list.Add(new OptionItem<string>(builtin.Key, LocalizationManager.Translate(builtin.LabelKey)));
        }

        if (settings.CustomSounds != null) {
            foreach (var custom in settings.CustomSounds.Where(IsValidCustomSound)) {
                list.Add(new OptionItem<string>(custom.Key, custom.Name));
            }
        }

        return list;
    }

    public static string? ResolveNotificationSound(NotificationSettings settings, string? soundKey) {
        if (OperatingSystem.IsWindows()) {
            return null;
        }
        if (string.Equals(soundKey, "adhan_silent", StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        if (string.IsNullOrWhiteSpace(soundKey) ||
            string.Equals(soundKey, "adhan_default", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(soundKey, UseGlobalKey, StringComparison.OrdinalIgnoreCase)) {
            var fallback = BuiltinSounds.FirstOrDefault(item =>
                item.Key.Equals(DefaultBuiltinKey, StringComparison.OrdinalIgnoreCase));
            return fallback?.FileName;
        }

        var builtin = BuiltinSounds.FirstOrDefault(item => item.Key.Equals(soundKey, StringComparison.OrdinalIgnoreCase));
        if (builtin != null) {
            return builtin.FileName;
        }

        if (settings.CustomSounds == null) {
            return null;
        }

        var custom = settings.CustomSounds.FirstOrDefault(item =>
            item.Key.Equals(soundKey, StringComparison.OrdinalIgnoreCase) && IsValidCustomSound(item));
        if (custom == null) {
            return null;
        }

        var fullPath = Path.Combine(GetCustomSoundsDirectory(), custom.FileName);
        return File.Exists(fullPath) ? fullPath : null;
    }

    public static bool IsSilent(string? soundKey) {
        return string.Equals(soundKey, "adhan_silent", StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolveEffectiveSoundKey(string? soundKey) {
        if (string.IsNullOrWhiteSpace(soundKey) ||
            string.Equals(soundKey, "adhan_default", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(soundKey, UseGlobalKey, StringComparison.OrdinalIgnoreCase)) {
            return DefaultBuiltinKey;
        }

        return soundKey.Trim();
    }

    public static string ResolvePrayerEffectiveSoundKey(NotificationSettings settings, string? prayerOverrideSoundKey) {
        if (string.IsNullOrWhiteSpace(prayerOverrideSoundKey) ||
            string.Equals(prayerOverrideSoundKey, UseGlobalKey, StringComparison.OrdinalIgnoreCase)) {
            return ResolveEffectiveSoundKey(settings.SoundKey);
        }

        return ResolveEffectiveSoundKey(prayerOverrideSoundKey);
    }

    public static AdhanPlaybackSource? ResolvePlaybackSource(NotificationSettings settings, string? soundKey) {
        var resolvedKey = ResolveEffectiveSoundKey(soundKey);
        if (IsSilent(resolvedKey)) {
            return null;
        }

        var builtin = BuiltinSounds.FirstOrDefault(item => item.Key.Equals(resolvedKey, StringComparison.OrdinalIgnoreCase));
        if (builtin != null) {
            return new AdhanPlaybackSource(Path.Combine("AdhanBuiltIn", builtin.FileName), IsPackageAsset: true);
        }

        if (settings.CustomSounds == null) {
            return null;
        }

        var custom = settings.CustomSounds.FirstOrDefault(item =>
            item.Key.Equals(resolvedKey, StringComparison.OrdinalIgnoreCase) && IsValidCustomSound(item));
        if (custom == null) {
            return null;
        }

        var fullPath = Path.Combine(GetCustomSoundsDirectory(), custom.FileName);
        return File.Exists(fullPath) ? new AdhanPlaybackSource(fullPath, IsPackageAsset: false) : null;
    }

    public static string BuildChannelId(string? soundKey) {
        if (string.IsNullOrWhiteSpace(soundKey)) {
            return "prayer_times_default";
        }

        var normalized = Regex.Replace(soundKey.Trim().ToLowerInvariant(), "[^a-z0-9_]+", "_");
        if (string.IsNullOrWhiteSpace(normalized)) {
            normalized = "default";
        }

        if (normalized.Length > 42) {
            normalized = normalized[..42];
        }

        return $"prayer_{normalized}";
    }

    public static bool IsCustomSound(NotificationSettings settings, string? soundKey) {
        if (string.IsNullOrWhiteSpace(soundKey)) {
            return false;
        }

        if (string.Equals(soundKey, "adhan_default", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(soundKey, "adhan_silent", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(soundKey, UseGlobalKey, StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        var builtin = BuiltinSounds.Any(item => item.Key.Equals(soundKey, StringComparison.OrdinalIgnoreCase));
        if (builtin) {
            return false;
        }

        return settings.CustomSounds != null &&
               settings.CustomSounds.Any(item =>
                   item.Key.Equals(soundKey, StringComparison.OrdinalIgnoreCase) &&
                   IsValidCustomSound(item));
    }

    public static string GetCustomSoundsDirectory() {
        return Path.Combine(FileSystem.AppDataDirectory, "AdhanSounds");
    }

    private static bool IsValidCustomSound(CustomAdhanSound item) {
        return !string.IsNullOrWhiteSpace(item.Key) &&
               !string.IsNullOrWhiteSpace(item.Name) &&
               !string.IsNullOrWhiteSpace(item.FileName);
    }

    private sealed record BuiltinSound(string Key, string FileName, string LabelKey);
}

public sealed record AdhanPlaybackSource(string Path, bool IsPackageAsset);

