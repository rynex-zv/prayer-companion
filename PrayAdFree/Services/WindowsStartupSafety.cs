using System.Text;

namespace Pray_Ad_Free.Services;

public static class WindowsStartupSafety {
    private static readonly object Sync = new();
    private static readonly string LogRoot = BuildLogRoot();
    private static readonly string TracePath = Path.Combine(LogRoot, "startup-trace.log");
    private static readonly string StartupPendingMarkerPath = Path.Combine(LogRoot, "startup.pending");

    private static volatile bool _windowsSafeStartupMode;

    public static bool IsWindowsSafeStartupMode => _windowsSafeStartupMode;

    public static bool ArmStartupPendingMarker() {
        if (!OperatingSystem.IsWindows()) {
            return false;
        }

        bool safeMode;
        try {
            Directory.CreateDirectory(LogRoot);
            safeMode = File.Exists(StartupPendingMarkerPath);
            File.WriteAllText(
                StartupPendingMarkerPath,
                $"UTC={DateTime.UtcNow:O};SafeMode={safeMode}",
                Encoding.UTF8);
        } catch {
            safeMode = false;
        }

        _windowsSafeStartupMode = safeMode;
        Trace($"Safety.MarkerArmed:safeMode={safeMode}");
        return safeMode;
    }

    public static void MarkStartupStable() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }

        try {
            if (File.Exists(StartupPendingMarkerPath)) {
                File.Delete(StartupPendingMarkerPath);
            }
            Trace("Safety.MarkerCleared");
        } catch (Exception ex) {
            Trace($"Safety.MarkerClearFailed:{ex.GetType().Name}:{ex.Message}");
        }
    }

    public static void Trace(string message) {
        if (!OperatingSystem.IsWindows()) {
            return;
        }

        try {
            var line = $"{DateTime.UtcNow:O} | T{Environment.CurrentManagedThreadId} | {message}";
            lock (Sync) {
                Directory.CreateDirectory(LogRoot);
                File.AppendAllText(TracePath, line + Environment.NewLine, Encoding.UTF8);
            }
        } catch {
        }
    }

    private static string BuildLogRoot() {
        try {
            if (OperatingSystem.IsWindows()) {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var dir = Path.Combine(desktop, "PrayAdFreeLogs");
                Directory.CreateDirectory(dir);
                return dir;
            }
        } catch {
        }

        return Path.Combine(Path.GetTempPath(), "PrayAdFreeLogs");
    }
}
