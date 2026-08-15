using System.Text.Json.Serialization;

namespace PrayAdFree.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter<WidgetTemplateKind>))]
public enum WidgetTemplateKind { NextPrayer, DailyPrayer, Fasting, Tasbih, DateAndPrayer, QiblaBearing }

[JsonConverter(typeof(JsonStringEnumConverter<WidgetDensity>))]
public enum WidgetDensity { Auto, Compact, Standard, Detailed }

[JsonConverter(typeof(JsonStringEnumConverter<WidgetTextScale>))]
public enum WidgetTextScale { Auto, Small, Normal, Large, ExtraLarge }

[JsonConverter(typeof(JsonStringEnumConverter<WidgetPlatform>))]
public enum WidgetPlatform { Preview, Android, Ios, WindowsSystem, WindowsCompanion }

[JsonConverter(typeof(JsonStringEnumConverter<WidgetSurface>))]
public enum WidgetSurface { Home, LockScreen, Board, Desktop, Preview }

[JsonConverter(typeof(JsonStringEnumConverter<WidgetFamily>))]
public enum WidgetFamily { Inline, Circular, Tiny, Compact, Small, Rectangular, Medium, Large, Schedule }

public sealed record WidgetStyle {
    public WidgetTextScale TextScale { get; init; } = WidgetTextScale.Auto;
    public string PrimaryTextColor { get; init; } = "#FFFFFFFF";
    public string SecondaryTextColor { get; init; } = "#B8FFFFFF";
    public string BackgroundColor { get; init; } = "#FF06252B";
    public string AccentColor { get; init; } = "#FF2EC4A6";
    public int BackgroundOpacity { get; init; } = 92;
    public bool FollowAppTheme { get; init; } = true;
}

public sealed record WidgetPrivacy {
    public bool HideLocationOnLockScreen { get; init; } = true;
    public bool HideLocationSourceOnLockScreen { get; init; } = true;
}

public sealed record WidgetProfile {
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public WidgetTemplateKind Template { get; init; }
    public long Revision { get; init; } = 1;
    public WidgetDensity Density { get; init; } = WidgetDensity.Auto;
    public IReadOnlyList<string> Projection { get; init; } = [];
    public WidgetStyle Style { get; init; } = new();
    public WidgetPrivacy Privacy { get; init; } = new();
    public bool IsBuiltIn { get; init; }
}

public sealed record WidgetProfilePatch {
    public string? Name { get; init; }
    public WidgetDensity? Density { get; init; }
    public IReadOnlyList<string>? Projection { get; init; }
    public WidgetStyle? Style { get; init; }
    public WidgetPrivacy? Privacy { get; init; }
    public long? ExpectedRevision { get; init; }
}

public sealed record WidgetInstanceAssignment {
    public string InstanceId { get; init; } = "";
    public string ProfileId { get; init; } = "";
    public WidgetPlatform Platform { get; init; }
    public WidgetSurface Surface { get; init; }
    public WidgetFamily Family { get; init; }
    public int MinWidthDp { get; init; }
    public int MaxWidthDp { get; init; }
    public int MinHeightDp { get; init; }
    public int MaxHeightDp { get; init; }
}

public sealed record WidgetProfileDocument {
    public int SchemaVersion { get; init; } = 1;
    public long Revision { get; init; } = 1;
    public IReadOnlyList<WidgetProfile> Profiles { get; init; } = [];
    public IReadOnlyList<WidgetInstanceAssignment> Assignments { get; init; } = [];
}

public sealed record WidgetCatalogEntry(
    WidgetTemplateKind Template,
    string NameKey,
    IReadOnlyList<string> RequiredProjection,
    IReadOnlyList<string> DefaultProjection,
    IReadOnlyList<string> AllowedProjection);
