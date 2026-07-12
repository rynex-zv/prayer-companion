using PrayAdFree.Core.Contracts;

namespace PrayAdFree.Core.Services;

public interface IApplicationTransaction {
    Task CommitAsync(CancellationToken cancellationToken);
    Task RollbackAsync(CancellationToken cancellationToken);
}

public interface IApplicationTransactionFactory {
    ValueTask<IApplicationTransaction> BeginAsync(CancellationToken cancellationToken);
}

public sealed record ApplicationCommandRequest(
    string RequestId,
    string CommandId,
    string Name,
    string Domain,
    long? ExpectedRevision);

public sealed record ApplicationCommandResult(
    object? Data,
    AppRevision Revision,
    IReadOnlyList<AppEvent> Events,
    bool Replayed);

public sealed record ApplicationCommandExecution(
    object? Data,
    IReadOnlyList<Func<CancellationToken, Task>> AfterCommit);

public sealed class ApplicationRevisionConflictException(long expected, long actual)
    : Exception($"Expected revision {expected}, but the current revision is {actual}.") {
    public long Expected { get; } = expected;
    public long Actual { get; } = actual;
}

/// <summary>Coordinates the persistence, revision, event, and idempotency boundary for application commands.</summary>
public sealed class ApplicationCoordinator {
    private const int MaxCompletedCommands = 512;
    private readonly IApplicationTransactionFactory _transactions;
    private readonly AppRevisionCoordinator _revisions;
    private readonly Func<AppEvent, CancellationToken, Task> _publish;
    private readonly Dictionary<string, ApplicationCommandResult> _completed = new(StringComparer.Ordinal);
    private readonly Queue<string> _completionOrder = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ApplicationCoordinator(
        IApplicationTransactionFactory transactions,
        AppRevisionCoordinator revisions,
        Func<AppEvent, CancellationToken, Task> publish) {
        _transactions = transactions;
        _revisions = revisions;
        _publish = publish;
    }

    public AppRevision Revisions => _revisions.Snapshot();

    public async Task<ApplicationCommandResult> CommandAsync(
        ApplicationCommandRequest request,
        Func<CancellationToken, Task<object?>> handler,
        CancellationToken cancellationToken) {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RequestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CommandId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Domain);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            if (_completed.TryGetValue(request.CommandId, out var completed)) {
                return completed with { Replayed = true };
            }

            var before = _revisions.Snapshot();
            if (request.ExpectedRevision is long expected && expected != before.Global) {
                throw new ApplicationRevisionConflictException(expected, before.Global);
            }

            var transaction = await _transactions.BeginAsync(cancellationToken).ConfigureAwait(false);
            object? data;
            try {
                data = await handler(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            } catch {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }

            if (data is ApplicationCommandExecution execution) {
                data = execution.Data;
                foreach (var effect in execution.AfterCommit) {
                    await effect(CancellationToken.None).ConfigureAwait(false);
                }
            }

            // Revision and events are created only after durable commit and coordinated effects succeed.
            var appEvent = _revisions.Changed(request.Domain, request.RequestId, invalidationKey: $"{request.Domain}.*");
            var result = new ApplicationCommandResult(data, _revisions.Snapshot(), [appEvent], false);
            _completed[request.CommandId] = result;
            _completionOrder.Enqueue(request.CommandId);
            while (_completionOrder.Count > MaxCompletedCommands) {
                _completed.Remove(_completionOrder.Dequeue());
            }
            await _publish(appEvent, cancellationToken).ConfigureAwait(false);
            return result;
        } finally {
            _gate.Release();
        }
    }
}

public sealed class ImmediateApplicationTransactionFactory : IApplicationTransactionFactory {
    public ValueTask<IApplicationTransaction> BeginAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IApplicationTransaction>(ImmediateApplicationTransaction.Instance);

    private sealed class ImmediateApplicationTransaction : IApplicationTransaction {
        public static readonly ImmediateApplicationTransaction Instance = new();
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
