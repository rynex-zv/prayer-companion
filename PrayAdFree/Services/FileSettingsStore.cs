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

        if (!File.Exists(_path)) {
            return defaultValue;
        }

        var value = File.ReadAllText(_path);
        return (T)(object)(value ?? "");
    }

    public void Set<T>(string key, T value) {
        if (value is not string text) {
            throw new NotSupportedException("Only string settings are supported.");
        }

        File.WriteAllText(_path, text);
    }
}
