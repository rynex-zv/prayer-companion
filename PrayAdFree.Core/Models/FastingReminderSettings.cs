using System.Collections.Generic;

namespace PrayAdFree.Core.Models;

public sealed class FastingReminderSettings {
    public List<int> ImsakRemindersMinutes { get; init; } = new();
    public List<int> IftarRemindersMinutes { get; init; } = new();

    public static FastingReminderSettings Default => new FastingReminderSettings();
}
