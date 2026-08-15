using System.Collections.Concurrent;
using System.Text.Json;
using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public sealed class WindowsWidgetProjectionStore {
    private static readonly ConcurrentDictionary<string, object> PathLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _path;
    private readonly object _sync;

    public WindowsWidgetProjectionStore(string path) {
        _path = Path.GetFullPath(path);
        _sync = PathLocks.GetOrAdd(_path, static _ => new object());
        Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
    }

    public WindowsWidgetProjectionBundle Load() {
        lock (_sync) {
            if (!File.Exists(_path)) return new WindowsWidgetProjectionBundle();
            try {
                return JsonSerializer.Deserialize(File.ReadAllText(_path), CoreJsonContext.Default.WindowsWidgetProjectionBundle)
                    ?? throw new InvalidDataException("Windows widget projection bundle is empty.");
            } catch (JsonException exception) {
                throw new InvalidDataException("Windows widget projection bundle is corrupt and was not replaced.", exception);
            }
        }
    }

    public WindowsWidgetProjectionBundle Put(WindowsWidgetInstanceProjection instance) {
        ArgumentNullException.ThrowIfNull(instance);
        if (string.IsNullOrWhiteSpace(instance.InstanceId)) throw new ArgumentException("Widget instance ID is required.");
        lock (_sync) {
            var current = Load();
            var instances = current.Instances.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
            instances[instance.InstanceId] = instance;
            var next = current with { Revision = current.Revision + 1, Instances = instances };
            SaveCore(next);
            return next;
        }
    }

    public WindowsWidgetProjectionBundle Remove(string instanceId) {
        lock (_sync) {
            var current = Load();
            var instances = current.Instances.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
            if (!instances.Remove(instanceId)) return current;
            var next = current with { Revision = current.Revision + 1, Instances = instances };
            SaveCore(next);
            return next;
        }
    }

    public WidgetRenderTree Resolve(string instanceId, WidgetFamily family) {
        var current = Load();
        if (!current.Instances.TryGetValue(instanceId, out var instance)) return Missing(instanceId, family, "Widget data is not available yet.");
        if (!instance.RenderTrees.TryGetValue(family, out var tree)) return Missing(instanceId, family, "Widget data for this size is not available yet.");
        return tree;
    }

    private void SaveCore(WindowsWidgetProjectionBundle bundle) {
        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(bundle, CoreJsonContext.Default.WindowsWidgetProjectionBundle));
        File.Move(temporary, _path, true);
        RpcObservability.RecordPersistenceWrite();
    }

    private static WidgetRenderTree Missing(string instanceId, WidgetFamily family, string error) => new() {
        ProfileId = instanceId,
        Family = family,
        Status = "error",
        Error = error
    };
}
