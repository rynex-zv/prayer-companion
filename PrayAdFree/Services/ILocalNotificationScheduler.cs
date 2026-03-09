using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Services;

public interface ILocalNotificationScheduler {
    Task ScheduleAsync(IEnumerable<PrayerDay> days, AppSettings settings, CancellationToken cancellationToken, bool requestPermissions = true);
    Task CancelAsync();
}
