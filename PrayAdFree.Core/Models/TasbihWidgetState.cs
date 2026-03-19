namespace PrayAdFree.Core.Models;

public sealed class TasbihWidgetState {
    public int AppWidgetId { get; init; }
    public int PresetIndex { get; init; }
    public int Count { get; init; }
    public DateTime LastUpdatedUtc { get; init; }
}
