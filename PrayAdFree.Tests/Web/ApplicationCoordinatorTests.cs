using PrayAdFree.Core.Contracts;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests.Web;

public sealed class ApplicationCoordinatorTests {
    [Fact]
    public async Task Publishes_only_after_commit_and_replays_by_command_id() {
        var order = new List<string>();
        var transaction = new RecordingTransaction(order);
        var coordinator = new ApplicationCoordinator(
            new RecordingFactory(transaction),
            new AppRevisionCoordinator(),
            (appEvent, _) => { order.Add("publish"); return Task.CompletedTask; });
        var request = new ApplicationCommandRequest("request-1", "command-1", "tasbih.increment", "tasbih", 0);

        var first = await coordinator.CommandAsync(request, _ => {
            order.Add("handler");
            return Task.FromResult<object?>(new { count = 1 });
        }, default);
        var replay = await coordinator.CommandAsync(request, _ => throw new InvalidOperationException("must not rerun"), default);

        Assert.Equal(["handler", "commit", "publish"], order);
        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        Assert.Equal(first.Revision, replay.Revision);
        Assert.Single(first.Events);
    }

    [Fact]
    public async Task Rolls_back_without_revision_or_event_when_handler_fails() {
        var order = new List<string>();
        var revisions = new AppRevisionCoordinator();
        var coordinator = new ApplicationCoordinator(
            new RecordingFactory(new RecordingTransaction(order)), revisions,
            (_, _) => { order.Add("publish"); return Task.CompletedTask; });

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.CommandAsync(
            new("request-1", "command-1", "settings.changeTheme", "settings", 0),
            _ => throw new InvalidOperationException("failed"), default));

        Assert.Equal(["rollback"], order);
        Assert.Equal(0, revisions.Snapshot().Global);
    }

    [Fact]
    public async Task Rejects_stale_expected_revision_before_starting_transaction() {
        var revisions = new AppRevisionCoordinator();
        revisions.Changed("settings", null);
        var started = false;
        var coordinator = new ApplicationCoordinator(
            new DelegateFactory(() => { started = true; return new RecordingTransaction([]); }), revisions,
            (_, _) => Task.CompletedTask);

        var error = await Assert.ThrowsAsync<ApplicationRevisionConflictException>(() => coordinator.CommandAsync(
            new("request-2", "command-2", "settings.changeTheme", "settings", 0),
            _ => Task.FromResult<object?>(null), default));

        Assert.False(started);
        Assert.Equal(0, error.Expected);
        Assert.Equal(1, error.Actual);
    }

    private sealed class RecordingFactory(IApplicationTransaction transaction) : IApplicationTransactionFactory {
        public ValueTask<IApplicationTransaction> BeginAsync(CancellationToken cancellationToken) => ValueTask.FromResult(transaction);
    }

    private sealed class DelegateFactory(Func<IApplicationTransaction> create) : IApplicationTransactionFactory {
        public ValueTask<IApplicationTransaction> BeginAsync(CancellationToken cancellationToken) => ValueTask.FromResult(create());
    }

    private sealed class RecordingTransaction(List<string> order) : IApplicationTransaction {
        public Task CommitAsync(CancellationToken cancellationToken) { order.Add("commit"); return Task.CompletedTask; }
        public Task RollbackAsync(CancellationToken cancellationToken) { order.Add("rollback"); return Task.CompletedTask; }
    }
}
