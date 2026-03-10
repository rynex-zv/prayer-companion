using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Services;

public interface IWindowsAdhanAlarmService {
    void Schedule(DateTime when, AdhanNotificationPayload payload);
    void Clear();
}
