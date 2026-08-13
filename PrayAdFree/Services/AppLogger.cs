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
    private const long MaxLogBytes = 10 * 1024 * 1024;
    private const int PreservedTailBytes = 2 * 1024 * 1024;
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
            EnsureBounded(path);
            File.AppendAllText(path, text);
        }
    }

    private static void EnsureBounded(string path) {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length <= MaxLogBytes) return;

        byte[] tail;
        using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
            var bytesToKeep = (int)Math.Min(PreservedTailBytes, source.Length);
            tail = new byte[bytesToKeep];
            source.Seek(-bytesToKeep, SeekOrigin.End);
            source.ReadExactly(tail);
        }

        File.WriteAllBytes(path + ".previous", tail);
        File.WriteAllText(path, $"===== log rotated UTC {DateTime.UtcNow:O}; previous tail={tail.Length} bytes ====={Environment.NewLine}");
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
