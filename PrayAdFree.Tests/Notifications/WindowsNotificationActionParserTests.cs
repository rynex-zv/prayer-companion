using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public sealed class WindowsNotificationActionParserTests {
    [Theory]
    [InlineData("adhan_control")]
    [InlineData("source=adhan_control")]
    [InlineData("foo=1&source=adhan_control")]
    [InlineData("stop_adhan")]
    [InlineData("action=stop_adhan")]
    [InlineData("foo=1&action=stop_adhan")]
    public void ShouldStopAdhan_WhenArgumentContainsControlOrAction(string argument) {
        Assert.True(WindowsNotificationActionParser.ShouldStopAdhan(argument));
    }

    [Fact]
    public void ShouldStopAdhan_WhenDictionaryContainsSourceOrAction() {
        var sourceArgs = new Dictionary<string, string?> { ["source"] = "adhan_control" };
        var actionArgs = new Dictionary<string, string?> { ["action"] = "stop_adhan" };

        Assert.True(WindowsNotificationActionParser.ShouldStopAdhan(string.Empty, sourceArgs));
        Assert.True(WindowsNotificationActionParser.ShouldStopAdhan(string.Empty, actionArgs));
    }

    [Fact]
    public void ShouldNotStopAdhan_ForUnrelatedArguments() {
        var args = new Dictionary<string, string?> { ["source"] = "other" };

        Assert.False(WindowsNotificationActionParser.ShouldStopAdhan("hello=world", args));
    }
}
