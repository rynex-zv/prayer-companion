using PrayAdFree.Core.Services;

namespace Pray_Ad_Free.Services;

public interface IWindowsNotificationQueueService {
    void ReplaceSchedule(IReadOnlyList<PlannedNotification> notifications);
    void Clear();
}
