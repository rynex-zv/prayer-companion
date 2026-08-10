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

        // Existing-but-unreadable settings are a release-significant data
        // failure. Do not disguise access/corruption errors as a clean install.
        var value = File.ReadAllText(_path);
        return (T)(object)(value ?? "");
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
            RpcObservability.RecordPersistenceWrite();
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
