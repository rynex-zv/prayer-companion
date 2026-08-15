using System.Text.Json;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests.Widgets;

public sealed class WindowsAdaptiveCardWidgetRendererTests {
    [Theory]
    [InlineData(WidgetFamily.Small, 4)]
    [InlineData(WidgetFamily.Medium, 7)]
    [InlineData(WidgetFamily.Large, 12)]
    public void ProducesOfflineAdaptiveCardForEveryWindowsFamily(WidgetFamily family, int maxItems) {
        var tree = new WidgetRenderTree {
            ProfileId = "p",
            ProfileRevision = 2,
            Family = family,
            IsRtl = true,
            Texts = [new("name", "الفجر", "title", true, "الفجر")],
            Rows = [new("time", "الوقت", "04:30", true, "الوقت، 04:30")],
            Actions = [new("open", "فتح", "prayadfree://today", "فتح التطبيق")]
        };
        var json = new WindowsAdaptiveCardWidgetRenderer().Render(tree);
        using var document = JsonDocument.Parse(json);
        Assert.Equal("AdaptiveCard", document.RootElement.GetProperty("type").GetString());
        Assert.True(document.RootElement.GetProperty("rtl").GetBoolean());
        Assert.DoesNotContain("http://pray", json, StringComparison.OrdinalIgnoreCase);
        Assert.True(tree.Texts.Count + tree.Rows.Count <= maxItems);
    }

    [Fact]
    public void ErrorCardDoesNotInventData() {
        var json = new WindowsAdaptiveCardWidgetRenderer().Render(new WidgetRenderTree { Status = "error", Error = "No current projection" });
        Assert.Contains("No current projection", json);
        Assert.DoesNotContain("04:30", json);
    }
}
