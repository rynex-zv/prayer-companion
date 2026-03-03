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
        if (string.Equals(storedVersion, currentVersion, StringComparison.Ordinal)) {
            return;
        }

        foreach (var file in Files) {
            var targetPath = Path.Combine(FileSystem.AppDataDirectory, file.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            var content = TryReadPackageText(file);
            if (content != null) {
                File.WriteAllText(targetPath, content);
            }
        }

        File.WriteAllText(versionPath, currentVersion);
    }

    private static string? TryReadPackageText(string relativePath) {
        try {
            using var stream = FileSystem.OpenAppPackageFileAsync(relativePath).GetAwaiter().GetResult();
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        } catch {
            var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            var candidates = new[] {
                Path.Combine(AppContext.BaseDirectory, normalized),
                Path.Combine(AppContext.BaseDirectory, "Resources", "Raw", normalized),
                Path.Combine(AppContext.BaseDirectory, "i18n", Path.GetFileName(normalized))
            };
            foreach (var candidate in candidates) {
                if (File.Exists(candidate)) {
                    return File.ReadAllText(candidate);
                }
            }
        }

        return null;
    }
}
