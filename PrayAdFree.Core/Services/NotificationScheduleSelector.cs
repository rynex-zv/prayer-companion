namespace PrayAdFree.Core.Services;

public sealed record PlannedNotification(
    int NotificationId,
    DateTime NotifyTime,
    string Title,
    string Description,
    string ReturningData,
    bool PlayAdhan);

public static class NotificationScheduleSelector {
    private static readonly TimeSpan DueGrace = TimeSpan.FromSeconds(15);

    public static IReadOnlyList<PlannedNotification> Normalize(IEnumerable<PlannedNotification> source, DateTime nowLocal) {
        return source
            .Where(item => item.NotifyTime >= nowLocal - DueGrace)
            .Where(item => !string.IsNullOrWhiteSpace(item.Title))
            .Where(item => !string.IsNullOrWhiteSpace(item.Description))
            .GroupBy(item => new {
                item.NotificationId,
                item.NotifyTime,
                item.Title,
                item.Description,
                item.ReturningData,
                item.PlayAdhan
            })
            .Select(group => group.First())
            .OrderBy(item => item.NotifyTime)
            .ThenBy(item => item.NotificationId)
            .ToList();
    }

    public static PlannedNotification? NextForWindowsQueue(IEnumerable<PlannedNotification> source, DateTime nowLocal) {
        return Normalize(source, nowLocal).FirstOrDefault();
    }
}
