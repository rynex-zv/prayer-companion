using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Services;

public interface IAdhanPlaybackService {
    void Initialize();
    Task<bool> PlayPreviewAsync(string? soundKey);
    Task<bool> PlayScheduledAsync(AdhanNotificationPayload payload);
    Task<bool> ScheduleTestAlarmAsync(string? soundKey, TimeSpan delay);
    Task StopAsync();
}
