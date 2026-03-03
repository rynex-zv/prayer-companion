using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Services;

public interface ILocalNotificationScheduler {
    Task ScheduleAsync(IEnumerable<PrayerDay> days, NotificationSettings settings, CancellationToken cancellationToken);
    Task CancelAsync();
}
