using System.Collections.Generic;

namespace PrayAdFree.Core.Models;

public sealed class TasbihPresetSettings {
    public string Name { get; init; } = "";
    public TasbihRepeatMode RepeatMode { get; init; } = TasbihRepeatMode.None;
    public List<TasbihItemSettings> Items { get; init; } = new();
}
