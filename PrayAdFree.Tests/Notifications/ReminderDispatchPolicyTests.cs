using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public sealed class ReminderDispatchPolicyTests {
    [Fact]
    public void ShouldEmitToast_RespectsSilentRule() {
        Assert.True(ReminderDispatchPolicy.ShouldEmitToast(AdhanReminderAlertType.Adhan));
        Assert.True(ReminderDispatchPolicy.ShouldEmitToast(AdhanReminderAlertType.Alarm));
        Assert.True(ReminderDispatchPolicy.ShouldEmitToast(AdhanReminderAlertType.Notification));
        Assert.False(ReminderDispatchPolicy.ShouldEmitToast(AdhanReminderAlertType.Silent));
    }

    [Theory]
    [InlineData(true, AdhanReminderAlertType.Adhan, true)]
    [InlineData(false, AdhanReminderAlertType.Adhan, false)]
    [InlineData(true, AdhanReminderAlertType.Alarm, true)]
    [InlineData(false, AdhanReminderAlertType.Alarm, false)]
    [InlineData(true, AdhanReminderAlertType.Notification, false)]
    [InlineData(true, AdhanReminderAlertType.Silent, false)]
    public void ShouldPlayAdhan_OnlyForAdhanTypeWhenEnabled(bool enabled, AdhanReminderAlertType type, bool expected) {
        var result = ReminderDispatchPolicy.ShouldPlayAdhan(type, enabled);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(AdhanReminderAlertType.Adhan, false)]
    [InlineData(AdhanReminderAlertType.Notification, false)]
    [InlineData(AdhanReminderAlertType.Silent, false)]
    [InlineData(AdhanReminderAlertType.Alarm, true)]
    public void ShouldOpenAlarmScreen_OnlyForAlarm(AdhanReminderAlertType type, bool expected) {
        Assert.Equal(expected, ReminderDispatchPolicy.ShouldOpenAlarmScreen(type));
    }
}
