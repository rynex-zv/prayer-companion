using PrayAdFree.Core.Models;

namespace PrayAdFree.Tests;

public sealed class AdhanAlarmPayloadTests {
    [Fact]
    public void Build_TryParse_Roundtrip() {
        var baseTime = new DateTime(2026, 3, 13, 5, 10, 0, DateTimeKind.Local);
        var notifyTime = baseTime.AddMinutes(12);
        var value = AdhanAlarmPayload.Build(PrayerId.Fajr, "adhan_default", baseTime, notifyTime);

        var ok = AdhanAlarmPayload.TryParse(value, out var parsed);

        Assert.True(ok);
        Assert.Equal(PrayerId.Fajr, parsed.Prayer);
        Assert.Equal("adhan_default", parsed.SoundKey);
        Assert.Equal(baseTime, parsed.BasePrayerTime);
        Assert.Equal(notifyTime, parsed.NotifyTime);
    }

    [Fact]
    public void Build_UsesDefaultSound_WhenEmpty() {
        var value = AdhanAlarmPayload.Build(
            PrayerId.Asr,
            "",
            new DateTime(2026, 3, 13, 15, 0, 0, DateTimeKind.Local),
            new DateTime(2026, 3, 13, 15, 9, 0, DateTimeKind.Local));

        var ok = AdhanAlarmPayload.TryParse(value, out var parsed);

        Assert.True(ok);
        Assert.Equal("adhan_default", parsed.SoundKey);
    }

    [Fact]
    public void TryParse_RejectsInvalidFormat() {
        Assert.False(AdhanAlarmPayload.TryParse("play|1|adhan_default", out _));
        Assert.False(AdhanAlarmPayload.TryParse("alarm|abc|x|1|2", out _));
        Assert.False(AdhanAlarmPayload.TryParse("", out _));
    }
}
