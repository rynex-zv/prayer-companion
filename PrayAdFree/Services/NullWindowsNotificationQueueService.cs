using PrayAdFree.Core.Services;

namespace Pray_Ad_Free.Services;

public sealed class NullWindowsNotificationQueueService : IWindowsNotificationQueueService {
    public void ReplaceSchedule(IReadOnlyList<PlannedNotification> notifications) {
    }

    public void Clear() {
    }
}
