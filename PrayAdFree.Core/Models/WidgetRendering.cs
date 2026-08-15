namespace PrayAdFree.Core.Models;

public sealed record WidgetHostCapabilities {
    public WidgetPlatform Platform { get; init; } = WidgetPlatform.Preview;
    public WidgetSurface Surface { get; init; } = WidgetSurface.Preview;
    public WidgetFamily Family { get; init; } = WidgetFamily.Medium;
    public int WidthDp { get; init; } = 300;
    public int HeightDp { get; init; } = 180;
    public int MaxTextItems { get; init; } = 8;
    public int MaxActions { get; init; } = 2;
    public bool SupportsBackgroundColor { get; init; } = true;
    public bool SupportsBackgroundOpacity { get; init; } = true;
    public bool SupportsFullColor { get; init; } = true;
    public bool SupportsLiveCountdown { get; init; } = true;
    public bool IsAuthenticated { get; init; } = true;
}

public sealed record WidgetPrayerRow {
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Time { get; init; } = "";
    public long TargetUnixMilliseconds { get; init; }
    public bool IsNext { get; init; }
}

public sealed record WidgetProjection {
    public long GeneratedAtUnixMilliseconds { get; init; }
    public string Language { get; init; } = "en";
    public bool IsRtl { get; init; }
    public string Status { get; init; } = "ready";
    public string Error { get; init; } = "";
    public string LocationTitle { get; init; } = "";
    public string LocationSource { get; init; } = "";
    public string HijriDate { get; init; } = "";
    public string GregorianDate { get; init; } = "";
    public string NextPrayerId { get; init; } = "";
    public string NextPrayerName { get; init; } = "";
    public string NextPrayerTime { get; init; } = "";
    public long NextPrayerAtUnixMilliseconds { get; init; }
    public bool IsNextPrayerTomorrow { get; init; }
    public IReadOnlyList<WidgetPrayerRow> PrayerRows { get; init; } = [];
    public string ImsakTime { get; init; } = "";
    public string IftarTime { get; init; } = "";
    public string FastingTargetName { get; init; } = "";
    public long FastingTargetAtUnixMilliseconds { get; init; }
    public string TasbihPresetName { get; init; } = "";
    public string TasbihText { get; init; } = "";
    public int TasbihCount { get; init; }
    public int TasbihTarget { get; init; }
    public int QiblaBearingDegrees { get; init; }
}

public sealed record WidgetRenderText(string Key, string Text, string Role, bool Required, string AccessibilityLabel);
public sealed record WidgetRenderRow(string Key, string Label, string Value, bool Highlighted, string AccessibilityLabel);
public sealed record WidgetRenderAction(string Id, string Label, string DeepLink, string AccessibilityLabel);

public sealed record WidgetRenderTree {
    public string ProfileId { get; init; } = "";
    public long ProfileRevision { get; init; }
    public string Status { get; init; } = "ready";
    public string Error { get; init; } = "";
    public bool IsRtl { get; init; }
    public WidgetFamily Family { get; init; }
    public WidgetStyle Style { get; init; } = new();
    public IReadOnlyList<WidgetRenderText> Texts { get; init; } = [];
    public IReadOnlyList<WidgetRenderRow> Rows { get; init; } = [];
    public IReadOnlyList<WidgetRenderAction> Actions { get; init; } = [];
    public long? CountdownTargetUnixMilliseconds { get; init; }
    public double? Progress { get; init; }
    public IReadOnlyList<string> OmittedProjection { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed record WindowsAdaptiveCardTextBlock {
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string Type { get; init; } = "TextBlock";
    [System.Text.Json.Serialization.JsonPropertyName("text")]
    public string Text { get; init; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("size")]
    public string Size { get; init; } = "Default";
    [System.Text.Json.Serialization.JsonPropertyName("weight")]
    public string Weight { get; init; } = "Default";
    [System.Text.Json.Serialization.JsonPropertyName("wrap")]
    public bool Wrap { get; init; } = true;
}

public sealed record WindowsAdaptiveCardAction {
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string Type { get; init; } = "Action.OpenUrl";
    [System.Text.Json.Serialization.JsonPropertyName("title")]
    public string Title { get; init; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("url")]
    public string Url { get; init; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("tooltip")]
    public string Tooltip { get; init; } = "";
}

public sealed record WindowsAdaptiveCardDocument {
    [System.Text.Json.Serialization.JsonPropertyName("$schema")]
    public string Schema { get; init; } = "http://adaptivecards.io/schemas/adaptive-card.json";
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string Type { get; init; } = "AdaptiveCard";
    [System.Text.Json.Serialization.JsonPropertyName("version")]
    public string Version { get; init; } = "1.5";
    [System.Text.Json.Serialization.JsonPropertyName("rtl")]
    public bool Rtl { get; init; }
    [System.Text.Json.Serialization.JsonPropertyName("body")]
    public IReadOnlyList<WindowsAdaptiveCardTextBlock> Body { get; init; } = [];
    [System.Text.Json.Serialization.JsonPropertyName("actions")]
    public IReadOnlyList<WindowsAdaptiveCardAction> Actions { get; init; } = [];
}

public sealed record WindowsWidgetInstanceProjection {
    public string InstanceId { get; init; } = "";
    public string ProfileId { get; init; } = "";
    public long ProfileRevision { get; init; }
    public long UpdatedAtUnixMilliseconds { get; init; }
    public IReadOnlyDictionary<WidgetFamily, WidgetRenderTree> RenderTrees { get; init; } = new Dictionary<WidgetFamily, WidgetRenderTree>();
}

public sealed record WindowsWidgetProjectionBundle {
    public int SchemaVersion { get; init; } = 1;
    public long Revision { get; init; }
    public IReadOnlyDictionary<string, WindowsWidgetInstanceProjection> Instances { get; init; } = new Dictionary<string, WindowsWidgetInstanceProjection>();
}
