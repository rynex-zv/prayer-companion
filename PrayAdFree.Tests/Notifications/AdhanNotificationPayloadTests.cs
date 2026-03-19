using PrayAdFree.Core.Models;

namespace PrayAdFree.Tests;

public sealed class AdhanNotificationPayloadTests {
    [Fact]
    public void BuildPlay_TryParse_Roundtrip() {
        var payload = AdhanNotificationPayload.BuildPlay(PrayerId.Dhuhr, "adhan_default");

        var ok = AdhanNotificationPayload.TryParse(payload, out var parsed);

        Assert.True(ok);
        Assert.Equal(PrayerId.Dhuhr, parsed.Prayer);
        Assert.Equal("adhan_default", parsed.SoundKey);
    }

    [Fact]
    public void TryParse_RejectsEmpty() {
        var ok = AdhanNotificationPayload.TryParse("", out _);
        Assert.False(ok);
    }

    [Fact]
    public void TryParse_RejectsInvalidFormat() {
        var ok = AdhanNotificationPayload.TryParse("bad|format", out _);
        Assert.False(ok);
    }
}
