namespace PrayAdFree.Core.Models;

public sealed class AlarmUserReminderItem {
    public string Id { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public bool IsEnabled { get; init; } = true;
}
