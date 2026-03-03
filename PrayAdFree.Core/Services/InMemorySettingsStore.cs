namespace PrayAdFree.Core.Services;

public sealed class InMemorySettingsStore : ISettingsStore {
    private readonly Dictionary<string, object> _values = new();

    public T Get<T>(string key, T defaultValue) {
        return _values.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;
    }

    public void Set<T>(string key, T value) {
        _values[key] = value ?? throw new ArgumentNullException(nameof(value));
    }
}
