using System.Text.Json;
using System.Collections.Concurrent;
using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public interface IWidgetProfileRepository {
    WidgetProfileDocument? Load();
    void Save(WidgetProfileDocument document);
}

public sealed class InMemoryWidgetProfileRepository(WidgetProfileDocument? initial = null) : IWidgetProfileRepository {
    private WidgetProfileDocument? _document = initial;
    public WidgetProfileDocument? Load() => _document;
    public void Save(WidgetProfileDocument document) => _document = document;
}

public sealed class JsonFileWidgetProfileRepository : IWidgetProfileRepository {
    private static readonly ConcurrentDictionary<string, object> PathLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _path;
    private readonly object _sync;

    public JsonFileWidgetProfileRepository(string path) {
        _path = path;
        _sync = PathLocks.GetOrAdd(Path.GetFullPath(path), static _ => new object());
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
    }

    public WidgetProfileDocument? Load() {
        lock (_sync) {
            if (!File.Exists(_path)) return null;
            try {
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize(json, CoreJsonContext.Default.WidgetProfileDocument)
                    ?? throw new InvalidDataException("Widget profile document is null.");
            } catch (JsonException exception) {
                throw new InvalidDataException("Widget profile document is corrupt and was not replaced.", exception);
            }
        }
    }

    public void Save(WidgetProfileDocument document) {
        lock (_sync) {
            var json = JsonSerializer.Serialize(document, CoreJsonContext.Default.WidgetProfileDocument);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, _path, true);
            RpcObservability.RecordPersistenceWrite();
        }
    }
}

public sealed class WidgetProfileService {
    private readonly IWidgetProfileRepository _repository;
    private readonly object _sync = new();
    private WidgetProfileDocument _document;

    public WidgetProfileService(IWidgetProfileRepository repository) {
        _repository = repository;
        var stored = repository.Load();
        _document = Normalize(stored);
        if (stored is null) repository.Save(_document);
    }

    public static IReadOnlyList<WidgetCatalogEntry> Catalog { get; } = [
        Entry(WidgetTemplateKind.NextPrayer, "widgetNextPrayer",
            ["nextPrayerName", "nextPrayerTime"],
            ["nextPrayerName", "nextPrayerTime", "countdown", "followingPrayer"],
            ["nextPrayerName", "nextPrayerTime", "countdown", "followingPrayer", "location", "hijriDate", "gregorianDate", "locationSource"]),
        Entry(WidgetTemplateKind.DailyPrayer, "widgetDailyPrayer",
            ["prayerRows"],
            ["prayerRows", "hijriDate"],
            ["prayerRows", "hijriDate", "gregorianDate", "location", "locationSource"]),
        Entry(WidgetTemplateKind.Fasting, "widgetFasting",
            ["imsak", "iftar"],
            ["imsak", "iftar", "fastingCountdown"],
            ["imsak", "iftar", "fastingCountdown", "hijriDate", "gregorianDate", "location"]),
        Entry(WidgetTemplateKind.Tasbih, "widgetTasbih",
            ["tasbihCount"],
            ["tasbihText", "tasbihCount", "tasbihProgress", "tasbihIncrement"],
            ["tasbihPreset", "tasbihText", "tasbihCount", "tasbihProgress", "tasbihIncrement", "tasbihReset"]),
        Entry(WidgetTemplateKind.DateAndPrayer, "widgetDateAndPrayer",
            ["hijriDate", "nextPrayerName", "nextPrayerTime"],
            ["hijriDate", "gregorianDate", "nextPrayerName", "nextPrayerTime"],
            ["hijriDate", "gregorianDate", "nextPrayerName", "nextPrayerTime", "countdown", "location"]),
        Entry(WidgetTemplateKind.QiblaBearing, "widgetQiblaBearing",
            ["qiblaBearing"],
            ["qiblaBearing", "location", "openQibla"],
            ["qiblaBearing", "location", "openQibla"])
    ];

    public WidgetProfileDocument Snapshot() { lock (_sync) return _document; }

    public WidgetProfileDocument RefreshFromStorage() {
        lock (_sync) {
            var stored = _repository.Load();
            if (stored is null) return _document;
            _document = Normalize(stored);
            return _document;
        }
    }

    public WidgetProfile Create(WidgetTemplateKind template, string? name = null) {
        lock (_sync) {
        var catalog = GetCatalog(template);
        var profile = Validate(new WidgetProfile {
            Id = Guid.NewGuid().ToString("N"),
            Name = NormalizeName(name, catalog.NameKey),
            Template = template,
            Projection = catalog.DefaultProjection.ToArray(),
            Style = new WidgetStyle(),
            Privacy = new WidgetPrivacy()
        });
        Commit(_document with {
            Revision = _document.Revision + 1,
            Profiles = _document.Profiles.Append(profile).ToArray()
        });
        return profile;
        }
    }

    public WidgetProfile Update(string id, WidgetProfilePatch patch) {
        lock (_sync) {
        var current = Find(id);
        if (patch.ExpectedRevision.HasValue && patch.ExpectedRevision.Value != current.Revision) {
            throw new InvalidOperationException($"Widget profile revision conflict for {id}.");
        }
        var updated = Validate(current with {
            Name = patch.Name is null ? current.Name : NormalizeName(patch.Name, current.Name),
            Density = patch.Density ?? current.Density,
            Projection = patch.Projection ?? current.Projection,
            Style = patch.Style ?? current.Style,
            Privacy = patch.Privacy ?? current.Privacy,
            Revision = current.Revision + 1
        });
        Commit(_document with {
            Revision = _document.Revision + 1,
            Profiles = _document.Profiles.Select(item => item.Id == id ? updated : item).ToArray()
        });
        return updated;
        }
    }

    public WidgetProfile Duplicate(string id, string? name = null) {
        lock (_sync) {
        var source = Find(id);
        var duplicate = source with {
            Id = Guid.NewGuid().ToString("N"),
            Name = NormalizeName(name, $"{source.Name} copy"),
            Revision = 1,
            IsBuiltIn = false
        };
        Commit(_document with {
            Revision = _document.Revision + 1,
            Profiles = _document.Profiles.Append(duplicate).ToArray()
        });
        return duplicate;
        }
    }

    public WidgetProfile Reset(string id) {
        lock (_sync) {
        var current = Find(id);
        var catalog = GetCatalog(current.Template);
        return Update(id, new WidgetProfilePatch {
            ExpectedRevision = current.Revision,
            Density = WidgetDensity.Auto,
            Projection = catalog.DefaultProjection,
            Style = new WidgetStyle(),
            Privacy = new WidgetPrivacy()
        });
        }
    }

    public WidgetProfileDocument Delete(string id) {
        lock (_sync) {
        var profile = Find(id);
        if (profile.IsBuiltIn) throw new InvalidOperationException("Built-in widget profiles must be reset, not deleted.");
        if (_document.Assignments.Any(item => item.ProfileId == id)) {
            throw new InvalidOperationException("A widget profile assigned to an installed widget cannot be deleted.");
        }
        Commit(_document with {
            Revision = _document.Revision + 1,
            Profiles = _document.Profiles.Where(item => item.Id != id).ToArray()
        });
        return _document;
        }
    }

    public WidgetInstanceAssignment Assign(WidgetInstanceAssignment assignment) {
        lock (_sync) {
        if (string.IsNullOrWhiteSpace(assignment.InstanceId)) throw new ArgumentException("Widget instance ID is required.");
        _ = Find(assignment.ProfileId);
        var existing = _document.Assignments.FirstOrDefault(item => item.InstanceId == assignment.InstanceId);
        if (existing == assignment) return existing;
        var updated = _document.Assignments
            .Where(item => item.InstanceId != assignment.InstanceId)
            .Append(assignment)
            .ToArray();
        Commit(_document with { Revision = _document.Revision + 1, Assignments = updated });
        return assignment;
        }
    }

    public WidgetProfileDocument Unassign(string instanceId) {
        lock (_sync) {
        if (string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException("Widget instance ID is required.");
        if (!_document.Assignments.Any(item => item.InstanceId == instanceId)) return _document;
        Commit(_document with {
            Revision = _document.Revision + 1,
            Assignments = _document.Assignments.Where(item => item.InstanceId != instanceId).ToArray()
        });
        return _document;
        }
    }

    public WidgetProfile Find(string id) {
        lock (_sync) return _document.Profiles.FirstOrDefault(item => item.Id == id)
            ?? throw new KeyNotFoundException($"Widget profile {id} was not found.");
    }

    public WidgetProfile ValidatePreview(WidgetProfile profile) => Validate(profile);

    private void Commit(WidgetProfileDocument document) {
        _repository.Save(document);
        _document = document;
    }

    private static WidgetProfileDocument Normalize(WidgetProfileDocument? document) {
        var defaults = BuildDefaults();
        if (document is null) return new WidgetProfileDocument { Profiles = defaults };
        var profiles = document.Profiles.Count == 0 ? defaults : document.Profiles.Select(Validate).ToArray();
        return document with { Profiles = profiles };
    }

    private static WidgetProfile[] BuildDefaults() => Catalog.Select((item, index) => new WidgetProfile {
        Id = $"default-{ToKebab(item.Template.ToString())}",
        Name = item.NameKey,
        Template = item.Template,
        Projection = item.DefaultProjection.ToArray(),
        IsBuiltIn = true,
        Revision = 1,
        Style = new WidgetStyle(),
        Privacy = new WidgetPrivacy()
    }).ToArray();

    private static WidgetProfile Validate(WidgetProfile profile) {
        if (string.IsNullOrWhiteSpace(profile.Id)) throw new ArgumentException("Widget profile ID is required.");
        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Trim().Length > 80) throw new ArgumentException("Widget profile name must contain 1-80 characters.");
        var catalog = GetCatalog(profile.Template);
        var projection = profile.Projection.Distinct(StringComparer.Ordinal).ToArray();
        if (projection.Except(catalog.AllowedProjection, StringComparer.Ordinal).Any()) throw new ArgumentException("Widget projection contains unsupported fields.");
        if (catalog.RequiredProjection.Except(projection, StringComparer.Ordinal).Any()) throw new ArgumentException("Widget projection omits required fields.");
        ValidateColor(profile.Style.PrimaryTextColor, nameof(profile.Style.PrimaryTextColor));
        ValidateColor(profile.Style.SecondaryTextColor, nameof(profile.Style.SecondaryTextColor));
        ValidateColor(profile.Style.BackgroundColor, nameof(profile.Style.BackgroundColor));
        ValidateColor(profile.Style.AccentColor, nameof(profile.Style.AccentColor));
        if (profile.Style.BackgroundOpacity is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(profile.Style.BackgroundOpacity));
        if (!WidgetContrast.IsReadable(profile.Style.PrimaryTextColor, profile.Style.BackgroundColor)) {
            throw new ArgumentException("Primary widget text does not meet the 4.5:1 contrast requirement.");
        }
        return profile with { Name = profile.Name.Trim(), Projection = projection };
    }

    private static void ValidateColor(string value, string name) {
        if (value.Length != 9 || value[0] != '#' || !uint.TryParse(value[1..], System.Globalization.NumberStyles.HexNumber, null, out _)) {
            throw new ArgumentException($"{name} must use #AARRGGBB.");
        }
    }

    private static WidgetCatalogEntry GetCatalog(WidgetTemplateKind template) => Catalog.First(item => item.Template == template);
    private static WidgetCatalogEntry Entry(WidgetTemplateKind template, string name, string[] required, string[] defaults, string[] allowed) =>
        new(template, name, required, defaults, allowed);
    private static string NormalizeName(string? name, string fallback) => string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
    private static string ToKebab(string value) => string.Concat(value.Select((character, index) => char.IsUpper(character) && index > 0 ? $"-{char.ToLowerInvariant(character)}" : char.ToLowerInvariant(character).ToString()));
}

public static class WidgetContrast {
    public static bool IsReadable(string foregroundArgb, string backgroundArgb) {
        var foreground = Parse(foregroundArgb);
        var background = Parse(backgroundArgb);
        var lighter = Math.Max(Luminance(foreground), Luminance(background));
        var darker = Math.Min(Luminance(foreground), Luminance(background));
        return (lighter + 0.05) / (darker + 0.05) >= 4.5;
    }

    private static (byte R, byte G, byte B) Parse(string argb) => (
        Convert.ToByte(argb.Substring(3, 2), 16),
        Convert.ToByte(argb.Substring(5, 2), 16),
        Convert.ToByte(argb.Substring(7, 2), 16));

    private static double Luminance((byte R, byte G, byte B) color) =>
        0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
    private static double Linear(byte component) {
        var value = component / 255d;
        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
