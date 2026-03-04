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
        if (string.Equals(storedVersion, currentVersion, StringComparison.Ordinal) && !ArabicFileLooksCorrupted(targetDir)) {
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
