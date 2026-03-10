using System;
using System.Collections.Generic;
using System.Linq;
using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public static class AdhanReminderResolver {
    public static IReadOnlyList<AdhanReminderItem> Resolve(NotificationSettings settings) {
        var source = settings.ReminderItems;
        if (source == null || source.Count == 0) {
            source = settings.ReminderOffsetsMinutes
                .Select(offset => new AdhanReminderItem {
                    OffsetMinutes = offset,
                    AlertType = AdhanReminderAlertType.Adhan
                })
                .ToList();
        }

        return source
            .Where(item => item.OffsetMinutes != 0)
            .GroupBy(item => (item.OffsetMinutes, item.AlertType))
            .Select(group => group.First())
            .OrderBy(item => item.OffsetMinutes)
            .ThenBy(item => item.AlertType)
            .ToList();
    }
}
