namespace PrayAdFree.Core.Models;

public enum QiblaReadingMode {
    Smooth,
    Balanced,
    Fast,
    Raw
}

public enum QiblaFilterMode {
    Off,
    Normal,
    Strict
}

public sealed class QiblaPreferences {
    public QiblaReadingMode ReadingMode { get; init; } = QiblaReadingMode.Balanced;
    public QiblaFilterMode FilterMode { get; init; } = QiblaFilterMode.Normal;
}
