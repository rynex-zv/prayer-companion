namespace PrayAdFree.Core.Services;

public interface ISettingsStore {
    T Get<T>(string key, T defaultValue);
    void Set<T>(string key, T value);
}
