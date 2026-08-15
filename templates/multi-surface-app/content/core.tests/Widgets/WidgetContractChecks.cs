using TemplateApp.Core.Widgets;

namespace TemplateApp.CoreTests.Widgets;

public sealed class WidgetContractChecks {
    [Fact]
    public void HostCapacityAndLockPrivacyAreEnforced() {
        var profile = new WidgetProfile("p", "Profile", "summary", 1, WidgetDensity.Auto,
            ["title", "location", "time"], ["title", "time"], WidgetTextScale.Auto,
            "#FFFFFFFF", "#CCFFFFFF", "#FF001122", 100, "#FF00AA88", true, true);
        var projection = new WidgetProjection(0, "ready", "", new Dictionary<string, string> {
            ["title"] = "Next event", ["location"] = "Private place", ["time"] = "12:30"
        }, [], 1);
        var host = new WidgetHostCapabilities("test", "lock-screen", WidgetFamily.Compact, 160, 100, 2, 1, true, true, true);
        var tree = new WidgetLayoutResolver().Resolve(profile, projection, host);
        Assert.True(tree.Items.Count <= host.MaxItems);
        Assert.DoesNotContain(tree.Items, item => item.Key == "location");
        Assert.Empty(tree.Omitted);
    }

    [Theory]
    [InlineData(1, "error")]
    [InlineData(2, "ready")]
    public void RequiredContentNeverDisappearsSilently(int capacity, string expectedStatus) {
        var profile = new WidgetProfile("p", "Profile", "summary", 1, WidgetDensity.Auto,
            ["title", "time"], ["title", "time"], WidgetTextScale.Auto,
            "#FFFFFFFF", "#CCFFFFFF", "#FF001122", 100, "#FF00AA88", true, true);
        var projection = new WidgetProjection(0, "ready", "", new Dictionary<string, string> {
            ["title"] = "Next event", ["time"] = "12:30"
        }, [], 1);
        var host = new WidgetHostCapabilities("test", "home", WidgetFamily.Compact, 160, 100, capacity, 1, true, true, true);

        var tree = new WidgetLayoutResolver().Resolve(profile, projection, host);

        Assert.Equal(expectedStatus, tree.Status);
        if (tree.Status == "error") Assert.Equal("required-content-overflow", tree.Error);
    }
}
