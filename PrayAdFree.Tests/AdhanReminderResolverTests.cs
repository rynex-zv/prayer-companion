using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public sealed class AdhanReminderResolverTests {
    [Fact]
    public void Resolve_UsesReminderItems_WhenPresent() {
        var settings = new NotificationSettings {
            ReminderItems = new List<AdhanReminderItem> {
                new() { OffsetMinutes = -10, AlertType = AdhanReminderAlertType.Notification },
                new() { OffsetMinutes = -5, AlertType = AdhanReminderAlertType.Adhan }
            },
            ReminderOffsetsMinutes = new List<int> { -30 }
        };

        var result = AdhanReminderResolver.Resolve(settings);

        Assert.Equal(2, result.Count);
        Assert.Equal(-10, result[0].OffsetMinutes);
        Assert.Equal(AdhanReminderAlertType.Notification, result[0].AlertType);
        Assert.Equal(-5, result[1].OffsetMinutes);
        Assert.Equal(AdhanReminderAlertType.Adhan, result[1].AlertType);
    }

    [Fact]
    public void Resolve_FallsBackToOffsets_WithAdhanType() {
        var settings = new NotificationSettings {
            ReminderItems = new List<AdhanReminderItem>(),
            ReminderOffsetsMinutes = new List<int> { -15, 10 }
        };

        var result = AdhanReminderResolver.Resolve(settings);

        Assert.Equal(2, result.Count);
        Assert.All(result, item => Assert.Equal(AdhanReminderAlertType.Adhan, item.AlertType));
        Assert.Equal(new[] { -15, 10 }, result.Select(item => item.OffsetMinutes).ToArray());
    }

    [Fact]
    public void Resolve_DedupesByOffsetAndType_AndFiltersZero() {
        var settings = new NotificationSettings {
            ReminderItems = new List<AdhanReminderItem> {
                new() { OffsetMinutes = -10, AlertType = AdhanReminderAlertType.Adhan },
                new() { OffsetMinutes = -10, AlertType = AdhanReminderAlertType.Adhan },
                new() { OffsetMinutes = -10, AlertType = AdhanReminderAlertType.Alarm },
                new() { OffsetMinutes = -10, AlertType = AdhanReminderAlertType.Notification },
                new() { OffsetMinutes = 0, AlertType = AdhanReminderAlertType.Silent }
            }
        };

        var result = AdhanReminderResolver.Resolve(settings);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, item => item.OffsetMinutes == -10 && item.AlertType == AdhanReminderAlertType.Adhan);
        Assert.Contains(result, item => item.OffsetMinutes == -10 && item.AlertType == AdhanReminderAlertType.Alarm);
        Assert.Contains(result, item => item.OffsetMinutes == -10 && item.AlertType == AdhanReminderAlertType.Notification);
        Assert.DoesNotContain(result, item => item.OffsetMinutes == 0);
    }

    [Fact]
    public void Resolve_PreservesAlarmEntries() {
        var settings = new NotificationSettings {
            ReminderItems = new List<AdhanReminderItem> {
                new() { OffsetMinutes = 4, AlertType = AdhanReminderAlertType.Alarm }
            }
        };

        var result = AdhanReminderResolver.Resolve(settings);

        var item = Assert.Single(result);
        Assert.Equal(4, item.OffsetMinutes);
        Assert.Equal(AdhanReminderAlertType.Alarm, item.AlertType);
    }
}
