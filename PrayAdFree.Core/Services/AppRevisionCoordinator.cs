using PrayAdFree.Core.Contracts;

namespace PrayAdFree.Core.Services;

public sealed class AppRevisionCoordinator {
    private readonly object _gate = new();
    private readonly Dictionary<string, long> _domains = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<AppEvent> _pending = new();
    private long _global;
    private long _sequence;

    public AppRevisionCoordinator(AppRevision? initial = null) {
        if (initial is null) return;
        _global = initial.Global;
        _sequence = initial.EventSequence;
        foreach (var pair in initial.Domains) _domains[pair.Key] = pair.Value;
    }

    public AppRevision Snapshot() {
        lock (_gate) return new AppRevision(_global, new Dictionary<string, long>(_domains), _sequence);
    }

    public AppEvent Changed(string domain, string? causeRequestId, string type = "domain.changed", object? payload = null, string? invalidationKey = null) {
        lock (_gate) {
            var revision = ++_global;
            _domains[domain] = revision;
            var appEvent = new AppEvent(++_sequence, Guid.NewGuid().ToString("D"), DateTimeOffset.UtcNow, domain, type, revision, causeRequestId, payload, invalidationKey);
            _pending.Enqueue(appEvent);
            return appEvent;
        }
    }

    public IReadOnlyList<AppEvent> DrainEvents() {
        lock (_gate) {
            var events = _pending.ToArray();
            _pending.Clear();
            return events;
        }
    }
}

public sealed record AppRevision(long Global, IReadOnlyDictionary<string, long> Domains, long EventSequence);
