namespace Pray_Ad_Free.Services;

public interface INotificationBootstrapper {
    Task EnsureScheduledAsync(string reason, bool requestPermissions);
}
