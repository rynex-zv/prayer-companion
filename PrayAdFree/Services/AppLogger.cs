using System.Diagnostics;
using System.Text;
using System.Collections.Concurrent;
using Microsoft.Maui.Storage;
#if ANDROID
using Android.Util;
#endif

namespace Pray_Ad_Free.Services;

public sealed class AppLogger : IAppLogger {
    private const string Tag = "PrayAdFree";
    private readonly string _exceptionPath;
    private readonly string _eventPath;
    private readonly object _lock = new();
    private readonly ConcurrentQueue<string> _eventQueue = new();
    private readonly Timer _eventFlushTimer;

    public AppLogger() {
        var root = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            : FileSystem.AppDataDirectory;
        var logRoot = Path.Combine(root, "PrayAdFreeLogs");
        Directory.CreateDirectory(logRoot);
        _exceptionPath = Path.Combine(logRoot, "PrayAdFree.log");
        _eventPath = Path.Combine(logRoot, "PrayAdFree-events.log");
        _eventQueue.Enqueue($"{Environment.NewLine}===== PrayAdFree session UTC {DateTime.UtcNow:O} ====={Environment.NewLine}");
        _eventFlushTimer = new Timer(_ => FlushEvents(), null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        AppDomain.CurrentDomain.ProcessExit += (_, _) => FlushEvents();
    }

    public void LogException(Exception exception, string context) {
        try {
            var builder = new StringBuilder();
            builder.AppendLine("-----");
            builder.AppendLine($"UTC: {DateTime.UtcNow:O}");
            builder.AppendLine($"Context: {context}");
            builder.AppendLine(exception.ToString());
            var payload = builder.ToString();
            Append(_exceptionPath, payload);
            WritePlatformLog(payload, isError: true);
        } catch {
        }
    }

    public void LogEvent(string name, string details) {
        try {
            var line = $"UTC: {DateTime.UtcNow:O} | {name} | {details}";
            _eventQueue.Enqueue(line + Environment.NewLine);
            WritePlatformLog(line, isError: false);
        } catch {
        }
    }

    private void Append(string path, string text) {
        lock (_lock) {
            File.AppendAllText(path, text);
        }
    }

    private void FlushEvents() {
        if (_eventQueue.IsEmpty) {
            return;
        }

        try {
            var batch = new StringBuilder();
            while (_eventQueue.TryDequeue(out var entry)) {
                batch.Append(entry);
            }

            if (batch.Length > 0) {
                Append(_eventPath, batch.ToString());
            }
        } catch {
            // Logging must never block or crash an application data call.
        }
    }

    private static void WritePlatformLog(string message, bool isError) {
        try {
            Debug.WriteLine(message);
#if ANDROID
            if (isError) {
                Log.Error(Tag, message);
            } else {
                Log.Debug(Tag, message);
            }
#endif
        } catch {
        }
    }
}
