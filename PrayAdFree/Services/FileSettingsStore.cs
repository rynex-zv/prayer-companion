using System.IO;
using PrayAdFree.Core.Services;

namespace Pray_Ad_Free.Services;

public sealed class FileSettingsStore : ISettingsStore {
    private readonly string _path;

    public FileSettingsStore(string path) {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
    }

    public T Get<T>(string key, T defaultValue) {
        if (typeof(T) != typeof(string)) {
            throw new NotSupportedException("Only string settings are supported.");
        }

        try {
            if (!File.Exists(_path)) {
                return defaultValue;
            }

            var value = File.ReadAllText(_path);
            return (T)(object)(value ?? "");
        } catch {
            return defaultValue;
        }
    }

    public void Set<T>(string key, T value) {
        if (value is not string text) {
            throw new NotSupportedException("Only string settings are supported.");
        }

        var tempPath = _path + ".tmp";
        try {
            File.WriteAllText(tempPath, text);
            if (File.Exists(_path)) {
                File.Delete(_path);
            }
            File.Move(tempPath, _path);
        } finally {
            if (File.Exists(tempPath)) {
                try {
                    File.Delete(tempPath);
                } catch {
                }
            }
        }
    }
}
