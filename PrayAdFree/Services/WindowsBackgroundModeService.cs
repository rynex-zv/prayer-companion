using System.Diagnostics;
#if WINDOWS
using Microsoft.Win32;
#endif

namespace Pray_Ad_Free.Services;

public sealed class WindowsBackgroundModeService : IWindowsBackgroundModeService {
    public const string BackgroundArgument = "--background";

#if WINDOWS
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "PrayAdFreeBackground";
#endif

    public bool IsSupported => OperatingSystem.IsWindows();

    public bool IsEnabled() {
#if WINDOWS
        try {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            var raw = key?.GetValue(RunValueName) as string;
            return !string.IsNullOrWhiteSpace(raw) &&
                   raw.Contains(BackgroundArgument, StringComparison.OrdinalIgnoreCase);
        } catch {
            return false;
        }
#else
        return false;
#endif
    }

    public bool SetEnabled(bool enabled) {
#if WINDOWS
        try {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
            if (key == null) {
                return false;
            }

            if (enabled) {
                var command = BuildStartupCommand();
                if (string.IsNullOrWhiteSpace(command)) {
                    return false;
                }

                key.SetValue(RunValueName, command, RegistryValueKind.String);
            } else {
                key.DeleteValue(RunValueName, false);
            }

            return IsEnabled() == enabled;
        } catch {
            return false;
        }
#else
        return false;
#endif
    }

    public static bool IsBackgroundLaunch(string? launchArguments) {
        if (string.IsNullOrWhiteSpace(launchArguments)) {
            return false;
        }

        return launchArguments
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(arg => string.Equals(arg, BackgroundArgument, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildStartupCommand() {
        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path)) {
            path = Process.GetCurrentProcess().MainModule?.FileName;
        }

        if (string.IsNullOrWhiteSpace(path)) {
            return string.Empty;
        }

        return $"\"{path}\" {BackgroundArgument}";
    }
}
