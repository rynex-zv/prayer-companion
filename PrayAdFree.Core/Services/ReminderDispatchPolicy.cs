using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public static class ReminderDispatchPolicy {
    public static bool ShouldEmitToast(AdhanReminderAlertType alertType) {
        return alertType != AdhanReminderAlertType.Silent;
    }

    public static bool ShouldPlayAdhan(AdhanReminderAlertType alertType, bool enableAdhan) {
        return enableAdhan && (alertType == AdhanReminderAlertType.Adhan || alertType == AdhanReminderAlertType.Alarm);
    }

    public static bool ShouldOpenAlarmScreen(AdhanReminderAlertType alertType) {
        return alertType == AdhanReminderAlertType.Alarm;
    }
}
