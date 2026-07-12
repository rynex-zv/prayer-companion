using System.Text.Json;
using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public interface ISettingsRepository {
    event EventHandler? Committed;
    AppSettings Load();
    void Save(AppSettings settings);
}

public sealed class SettingsService : ISettingsRepository, IApplicationTransactionFactory {
    private const string SettingsKey = "app_settings";
    private readonly ISettingsStore _store;
    private readonly AsyncLocal<PendingTransaction?> _transaction = new();
    public event EventHandler? Committed;
    public SettingsService(ISettingsStore store) {
        _store = store;
    }

    public AppSettings Load() {
        try {
            var json = _transaction.Value?.PendingJson ?? _store.Get(SettingsKey, "");
            if (string.IsNullOrWhiteSpace(json)) {
                return new AppSettings();
            }

            return JsonSerializer.Deserialize(json, CoreJsonContext.Default.AppSettings) ?? new AppSettings();
        } catch {
            var fallback = new AppSettings();
            try {
                Save(fallback);
            } catch {
            }
            return fallback;
        }
    }

    public void Save(AppSettings settings) {
        var json = JsonSerializer.Serialize(settings, CoreJsonContext.Default.AppSettings);
        var transaction = _transaction.Value;
        if (transaction is not null) {
            transaction.PendingJson = json;
            return;
        }
        _store.Set(SettingsKey, json);
        NotifyCommitted();
    }

    public ValueTask<IApplicationTransaction> BeginAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (_transaction.Value is not null) throw new InvalidOperationException("A settings transaction is already active.");
        var pending = new PendingTransaction();
        _transaction.Value = pending;
        return ValueTask.FromResult<IApplicationTransaction>(new Transaction(this, pending));
    }

    private sealed class PendingTransaction {
        public string? PendingJson { get; set; }
        public bool Completed { get; set; }
    }

    private sealed class Transaction(SettingsService owner, PendingTransaction pending) : IApplicationTransaction {
        public Task CommitAsync(CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureActive();
            if (pending.PendingJson is not null) {
                owner._store.Set(SettingsKey, pending.PendingJson);
            }
            Complete();
            if (pending.PendingJson is not null) owner.NotifyCommitted();
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken) {
            EnsureActive();
            Complete();
            return Task.CompletedTask;
        }

        private void EnsureActive() {
            if (pending.Completed || !ReferenceEquals(owner._transaction.Value, pending)) {
                throw new InvalidOperationException("The settings transaction is no longer active.");
            }
        }

        private void Complete() {
            pending.Completed = true;
            owner._transaction.Value = null;
        }
    }

    private void NotifyCommitted() {
        foreach (EventHandler handler in Committed?.GetInvocationList() ?? []) {
            try { handler(this, EventArgs.Empty); } catch { }
        }
    }
}
