using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public static class NotificationPlatformPolicy {
    public static MobilePrimaryAdhanType NormalizePrimaryAdhanType(
        MobilePrimaryAdhanType configured,
        bool isMobilePlatform) {
        return isMobilePlatform
            ? configured
            : MobilePrimaryAdhanType.AdhanNotification;
    }

    public static AdhanReminderAlertType NormalizeReminderAlertType(
        AdhanReminderAlertType configured,
        bool isMobilePlatform) {
        return !isMobilePlatform && configured == AdhanReminderAlertType.Alarm
            ? AdhanReminderAlertType.Adhan
            : configured;
    }
}
