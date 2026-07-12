using System.Reflection;
using System.Text.Json;
using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

/// <summary>
/// Loads Islamic occasions from embedded JSON resources.
/// Base file: base.c.event.json. Madhhab overrides: {madhhab}.c.event.json.
/// Merge rule: madhhab entries override base entries with the same id.
/// </summary>
public sealed class IslamicOccasionCatalog {
    private static readonly Assembly Asm = typeof(IslamicOccasionCatalog).Assembly;
    private static readonly Dictionary<string, IReadOnlyList<IslamicOccasion>> Cache = new();
    private static readonly object Gate = new();

    public IReadOnlyList<IslamicOccasion> ForMadhhab(Madhhab madhhab) {
        var key = madhhab.ToString().ToLowerInvariant();
        lock (Gate) {
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var baseList = Load("base");
            var madhhabList = Load(key);
            var byId = baseList.ToDictionary(o => o.Id, o => o);
            foreach (var o in madhhabList) byId[o.Id] = o;
            var merged = byId.Values
                .OrderBy(o => o.HijriMonth)
                .ThenBy(o => o.HijriDay)
                .ToList();
            Cache[key] = merged;
            return merged;
        }
    }

    private static IReadOnlyList<IslamicOccasion> Load(string source) {
        var resourceName = $"PrayAdFree.Core.Resources.CalendarEvents.{source}.c.event.json";
        using var stream = Asm.GetManifestResourceStream(resourceName);
        if (stream is null) return Array.Empty<IslamicOccasion>();
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<IslamicOccasion>();
        var typeInfo = CoreJsonContext.Default.GetTypeInfo(typeof(List<Entry>))
            ?? throw new InvalidOperationException("Islamic occasion JSON metadata is unavailable.");
        var items = (List<Entry>?)JsonSerializer.Deserialize(json, typeInfo) ?? new();
        return items.Select(e => new IslamicOccasion {
            Id = e.Id ?? "",
            HijriMonth = e.HijriMonth,
            HijriDay = e.HijriDay,
            LabelKey = e.LabelKey ?? "",
            Importance = e.Importance ?? "minor",
            Color = e.Color ?? "primary",
            Source = source
        }).ToList();
    }

    

    internal sealed class Entry {
        public string? Id { get; set; }
        public int HijriMonth { get; set; }
        public int HijriDay { get; set; }
        public string? LabelKey { get; set; }
        public string? Importance { get; set; }
        public string? Color { get; set; }
    }
}
