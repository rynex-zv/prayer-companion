namespace Pray_Ad_Free.Services;

public static class AutomationRuntime {
#if PRAY_AUTOMATION
    public const bool IsEnabled = true;
#else
    public const bool IsEnabled = false;
#endif

    public static string DataRoot => IsEnabled
        ? Path.Combine(FileSystem.AppDataDirectory, "AutomationState")
        : FileSystem.AppDataDirectory;

    public static string SettingsPath => IsEnabled
        ? Path.Combine(DataRoot, "app_settings.json")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrayAdFree", "app_settings.json");
}
