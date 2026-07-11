namespace PrayAdFree.Core.Services;

public static class WebAlarmSnapshotFactory {
    public static object Inactive(string language) => Build(
        language, false, string.Empty, string.Empty, string.Empty, string.Empty, false, 0, 0, 0);

    public static object Active(
        string language,
        string prayerClock,
        string delayFromBase,
        string prayerName,
        string reminderText,
        bool canSnooze,
        int minDelayMinutes,
        int maxDelayMinutes,
        int selectedDelayMinutes) => Build(
            language, true, prayerClock, delayFromBase, prayerName, reminderText,
            canSnooze, minDelayMinutes, maxDelayMinutes, selectedDelayMinutes);

    private static object Build(
        string language,
        bool isActive,
        string prayerClock,
        string delayFromBase,
        string prayerName,
        string reminderText,
        bool canSnooze,
        int minDelayMinutes,
        int maxDelayMinutes,
        int selectedDelayMinutes) => new {
            isActive,
            prayerClock,
            delayFromBase,
            prayerName,
            reminderText,
            canSnooze,
            minDelayMinutes,
            maxDelayMinutes,
            selectedDelayMinutes,
            labels = WebCatalog.AlarmLabels(language)
        };
}
