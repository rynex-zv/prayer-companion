using System.Collections.Concurrent;

namespace PrayAdFree.Core.Services;

/// <summary>Shares reconstructable backend work by operation key and authoritative input revision.</summary>
public sealed class ApplicationOperationCoalescer {
    private readonly ConcurrentDictionary<string, Lazy<Task<object?>>> _inFlight = new(StringComparer.Ordinal);

    public async Task<object?> RunAsync(
        string operationKey,
        long inputRevision,
        Func<CancellationToken, Task<object?>> operation,
        CancellationToken cancellationToken) {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        var key = $"{operationKey}@{inputRevision}";
        var lazy = _inFlight.GetOrAdd(key, _ => new Lazy<Task<object?>>(
            () => operation(CancellationToken.None),
            LazyThreadSafetyMode.ExecutionAndPublication));
        try {
            return await lazy.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
        } finally {
            if (lazy.IsValueCreated && lazy.Value.IsCompleted) _inFlight.TryRemove(new KeyValuePair<string, Lazy<Task<object?>>>(key, lazy));
        }
    }
}
