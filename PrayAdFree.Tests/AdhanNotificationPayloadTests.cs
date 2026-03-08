using PrayAdFree.Core.Models;

namespace PrayAdFree.Tests;

public class AdhanNotificationPayloadTests {
    [Fact]
    public void BuildPlay_AndTryParse_RoundTrips() {
        var raw = AdhanNotificationPayload.BuildPlay(PrayerId.Asr, "adhan_builtin_03");

        var ok = AdhanNotificationPayload.TryParse(raw, out var payload);

        Assert.True(ok);
        Assert.Equal(PrayerId.Asr, payload.Prayer);
        Assert.Equal("adhan_builtin_03", payload.SoundKey);
    }

    [Fact]
    public void BuildPlay_Escapes_SoundKey() {
        var raw = AdhanNotificationPayload.BuildPlay(PrayerId.Fajr, "adhan custom/01");

        var ok = AdhanNotificationPayload.TryParse(raw, out var payload);

        Assert.True(ok);
        Assert.Equal("adhan custom/01", payload.SoundKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("play|bad|adhan_builtin_01")]
    [InlineData("play|1|")]
    public void TryParse_Invalid_ReturnsFalse(string raw) {
        var ok = AdhanNotificationPayload.TryParse(raw, out var payload);

        Assert.False(ok);
        Assert.Equal(default, payload);
    }
}
