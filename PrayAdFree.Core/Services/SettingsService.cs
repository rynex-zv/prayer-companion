using System.Text.Json;
using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public sealed class SettingsService {
    private const string SettingsKey = "app_settings";
    private readonly ISettingsStore _store;
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions {
        WriteIndented = true
    };

    public SettingsService(ISettingsStore store) {
        _store = store;
    }

    public AppSettings Load() {
        var json = _store.Get(SettingsKey, "");
        if (string.IsNullOrWhiteSpace(json)) {
            return new AppSettings();
        }

        return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
    }

    public void Save(AppSettings settings) {
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        _store.Set(SettingsKey, json);
    }
}
