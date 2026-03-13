using System.Text.Json;
using Microsoft.Maui.Storage;
using Pray_Ad_Free.Models;

namespace Pray_Ad_Free.Services;

public sealed class AlarmReminderCatalogService {
    private const string Folder = "alarm_reminders";
    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<AlarmReminderCatalogItem> LoadForCurrentLanguage() {
        var language = NormalizeLanguage(LocalizationManager.CurrentLanguage);
        var items = Load(language);
        if (items.Count > 0) {
            return items;
        }

        items = Load("en");
        if (items.Count > 0) {
            return items;
        }

        return Array.Empty<AlarmReminderCatalogItem>();
    }

    private static string NormalizeLanguage(string? language) {
        var value = string.IsNullOrWhiteSpace(language) ? "en" : language.Trim().ToLowerInvariant();
        if (value.Length == 2) {
            return value;
        }

        return value.Split('-', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "en";
    }

    private static IReadOnlyList<AlarmReminderCatalogItem> Load(string language) {
        var text = TryReadText(Path.Combine(Folder, $"{language}.json").Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(text)) {
            return Array.Empty<AlarmReminderCatalogItem>();
        }

        try {
            var items = JsonSerializer.Deserialize<List<AlarmReminderCatalogItem>>(text, JsonOptions) ?? new List<AlarmReminderCatalogItem>();
            return items
                .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        } catch {
            return Array.Empty<AlarmReminderCatalogItem>();
        }
    }

    private static string? TryReadText(string relativePath) {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var candidates = new[] {
            Path.Combine(AppContext.BaseDirectory, normalized),
            Path.Combine(AppContext.BaseDirectory, "Resources", "Raw", normalized),
            Path.Combine(AppContext.BaseDirectory, Path.GetFileName(normalized)),
            normalized
        };

        foreach (var path in candidates) {
            if (File.Exists(path)) {
                return File.ReadAllText(path);
            }
        }

        try {
            using var stream = FileSystem.OpenAppPackageFileAsync(relativePath.Replace('\\', '/')).GetAwaiter().GetResult();
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        } catch {
            return null;
        }
    }
}
