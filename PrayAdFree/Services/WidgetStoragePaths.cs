namespace Pray_Ad_Free.Services;

public static class WidgetStoragePaths {
    public static string ProfilePath {
        get {
            if (!OperatingSystem.IsWindows()) return Path.Combine(AutomationRuntime.DataRoot, "widget_profiles.json");
            var shared = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PrayAdFree",
                "widget_profiles.json");
            MigrateLegacyWindowsFile(shared);
            return shared;
        }
    }

    private static void MigrateLegacyWindowsFile(string shared) {
        var legacy = Path.Combine(AutomationRuntime.DataRoot, "widget_profiles.json");
        if (string.Equals(Path.GetFullPath(legacy), Path.GetFullPath(shared), StringComparison.OrdinalIgnoreCase) ||
            File.Exists(shared) || !File.Exists(legacy)) return;

        Directory.CreateDirectory(Path.GetDirectoryName(shared)!);
        var temporary = shared + ".migration.tmp";
        File.Copy(legacy, temporary, true);
        File.Move(temporary, shared, false);
    }
}
