using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace Pray_Ad_Free.Services;

public sealed class LocalizationFileSync {
    private static readonly string[] Files = {
        "i18n/index.json",
        "i18n/en.json",
        "i18n/ar.json",
        "i18n/fr.json",
        "i18n/tr.json",
        "i18n/es.json"
    };

    public void SyncIfNeeded() {
        var targetDir = Path.Combine(FileSystem.AppDataDirectory, "i18n");
        Directory.CreateDirectory(targetDir);

        var versionPath = Path.Combine(targetDir, "version.txt");
        var currentVersion = AppInfo.Current.VersionString;
        var storedVersion = File.Exists(versionPath) ? File.ReadAllText(versionPath) : "";
        if (string.Equals(storedVersion, currentVersion, StringComparison.Ordinal)
            && !ArabicFileLooksCorrupted(targetDir)
            && !HasMissingKeys(targetDir)
            && !HasCorruptedValues(targetDir)) {
            return;
        }

        foreach (var file in Files) {
            var targetPath = Path.Combine(FileSystem.AppDataDirectory, file.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            var bytes = TryReadPackageBytes(file);
            if (bytes != null) {
                File.WriteAllBytes(targetPath, bytes);
            }
        }

        File.WriteAllText(versionPath, currentVersion);
    }

    private static bool ArabicFileLooksCorrupted(string targetDir) {
        try {
            var path = Path.Combine(targetDir, "ar.json");
            if (!File.Exists(path)) {
                return true;
            }

            var text = File.ReadAllText(path);
            return text.Contains("???", StringComparison.Ordinal);
        } catch {
            return true;
        }
    }

    private static bool HasMissingKeys(string targetDir) {
        try {
            return HasMissingKeys(targetDir, "en.json") || HasMissingKeys(targetDir, "ar.json");
        } catch {
            return true;
        }
    }

    private static bool HasMissingKeys(string targetDir, string fileName) {
        var packageBytes = TryReadPackageBytes($"i18n/{fileName}");
        if (packageBytes == null) {
            return false;
        }

        var targetPath = Path.Combine(targetDir, fileName);
        if (!File.Exists(targetPath)) {
            return true;
        }

        var packageData = Deserialize(packageBytes);
        var targetData = Deserialize(File.ReadAllBytes(targetPath));
        if (packageData == null || targetData == null) {
            return true;
        }

        return packageData.Keys.Except(targetData.Keys, StringComparer.OrdinalIgnoreCase).Any();
    }

    private static Dictionary<string, string>? Deserialize(byte[] bytes) {
        try {
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(text);
        } catch {
            return null;
        }
    }

    private static bool HasCorruptedValues(string targetDir) {
        try {
            return HasCorruptedValues(targetDir, "en.json") || HasCorruptedValues(targetDir, "ar.json");
        } catch {
            return true;
        }
    }

    private static bool HasCorruptedValues(string targetDir, string fileName) {
        var packageBytes = TryReadPackageBytes($"i18n/{fileName}");
        if (packageBytes == null) {
            return false;
        }

        var targetPath = Path.Combine(targetDir, fileName);
        if (!File.Exists(targetPath)) {
            return true;
        }

        var packageData = Deserialize(packageBytes);
        var targetData = Deserialize(File.ReadAllBytes(targetPath));
        if (packageData == null || targetData == null) {
            return true;
        }

        foreach (var entry in packageData) {
            if (!targetData.TryGetValue(entry.Key, out var value)) {
                continue;
            }

            if (string.Equals(value, entry.Key, StringComparison.Ordinal)
                && !string.Equals(entry.Value, entry.Key, StringComparison.Ordinal)) {
                return true;
            }
        }

        return false;
    }

    private static byte[]? TryReadPackageBytes(string relativePath) {
        try {
            using var stream = FileSystem.OpenAppPackageFileAsync(relativePath).GetAwaiter().GetResult();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        } catch {
            var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            var candidates = new[] {
                Path.Combine(AppContext.BaseDirectory, normalized),
                Path.Combine(AppContext.BaseDirectory, "Resources", "Raw", normalized),
                Path.Combine(AppContext.BaseDirectory, "i18n", Path.GetFileName(normalized))
            };
            foreach (var candidate in candidates) {
                if (File.Exists(candidate)) {
                    return File.ReadAllBytes(candidate);
                }
            }
        }

        return null;
    }
}
