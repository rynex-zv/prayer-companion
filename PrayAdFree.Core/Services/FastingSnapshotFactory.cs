using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public sealed class FastingSnapshotFactory {
    public FastingSnapshot Build(PrayerDay today, PrayerDay? tomorrow, AppSettings settings, DateTime now) {
        ArgumentNullException.ThrowIfNull(today);
        ArgumentNullException.ThrowIfNull(settings);

        var imsak = today.Timings.Imsak.AddMinutes(-settings.FastingOffsets.ImsakAdvanceMinutes);
        var iftar = today.Timings.Maghrib.AddMinutes(settings.FastingOffsets.IftarDelayMinutes);
        var tomorrowImsak = tomorrow != null
            ? tomorrow.Timings.Imsak.AddMinutes(-settings.FastingOffsets.ImsakAdvanceMinutes)
            : imsak.AddDays(1);

        DateTime nextTarget;
        var isImsakNext = false;
        var isIftarNext = false;

        if (now < imsak) {
            isImsakNext = true;
            nextTarget = imsak;
        } else if (now < iftar) {
            isIftarNext = true;
            nextTarget = iftar;
        } else {
            isImsakNext = true;
            nextTarget = tomorrowImsak > now ? tomorrowImsak : imsak.AddDays(1);
        }

        var remaining = nextTarget - now;
        if (remaining < TimeSpan.Zero) {
            remaining = TimeSpan.Zero;
        }

        return new FastingSnapshot {
            ImsakTime = imsak,
            IftarTime = iftar,
            NextTargetTime = nextTarget,
            IsImsakNext = isImsakNext,
            IsIftarNext = isIftarNext,
            Remaining = remaining
        };
    }
}
