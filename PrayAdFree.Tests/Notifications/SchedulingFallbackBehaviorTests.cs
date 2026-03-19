using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public sealed class SchedulingFallbackBehaviorTests {
    [Fact]
    public void Desktop_AlarmReminder_FallsBackToAdhan_AndKeepsClassicPath() {
        var normalized = NotificationPlatformPolicy.NormalizeReminderAlertType(
            AdhanReminderAlertType.Alarm,
            isMobilePlatform: false);

        Assert.Equal(AdhanReminderAlertType.Adhan, normalized);
        Assert.True(ReminderDispatchPolicy.ShouldPlayAdhan(normalized, enableAdhan: true));
        Assert.False(ReminderDispatchPolicy.ShouldOpenAlarmScreen(normalized));
    }

    [Fact]
    public void Mobile_AlarmReminder_StaysAlarm_AndOpensAlarmScreen() {
        var normalized = NotificationPlatformPolicy.NormalizeReminderAlertType(
            AdhanReminderAlertType.Alarm,
            isMobilePlatform: true);

        Assert.Equal(AdhanReminderAlertType.Alarm, normalized);
        Assert.True(ReminderDispatchPolicy.ShouldPlayAdhan(normalized, enableAdhan: true));
        Assert.True(ReminderDispatchPolicy.ShouldOpenAlarmScreen(normalized));
    }

    [Fact]
    public void Desktop_PrimaryAlarm_FallsBackToAdhanNotification() {
        var normalized = NotificationPlatformPolicy.NormalizePrimaryAdhanType(
            MobilePrimaryAdhanType.Alarm,
            isMobilePlatform: false);

        Assert.Equal(MobilePrimaryAdhanType.AdhanNotification, normalized);
    }
}
