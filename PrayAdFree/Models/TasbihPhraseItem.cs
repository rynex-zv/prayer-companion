using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Models;

public sealed class TasbihPresetItem {
    public TasbihPresetItem(string name, TasbihRepeatMode repeatMode, IReadOnlyList<TasbihItemSettings> items) {
        Name = name;
        RepeatMode = repeatMode;
        Items = items.ToList();
        TotalTarget = Items.Where(item => item.TargetCount > 0).Sum(item => item.TargetCount);
    }

    public string Name { get; }
    public TasbihRepeatMode RepeatMode { get; }
    public IReadOnlyList<TasbihItemSettings> Items { get; }
    public int TotalTarget { get; }
}

public sealed class TasbihPresetItemEntry {
    public TasbihPresetItemEntry(string text, int targetCount) {
        Text = text;
        TargetCount = targetCount;
    }

    public string Text { get; }
    public int TargetCount { get; }
}
