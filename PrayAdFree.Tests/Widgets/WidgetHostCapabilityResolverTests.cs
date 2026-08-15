using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests.Widgets;

public sealed class WidgetHostCapabilityResolverTests {
    [Theory]
    [InlineData(80, 80, WidgetFamily.Tiny, 2, 1)]
    [InlineData(140, 100, WidgetFamily.Compact, 4, 2)]
    [InlineData(220, 150, WidgetFamily.Medium, 7, 2)]
    [InlineData(320, 220, WidgetFamily.Large, 12, 2)]
    public void AndroidSizeUsesActualHostSpace(int width, int height, WidgetFamily expectedFamily, int expectedItems, int expectedActions) {
        var result = WidgetHostCapabilityResolver.ResolveAndroid(width, width, height, height, false);
        Assert.Equal(expectedFamily, result.Family);
        Assert.Equal(expectedItems, result.MaxTextItems);
        Assert.Equal(expectedActions, result.MaxActions);
    }

    [Fact]
    public void AndroidLandscapeUsesMaximumReportedDimensions() {
        var result = WidgetHostCapabilityResolver.ResolveAndroid(140, 320, 100, 160, false);
        Assert.Equal(320, result.WidthDp);
        Assert.Equal(160, result.HeightDp);
        Assert.Equal(WidgetFamily.Medium, result.Family);
    }

    [Fact]
    public void AndroidKeyguardIsARealLockScreenSurface() {
        var result = WidgetHostCapabilityResolver.ResolveAndroid(220, 220, 150, 150, true);
        Assert.Equal(WidgetSurface.LockScreen, result.Surface);
    }
}
