using System.Text.Json;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests.Web;

public sealed class WebStateRpcTests {
    [Fact]
    public void ImportStateAcceptsThePublicStateProperty() {
        var source = new WebCoreRpcDispatcher();
        Dispatch(source, "app.setLanguage", new { language = "ar" });
        Dispatch(source, "app.setTheme", new { theme = "dark" });
        var exported = JsonSerializer.SerializeToElement(Dispatch(source, "app.exportState", new { })).GetString();

        var restored = new WebCoreRpcDispatcher();
        Dispatch(restored, "app.importState", new { state = exported });
        var shell = JsonSerializer.SerializeToElement(Dispatch(restored, "app.getShellSnapshot", new { }));

        Assert.Equal("ar", shell.GetProperty("language").GetString());
        Assert.Equal("dark", shell.GetProperty("themeMode").GetString());
    }

    [Fact]
    public void ManualCoordinatesDoNotKeepAnUnrelatedPlaceName() {
        var dispatcher = new WebCoreRpcDispatcher();
        var result = Dispatch(dispatcher, "settings.setField", new {
            section = "locations",
            field = "value",
            value = new {
                useGps = false,
                latitude = 55.0,
                longitude = 4.0,
                country = "GH",
                countryName = "Ghana",
                city = "Eastern Region"
            }
        });
        var response = JsonSerializer.SerializeToElement(result);
        var calculated = response.GetProperty("calculated");

        Assert.Equal(55.0, calculated.GetProperty("latitude").GetDouble());
        Assert.Equal(4.0, calculated.GetProperty("longitude").GetDouble());
        Assert.Equal(string.Empty, calculated.GetProperty("country").GetString());
        Assert.Equal(string.Empty, calculated.GetProperty("countryName").GetString());
        Assert.Equal(string.Empty, calculated.GetProperty("city").GetString());
    }

    [Fact]
    public void TodaySnapshotUsesPrayerIdsAndCoreLabels() {
        var dispatcher = new WebCoreRpcDispatcher();
        var result = JsonSerializer.SerializeToElement(Dispatch(dispatcher, "today.getSnapshot", new { }));

        Assert.True(result.TryGetProperty("nextPrayerId", out var nextPrayerId));
        Assert.False(string.IsNullOrWhiteSpace(nextPrayerId.GetString()));
        Assert.False(result.TryGetProperty("nextPrayerName", out _));
        Assert.True(result.TryGetProperty("nextPrayerDayId", out _));
        Assert.All(result.GetProperty("todayTimings").EnumerateArray(), timing => {
            Assert.True(timing.TryGetProperty("id", out _));
            Assert.False(timing.TryGetProperty("name", out _));
        });
        Assert.Equal("Asr", WebCatalog.Translate("en", "Prayer_Asr"));
        Assert.Equal("العصر", WebCatalog.Translate("ar", "Prayer_Asr"));
    }

    [Fact]
    public void MissingCoreLabelThrowsInsteadOfLeakingAKey() {
        Assert.Throws<InvalidOperationException>(() => WebCatalog.Translate("en", "ThisKeyDoesNotExist"));
    }

    private static object? Dispatch(WebCoreRpcDispatcher dispatcher, string method, object payload) {
        return dispatcher.Dispatch(method, JsonSerializer.SerializeToElement(payload));
    }
}
