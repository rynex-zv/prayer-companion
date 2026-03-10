using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public sealed class NotificationScheduleSelectorTests {
    [Fact]
    public void Normalize_DedupesAndFiltersInvalidOrExpiredEntries() {
        var now = new DateTime(2026, 3, 10, 20, 36, 0, DateTimeKind.Local);
        var source = new[] {
            new PlannedNotification(1, now.AddMinutes(5), "title", "body", "", false),
            new PlannedNotification(1, now.AddMinutes(5), "title", "body", "", false),
            new PlannedNotification(2, now.AddMinutes(6), "", "body", "", false),
            new PlannedNotification(3, now.AddMinutes(7), "title", "", "", false),
            new PlannedNotification(4, now.AddSeconds(-30), "late", "late", "", false)
        };

        var result = NotificationScheduleSelector.Normalize(source, now);

        Assert.Single(result);
        Assert.Equal(1, result[0].NotificationId);
    }

    [Fact]
    public void Normalize_KeepsNearDueItemWithinGraceWindow() {
        var now = new DateTime(2026, 3, 10, 20, 36, 0, DateTimeKind.Local);
        var source = new[] {
            new PlannedNotification(1, now.AddSeconds(-5), "near", "due", "", false)
        };

        var result = NotificationScheduleSelector.Normalize(source, now);

        Assert.Single(result);
        Assert.Equal(1, result[0].NotificationId);
    }

    [Fact]
    public void NextForWindowsQueue_ReturnsNearestValidFutureItem() {
        var now = new DateTime(2026, 3, 10, 20, 36, 0, DateTimeKind.Local);
        var source = new[] {
            new PlannedNotification(4, now.AddMinutes(20), "later", "later", "", false),
            new PlannedNotification(2, now.AddMinutes(5), "next", "item", "", false),
            new PlannedNotification(1, now.AddMinutes(3), "", "invalid", "", false)
        };

        var result = NotificationScheduleSelector.NextForWindowsQueue(source, now);

        Assert.NotNull(result);
        Assert.Equal(2, result!.NotificationId);
    }
}
