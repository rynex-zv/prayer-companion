using System.Text.Json;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests.Web;

public sealed class WebStateRpcTests {
    [Fact]
    public void Deterministic_engine_persists_state_and_revision_only_through_explicit_output() {
        var commandPayload = JsonSerializer.SerializeToElement(new {
            language = "ar",
            _rpc = new { requestId = "request-1", domain = "app" }
        });
        var changed = WebCoreExecutionEngine.Execute(null, "app.setLanguage", commandPayload);
        var shell = WebCoreExecutionEngine.Execute(changed.State, "app.getShellSnapshot", JsonSerializer.SerializeToElement(new { }));
        var untouched = WebCoreExecutionEngine.Execute(null, "app.getShellSnapshot", JsonSerializer.SerializeToElement(new { }));

        Assert.Equal("ar", JsonSerializer.SerializeToElement(shell.Data).GetProperty("language").GetString());
        Assert.Equal("en", JsonSerializer.SerializeToElement(untouched.Data).GetProperty("language").GetString());
        Assert.Single(changed.Events);
        Assert.Equal(1, changed.Events[0].Revision);

        var notModified = WebCoreExecutionEngine.Execute(changed.State, "app.getShellSnapshot", JsonSerializer.SerializeToElement(new {
            _rpc = new { domain = "app" },
            _query = new { ifRevision = 1 }
        }));
        Assert.True(JsonSerializer.SerializeToElement(notModified.Data).GetProperty("notModified").GetBoolean());
    }

    [Fact]
    public void Deterministic_engine_imports_legacy_web_state_without_global_dispatcher_state() {
        var legacy = WebState.Default();
        legacy.Language = "tr";
        var state = JsonSerializer.Serialize(legacy);

        var result = WebCoreExecutionEngine.Execute(state, "app.getShellSnapshot", JsonSerializer.SerializeToElement(new { }));

        Assert.Equal("tr", JsonSerializer.SerializeToElement(result.Data).GetProperty("language").GetString());
        Assert.Contains("\"State\"", result.State, StringComparison.Ordinal);
        Assert.Contains("\"Revision\"", result.State, StringComparison.Ordinal);
    }

    [Fact]
    public void Language_object_query_is_pure_and_does_not_replace_authoritative_language() {
        var initial = WebCoreExecutionEngine.Execute(null, "app.setLanguage", JsonSerializer.SerializeToElement(new { language = "en" }));
        var query = WebCoreExecutionEngine.Execute(initial.State, "app.getLanguageObject", JsonSerializer.SerializeToElement(new { language = "fr" }));
        var shell = WebCoreExecutionEngine.Execute(query.State, "app.getShellSnapshot", JsonSerializer.SerializeToElement(new { }));

        Assert.Equal("fr", JsonSerializer.SerializeToElement(query.Data).GetProperty("code").GetString());
        Assert.Equal("en", JsonSerializer.SerializeToElement(shell.Data).GetProperty("language").GetString());
        Assert.Equal(initial.State, query.State);
    }

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
