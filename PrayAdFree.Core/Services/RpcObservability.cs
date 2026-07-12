using System.Threading;

namespace PrayAdFree.Core.Services;

public static class RpcObservability {
    private static readonly AsyncLocal<Metrics?> CurrentMetrics = new();

    public static IDisposable Begin() {
        var previous = CurrentMetrics.Value;
        CurrentMetrics.Value = new Metrics();
        return new Scope(previous);
    }

    public static void RecordPersistenceWrite(int count = 1) => CurrentMetrics.Value?.AddWrites(count);
    public static void RecordCacheHit() => CurrentMetrics.Value?.AddCacheHit();
    public static void RecordCacheMiss() => CurrentMetrics.Value?.AddCacheMiss();
    public static Snapshot Capture() => CurrentMetrics.Value?.Capture() ?? new Snapshot(0, 0, 0);

    public sealed record Snapshot(int PersistenceWrites, int CacheHits, int CacheMisses);
    private sealed class Metrics {
        private int _writes, _hits, _misses;
        public void AddWrites(int count) => Interlocked.Add(ref _writes, count);
        public void AddCacheHit() => Interlocked.Increment(ref _hits);
        public void AddCacheMiss() => Interlocked.Increment(ref _misses);
        public Snapshot Capture() => new(_writes, _hits, _misses);
    }
    private sealed class Scope(Metrics? previous) : IDisposable {
        public void Dispose() => CurrentMetrics.Value = previous;
    }
}
