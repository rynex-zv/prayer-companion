namespace Pray_Ad_Free.Services;

public static class AutomationRuntime {
#if DEBUG && PRAY_AUTOMATION
    private const bool CompiledForAutomation = true;
#else
    private const bool CompiledForAutomation = false;
#endif

    public static bool TestsEnabled { get; set; } = false;

    public static bool IsEnabled => CompiledForAutomation && TestsEnabled;

    public static string DataRoot => IsEnabled
        ? Path.Combine(FileSystem.AppDataDirectory, "AutomationState")
        : FileSystem.AppDataDirectory;

    public static string SettingsPath => IsEnabled
        ? Path.Combine(DataRoot, "app_settings.json")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrayAdFree", "app_settings.json");
}
