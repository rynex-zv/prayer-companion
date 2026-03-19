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

public enum QiblaDirectionMode {
    CompassOnly,
    LocationOnly,
    Both
}

public enum QiblaHeadingMode {
    Sensor,
    Manual
}

public sealed class QiblaPreferences {
    public QiblaReadingMode ReadingMode { get; init; } = QiblaReadingMode.Balanced;
    public QiblaFilterMode FilterMode { get; init; } = QiblaFilterMode.Normal;
    public QiblaDirectionMode DirectionMode { get; init; } = QiblaDirectionMode.Both;
    public QiblaHeadingMode HeadingMode { get; init; } = QiblaHeadingMode.Sensor;
    public double ManualHeading { get; init; }
}
