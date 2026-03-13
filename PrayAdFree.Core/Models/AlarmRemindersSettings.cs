using System.Collections.Generic;

namespace PrayAdFree.Core.Models;

public sealed class AlarmRemindersSettings {
    public List<string> DisabledBuiltInIds { get; init; } = new();
    public List<AlarmUserReminderItem> UserItems { get; init; } = new();
}
