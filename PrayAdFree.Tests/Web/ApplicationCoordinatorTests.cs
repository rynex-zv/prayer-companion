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

    [Fact]
    public async Task Settings_repository_stages_writes_until_commit_and_discards_rollback() {
        var store = new MemorySettingsStore();
        var repository = new SettingsService(store);
        var committed = 0;
        repository.Committed += (_, _) => committed++;
        repository.Save(new PrayAdFree.Core.Models.AppSettings { Language = "en" });
        Assert.Equal(1, committed);

        var rollback = await repository.BeginAsync(default);
        repository.Save(new PrayAdFree.Core.Models.AppSettings { Language = "ar" });
        Assert.Equal("ar", repository.Load().Language);
        Assert.Equal(1, store.WriteCount);
        await rollback.RollbackAsync(default);
        Assert.Equal("en", repository.Load().Language);
        Assert.Equal(1, store.WriteCount);
        Assert.Equal(1, committed);

        var commit = await repository.BeginAsync(default);
        repository.Save(new PrayAdFree.Core.Models.AppSettings { Language = "tr" });
        Assert.Equal(1, store.WriteCount);
        await commit.CommitAsync(default);
        Assert.Equal("tr", repository.Load().Language);
        Assert.Equal(2, store.WriteCount);
        Assert.Equal(2, committed);
    }

    [Fact]
    public async Task Failed_application_command_never_persists_staged_settings_or_publishes() {
        var store = new MemorySettingsStore();
        var repository = new SettingsService(store);
        repository.Save(new PrayAdFree.Core.Models.AppSettings { Language = "en" });
        var published = 0;
        var coordinator = new ApplicationCoordinator(
            repository,
            new AppRevisionCoordinator(),
            (_, _) => { published++; return Task.CompletedTask; });

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.CommandAsync(
            new("request", "command", "settings.setLanguage", "settings", null),
            _ => {
                repository.Save(new PrayAdFree.Core.Models.AppSettings { Language = "ar" });
                throw new InvalidOperationException("simulate workflow failure");
            },
            default));

        Assert.Equal("en", repository.Load().Language);
        Assert.Equal(1, store.WriteCount);
        Assert.Equal(0, published);
    }

    [Fact]
    public async Task Runs_coordinated_effects_after_commit_and_before_event() {
        var order = new List<string>();
        var coordinator = new ApplicationCoordinator(
            new RecordingFactory(new RecordingTransaction(order)),
            new AppRevisionCoordinator(),
            (_, _) => { order.Add("publish"); return Task.CompletedTask; });

        var result = await coordinator.CommandAsync(
            new("request", "command", "settings.save", "settings", 0),
            _ => Task.FromResult<object?>(new ApplicationCommandExecution(
                new { saved = true },
                [_ => { order.Add("effect"); return Task.CompletedTask; }])),
            default);

        Assert.Equal(["commit", "effect", "publish"], order);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task Coalescer_shares_work_only_for_the_same_key_and_revision() {
        var coalescer = new ApplicationOperationCoalescer();
        var executions = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<object?> Work(CancellationToken _) {
            Interlocked.Increment(ref executions);
            return CompleteAsync();
        }
        async Task<object?> CompleteAsync() { await release.Task; return "result"; }

        var first = coalescer.RunAsync("today.bootstrap", 7, Work, default);
        var duplicate = coalescer.RunAsync("today.bootstrap", 7, Work, default);
        await Task.Yield();
        Assert.Equal(1, executions);
        release.SetResult();
        Assert.Equal("result", await first);
        Assert.Equal("result", await duplicate);

        await coalescer.RunAsync("today.bootstrap", 8, _ => { executions++; return Task.FromResult<object?>("new"); }, default);
        Assert.Equal(2, executions);
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

    private sealed class MemorySettingsStore : ISettingsStore {
        private string _value = "";
        public int WriteCount { get; private set; }
        public T Get<T>(string key, T defaultValue) => string.IsNullOrEmpty(_value) ? defaultValue : (T)(object)_value;
        public void Set<T>(string key, T value) { _value = (string)(object)value!; WriteCount++; }
    }
}
