namespace PrayAdFree.Core.Models;

public sealed class AdhanReminderItem {
    public int OffsetMinutes { get; init; }
    public AdhanReminderAlertType AlertType { get; init; } = AdhanReminderAlertType.Adhan;
}
