namespace TemplateApp.Core.Widgets;

public enum WidgetDensity { Auto, Compact, Standard, Detailed }
public enum WidgetTextScale { Auto, Small, Normal, Large, ExtraLarge }
public enum WidgetFamily { Tiny, Compact, Small, Medium, Large, Inline, Circular, Rectangular }

public sealed record WidgetProfile(
    string Id,
    string Name,
    string Template,
    long Revision,
    WidgetDensity Density,
    IReadOnlyList<string> Projection,
    IReadOnlyList<string> RequiredProjection,
    WidgetTextScale TextScale,
    string PrimaryTextColor,
    string SecondaryTextColor,
    string BackgroundColor,
    int BackgroundOpacity,
    string AccentColor,
    bool FollowTheme,
    bool HidePrivateDataOnLockScreen);

public sealed record WidgetHostCapabilities(
    string Platform,
    string Surface,
    WidgetFamily Family,
    int Width,
    int Height,
    int MaxItems,
    int MaxActions,
    bool SupportsColor,
    bool SupportsOpacity,
    bool SupportsCountdown);

public sealed record WidgetProjection(
    long GeneratedAtUnixMilliseconds,
    string Status,
    string Error,
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Rows,
    long? TargetUnixMilliseconds);

public sealed record WidgetRenderItem(string Kind, string Key, string Label, string Value, string AccessibilityLabel);
public sealed record WidgetRenderTree(
    string ProfileId,
    long Revision,
    string Status,
    string Error,
    WidgetFamily Family,
    IReadOnlyList<WidgetRenderItem> Items,
    IReadOnlyList<string> Omitted,
    long? TargetUnixMilliseconds);
