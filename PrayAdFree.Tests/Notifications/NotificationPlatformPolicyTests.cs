using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public sealed class NotificationPlatformPolicyTests {
    [Theory]
    [InlineData(true, MobilePrimaryAdhanType.AdhanNotification, MobilePrimaryAdhanType.AdhanNotification)]
    [InlineData(true, MobilePrimaryAdhanType.Alarm, MobilePrimaryAdhanType.Alarm)]
    [InlineData(false, MobilePrimaryAdhanType.Alarm, MobilePrimaryAdhanType.AdhanNotification)]
    [InlineData(false, MobilePrimaryAdhanType.AdhanNotification, MobilePrimaryAdhanType.AdhanNotification)]
    public void NormalizePrimaryAdhanType_UsesDesktopFallback(
        bool isMobile,
        MobilePrimaryAdhanType configured,
        MobilePrimaryAdhanType expected) {
        var actual = NotificationPlatformPolicy.NormalizePrimaryAdhanType(configured, isMobile);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(true, AdhanReminderAlertType.Alarm, AdhanReminderAlertType.Alarm)]
    [InlineData(false, AdhanReminderAlertType.Alarm, AdhanReminderAlertType.Adhan)]
    [InlineData(false, AdhanReminderAlertType.Adhan, AdhanReminderAlertType.Adhan)]
    [InlineData(false, AdhanReminderAlertType.Notification, AdhanReminderAlertType.Notification)]
    [InlineData(false, AdhanReminderAlertType.Silent, AdhanReminderAlertType.Silent)]
    public void NormalizeReminderAlertType_UsesDesktopFallback(
        bool isMobile,
        AdhanReminderAlertType configured,
        AdhanReminderAlertType expected) {
        var actual = NotificationPlatformPolicy.NormalizeReminderAlertType(configured, isMobile);
        Assert.Equal(expected, actual);
    }
}
