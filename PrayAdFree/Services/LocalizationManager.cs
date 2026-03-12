using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Maui.Storage;
using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Services;

public static class LocalizationManager {
    private const string CatalogPath = "i18n/index.json";
    private static readonly Dictionary<string, Dictionary<string, string>> Strings = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static LocalizationCatalog? _catalog;
    private static bool _initialized;

    public static string CurrentLanguage { get; private set; } = "en";
    public static event EventHandler? LanguageChanged;

    public static async Task InitializeAsync(string? language) {
        if (_initialized) {
            return;
        }

        _catalog = await LoadCatalogAsync().ConfigureAwait(false);
        SetLanguage(language);
        _initialized = true;
    }

    public static void SetLanguage(string? language) {
        var requested = ResolveLanguage(language);
        if (string.Equals(CurrentLanguage, requested, StringComparison.OrdinalIgnoreCase) && Strings.ContainsKey(requested)) {
            return;
        }

        CurrentLanguage = requested;
        EnsureLanguageLoaded(requested);
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static IReadOnlyList<LanguageOption> GetAvailableLanguages() {
        if (_catalog == null || _catalog.Languages.Count == 0) {
            return DefaultCatalog().Languages;
        }

        return _catalog.Languages;
    }

    public static string Translate(string key) {
        if (Strings.TryGetValue(CurrentLanguage, out var table) &&
            table.TryGetValue(key, out var value) &&
            !IsMissingLocalizedValue(key, value)) {
            return value;
        }

        return Strings.TryGetValue("en", out var fallbackTable) &&
               fallbackTable.TryGetValue(key, out var fallback) &&
               !IsMissingLocalizedValue(key, fallback)
            ? fallback
            : key;
    }

    public static string TranslatePrayer(PrayerId prayer) {
        return prayer switch {
            PrayerId.Fajr => Translate("Prayer_Fajr"),
            PrayerId.Sunrise => Translate("Prayer_Sunrise"),
            PrayerId.Dhuhr => Translate("Prayer_Dhuhr"),
            PrayerId.Asr => Translate("Prayer_Asr"),
            PrayerId.Maghrib => Translate("Prayer_Maghrib"),
            PrayerId.Isha => Translate("Prayer_Isha"),
            PrayerId.Imsak => Translate("Prayer_Imsak"),
            _ => prayer.ToString()
        };
    }

    private static string ResolveLanguage(string? language) {
        if (string.IsNullOrWhiteSpace(language) || language == "auto") {
            var device = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (IsSupported(device) || HasLanguageFile(device)) {
                return device;
            }

            return "en";
        }

        if (IsSupported(language) || HasLanguageFile(language)) {
            return language;
        }

        return "en";
    }

    private static bool HasLanguageFile(string language) {
        var normalized = language.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) {
            return false;
        }

        var text = TryReadText($"i18n/{normalized}.json");
        return !string.IsNullOrWhiteSpace(text);
    }

    private static bool IsSupported(string language) {
        return _catalog?.Languages.Any(item => item.Code.Equals(language, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static bool IsMissingLocalizedValue(string key, string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return true;
        }

        return string.Equals(value.Trim(), key, StringComparison.Ordinal);
    }

    private static void EnsureLanguageLoaded(string language) {
        if (Strings.ContainsKey(language)) {
            return;
        }

        var file = $"i18n/{language}.json";
        var data = LoadJsonDictionary(file);
        if (data != null) {
            Strings[language] = data;
        }

        if (!Strings.ContainsKey("en")) {
            var fallback = LoadJsonDictionary("i18n/en.json");
            if (fallback != null) {
                Strings["en"] = fallback;
            }
        }
    }

    private static async Task<LocalizationCatalog> LoadCatalogAsync() {
        try {
            if (OperatingSystem.IsWindows()) {
                var text = TryReadText("i18n/index.json");
                if (!string.IsNullOrWhiteSpace(text)) {
                    var catalogFromFile = JsonSerializer.Deserialize<LocalizationCatalog>(text, JsonOptions);
                    if (catalogFromFile != null) {
                        return catalogFromFile;
                    }
                }

                return DefaultCatalog();
            }

            await using var stream = await FileSystem.OpenAppPackageFileAsync(CatalogPath).ConfigureAwait(false);
            var catalog = await JsonSerializer.DeserializeAsync<LocalizationCatalog>(stream, JsonOptions).ConfigureAwait(false);
            return catalog ?? new LocalizationCatalog();
        } catch {
            var text = TryReadText("i18n/index.json");
            if (!string.IsNullOrWhiteSpace(text)) {
                var catalog = JsonSerializer.Deserialize<LocalizationCatalog>(text, JsonOptions);
                if (catalog != null) {
                    return catalog;
                }
            }

            return DefaultCatalog();
        }
    }

    private static Dictionary<string, string>? LoadJsonDictionary(string path) {
        try {
            var text = TryReadText(path);
            if (!string.IsNullOrWhiteSpace(text)) {
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(text);
                return data ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            if (File.Exists(path)) {
                var json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                return data ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            using var stream = FileSystem.OpenAppPackageFileAsync(path).GetAwaiter().GetResult();
            var dataStream = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
            return dataStream ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        } catch {
            return null;
        }
    }

    private static string? TryReadText(string relativePath) {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var candidates = new[] {
            Path.Combine(AppContext.BaseDirectory, normalized),
            Path.Combine(AppContext.BaseDirectory, "Resources", "Raw", normalized),
            Path.Combine(AppContext.BaseDirectory, "i18n", Path.GetFileName(normalized)),
            normalized,
            Path.Combine(FileSystem.AppDataDirectory, normalized)
        };

        foreach (var candidate in candidates) {
            if (File.Exists(candidate)) {
                return File.ReadAllText(candidate);
            }
        }

        return null;
    }

    private static LocalizationCatalog DefaultCatalog() {
        return new LocalizationCatalog {
            Languages = new List<LanguageOption> {
                new LanguageOption { Code = "en", Name = "English" },
                new LanguageOption { Code = "ar", Name = "Arabic" },
                new LanguageOption { Code = "fr", Name = "French" },
                new LanguageOption { Code = "tr", Name = "Turkish" },
                new LanguageOption { Code = "es", Name = "Spanish" }
            }
        };
    }
}

public sealed class LocalizationCatalog {
    public List<LanguageOption> Languages { get; set; } = new();
}

public sealed class LanguageOption {
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}
