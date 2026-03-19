using System.Text.Json;
using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public sealed class TasbihWidgetStateStore {
    private readonly string _path;
    private readonly JsonSerializerOptions _jsonOptions = new() {
        WriteIndented = true
    };

    public TasbihWidgetStateStore(string path) {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
    }

    public IReadOnlyDictionary<int, TasbihWidgetState> Load() {
        return ReadStore();
    }

    public TasbihWidgetState GetOrCreate(int appWidgetId, Func<TasbihWidgetState> factory) {
        ArgumentNullException.ThrowIfNull(factory);

        var states = ReadStore();
        if (states.TryGetValue(appWidgetId, out var state)) {
            return state;
        }

        state = factory();
        states[appWidgetId] = state;
        SaveStore(states);
        return state;
    }

    public void Save(TasbihWidgetState state) {
        var states = ReadStore();
        states[state.AppWidgetId] = state;
        SaveStore(states);
    }

    public void Remove(int appWidgetId) {
        var states = ReadStore();
        if (!states.Remove(appWidgetId)) {
            return;
        }

        SaveStore(states);
    }

    private Dictionary<int, TasbihWidgetState> ReadStore() {
        try {
            if (!File.Exists(_path)) {
                return [];
            }

            var json = File.ReadAllText(_path);
            if (string.IsNullOrWhiteSpace(json)) {
                return [];
            }

            var payload = JsonSerializer.Deserialize<StorePayload>(json, _jsonOptions);
            return payload?.Widgets?.ToDictionary(item => item.AppWidgetId) ?? [];
        } catch {
            return [];
        }
    }

    private void SaveStore(Dictionary<int, TasbihWidgetState> states) {
        var payload = new StorePayload {
            Widgets = states.Values.OrderBy(item => item.AppWidgetId).ToList()
        };
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var tempPath = _path + ".tmp";

        try {
            File.WriteAllText(tempPath, json);
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

    private sealed class StorePayload {
        public List<TasbihWidgetState> Widgets { get; set; } = [];
    }
}
