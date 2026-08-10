using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Services;

/// <summary>Prevents automated acceptance runs from posting or cancelling real user notifications.</summary>
public sealed class AutomationNotificationScheduler : ILocalNotificationScheduler {
    public Task ScheduleAsync(IEnumerable<PrayerDay> days, AppSettings settings, CancellationToken cancellationToken, bool requestPermissions = true) => Task.CompletedTask;
    public Task CancelAsync() => Task.CompletedTask;
}
