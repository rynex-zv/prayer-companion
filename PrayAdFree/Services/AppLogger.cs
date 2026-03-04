using System.Text;
using Microsoft.Maui.Storage;

namespace Pray_Ad_Free.Services;

public sealed class AppLogger : IAppLogger {
    private readonly string _exceptionPath;
    private readonly string _eventPath;
    private readonly object _lock = new();

    public AppLogger() {
        var root = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            : FileSystem.AppDataDirectory;
        var logRoot = Path.Combine(root, "PrayAdFreeLogs");
        Directory.CreateDirectory(logRoot);
        _exceptionPath = Path.Combine(logRoot, "PrayAdFree.log");
        _eventPath = Path.Combine(logRoot, "PrayAdFree-events.log");
        ResetLogs();
    }

    public void LogException(Exception exception, string context) {
        try {
            var builder = new StringBuilder();
            builder.AppendLine("-----");
            builder.AppendLine($"UTC: {DateTime.UtcNow:O}");
            builder.AppendLine($"Context: {context}");
            builder.AppendLine(exception.ToString());
            Append(_exceptionPath, builder.ToString());
        } catch {
        }
    }

    public void LogEvent(string name, string details) {
#if DEBUG
        try {
            var line = $"UTC: {DateTime.UtcNow:O} | {name} | {details}";
            Append(_eventPath, line + Environment.NewLine);
        } catch {
        }
#endif
    }

    private void ResetLogs() {
        try {
            lock (_lock) {
                File.WriteAllText(_exceptionPath, string.Empty);
#if DEBUG
                File.WriteAllText(_eventPath, string.Empty);
#endif
            }
        } catch {
        }
    }

    private void Append(string path, string text) {
        lock (_lock) {
            File.AppendAllText(path, text);
        }
    }
}
