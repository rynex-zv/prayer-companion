using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public sealed class TasbihProgressCalculator {
    public int GetNextCount(TasbihPresetSettings preset, int currentCount) {
        ArgumentNullException.ThrowIfNull(preset);

        var totalTarget = GetTotalTarget(preset);
        if (totalTarget == 0) {
            return currentCount;
        }

        if (preset.RepeatMode == TasbihRepeatMode.None && currentCount >= totalTarget) {
            return currentCount;
        }

        var nextCount = currentCount + 1;
        if (preset.RepeatMode == TasbihRepeatMode.RepeatReset && nextCount >= totalTarget) {
            return 0;
        }

        return nextCount;
    }

    public TasbihProgressSnapshot BuildSnapshot(TasbihPresetSettings? preset, int count) {
        if (preset == null || preset.Items.Count == 0) {
            return new TasbihProgressSnapshot { IsEmpty = true };
        }

        var totalTarget = GetTotalTarget(preset);
        if (totalTarget == 0) {
            return new TasbihProgressSnapshot { IsEmpty = true };
        }

        var position = preset.RepeatMode == TasbihRepeatMode.RepeatContinue
            ? count % totalTarget
            : Math.Min(count, totalTarget);
        var running = 0;

        foreach (var item in preset.Items) {
            if (item.TargetCount <= 0) {
                continue;
            }

            var next = running + item.TargetCount;
            if (position < next) {
                return new TasbihProgressSnapshot {
                    CurrentText = item.Text,
                    LocalCount = position - running,
                    LocalTarget = item.TargetCount,
                    TotalTarget = totalTarget
                };
            }

            running = next;
        }

        var last = preset.Items.Last();
        return new TasbihProgressSnapshot {
            CurrentText = last.Text,
            LocalCount = last.TargetCount,
            LocalTarget = last.TargetCount,
            TotalTarget = totalTarget
        };
    }

    public int GetTotalTarget(TasbihPresetSettings preset) {
        ArgumentNullException.ThrowIfNull(preset);
        return preset.Items.Where(item => item.TargetCount > 0).Sum(item => item.TargetCount);
    }
}
