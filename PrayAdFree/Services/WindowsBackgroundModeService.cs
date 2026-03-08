using System.Diagnostics;
#if WINDOWS
using Microsoft.Win32;
#endif

namespace Pray_Ad_Free.Services;

public sealed class WindowsBackgroundModeService : IWindowsBackgroundModeService {
    public const string BackgroundArgument = "--background";
    private static readonly string BackgroundPidPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PrayAdFree",
        "background.pid");

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
                StartBackgroundProcess();
            } else {
                key.DeleteValue(RunValueName, false);
                StopBackgroundProcess();
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

    public static void RegisterCurrentBackgroundProcess() {
#if WINDOWS
        try {
            var directory = Path.GetDirectoryName(BackgroundPidPath);
            if (string.IsNullOrWhiteSpace(directory)) {
                return;
            }

            Directory.CreateDirectory(directory);
            File.WriteAllText(BackgroundPidPath, Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        } catch {
        }
#endif
    }

    public static void UnregisterCurrentBackgroundProcess() {
#if WINDOWS
        try {
            if (!File.Exists(BackgroundPidPath)) {
                return;
            }

            var raw = File.ReadAllText(BackgroundPidPath).Trim();
            if (!int.TryParse(raw, out var pid)) {
                File.Delete(BackgroundPidPath);
                return;
            }

            if (pid == Environment.ProcessId) {
                File.Delete(BackgroundPidPath);
            }
        } catch {
        }
#endif
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

    private static void StartBackgroundProcess() {
#if WINDOWS
        try {
            if (TryGetBackgroundProcess(out _)) {
                return;
            }

            var currentPath = ResolveExecutablePath();
            if (string.IsNullOrWhiteSpace(currentPath)) {
                return;
            }

            var startInfo = new ProcessStartInfo {
                FileName = currentPath,
                Arguments = BackgroundArgument,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(currentPath) ?? string.Empty
            };

            _ = Process.Start(startInfo);
        } catch {
        }
#endif
    }

    private static void StopBackgroundProcess() {
#if WINDOWS
        try {
            if (TryGetBackgroundProcess(out var process) && process != null) {
                process.Kill(true);
                process.WaitForExit(3000);
            }

            if (File.Exists(BackgroundPidPath)) {
                File.Delete(BackgroundPidPath);
            }
        } catch {
        }
#endif
    }

    private static string ResolveExecutablePath() {
        var path = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(path)) {
            return path;
        }

        return Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
    }

    private static bool TryGetBackgroundProcess(out Process? process) {
        process = null;
        try {
            if (!File.Exists(BackgroundPidPath)) {
                return false;
            }

            var raw = File.ReadAllText(BackgroundPidPath).Trim();
            if (!int.TryParse(raw, out var pid) || pid <= 0) {
                File.Delete(BackgroundPidPath);
                return false;
            }

            var candidate = Process.GetProcessById(pid);
            if (candidate.HasExited || candidate.Id == Environment.ProcessId) {
                File.Delete(BackgroundPidPath);
                return false;
            }

            process = candidate;
            return true;
        } catch {
            return false;
        }
    }
}
