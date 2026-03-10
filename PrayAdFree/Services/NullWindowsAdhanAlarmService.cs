using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Services;

public sealed class NullWindowsAdhanAlarmService : IWindowsAdhanAlarmService {
    public void Schedule(DateTime when, AdhanNotificationPayload payload) {
    }

    public void Clear() {
    }
}
