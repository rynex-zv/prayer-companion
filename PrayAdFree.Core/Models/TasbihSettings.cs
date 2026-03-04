using System.Collections.Generic;

namespace PrayAdFree.Core.Models;

public sealed class TasbihSettings {
    public List<TasbihPresetSettings> Presets { get; init; } = new();
    public int SelectedPresetIndex { get; init; }
}
