using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public static class WidgetHostCapabilityResolver {
    public static WidgetHostCapabilities ResolveAndroid(
        int minWidthDp,
        int maxWidthDp,
        int minHeightDp,
        int maxHeightDp,
        bool isKeyguard) {
        var width = Math.Max(0, Math.Max(minWidthDp, maxWidthDp));
        var height = Math.Max(0, Math.Max(minHeightDp, maxHeightDp));
        var family = width < 120 || height < 90
            ? WidgetFamily.Tiny
            : width < 180 || height < 120
                ? WidgetFamily.Compact
                : width < 250 || height < 180
                    ? WidgetFamily.Medium
                    : WidgetFamily.Large;
        var maxItems = family switch {
            WidgetFamily.Tiny => 2,
            WidgetFamily.Compact => 4,
            WidgetFamily.Medium => 7,
            _ => 12
        };
        return new WidgetHostCapabilities {
            Platform = WidgetPlatform.Android,
            Surface = isKeyguard ? WidgetSurface.LockScreen : WidgetSurface.Home,
            Family = family,
            WidthDp = width,
            HeightDp = height,
            MaxTextItems = maxItems,
            MaxActions = family == WidgetFamily.Tiny ? 1 : 2,
            SupportsLiveCountdown = true,
            IsAuthenticated = true
        };
    }
}
