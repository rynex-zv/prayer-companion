using System.Text.Json;
using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public sealed class SettingsService {
    private const string SettingsKey = "app_settings";
    private readonly ISettingsStore _store;
    public SettingsService(ISettingsStore store) {
        _store = store;
    }

    public AppSettings Load() {
        try {
            var json = _store.Get(SettingsKey, "");
            if (string.IsNullOrWhiteSpace(json)) {
                return new AppSettings();
            }

            return JsonSerializer.Deserialize(json, CoreJsonContext.Default.AppSettings) ?? new AppSettings();
        } catch {
            var fallback = new AppSettings();
            try {
                Save(fallback);
            } catch {
            }
            return fallback;
        }
    }

    public void Save(AppSettings settings) {
        var json = JsonSerializer.Serialize(settings, CoreJsonContext.Default.AppSettings);
        _store.Set(SettingsKey, json);
    }
}
