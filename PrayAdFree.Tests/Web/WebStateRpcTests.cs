using System.Text.Json;
using System.Diagnostics;
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
    public void Corrupt_persisted_browser_state_is_not_replaced_with_a_default_location() {
        var error = Assert.Throws<InvalidDataException>(() => WebCoreExecutionEngine.Execute(
            "{ definitely-not-json", "app.getShellSnapshot", JsonSerializer.SerializeToElement(new { })));
        Assert.Contains("not replaced", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FreshBrowserStateDoesNotInventAmsterdamLocation() {
        var dispatcher = new WebCoreRpcDispatcher();
        var location = JsonSerializer.SerializeToElement(Dispatch(dispatcher, "settings.getSnapshot", new { section = "locations" }));
        var today = JsonSerializer.SerializeToElement(Dispatch(dispatcher, "today.getSnapshot", new { }));

        Assert.Equal(string.Empty, location.GetProperty("country").GetString());
        Assert.Equal(string.Empty, location.GetProperty("countryName").GetString());
        Assert.Equal(string.Empty, location.GetProperty("city").GetString());
        Assert.Equal(0, location.GetProperty("latitude").GetDouble());
        Assert.Equal(0, location.GetProperty("longitude").GetDouble());
        Assert.DoesNotContain("Amsterdam", today.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.True(today.TryGetProperty("error", out _));
    }

    [Fact]
    public void ManualCoordinatesDoNotKeepAnUnrelatedPlaceName() {
        var dispatcher = new WebCoreRpcDispatcher();
        var result = Dispatch(dispatcher, "settings.update", new {
            section = "locations",
            field = "value",
            value = new {
                useGps = false,
                latitude = 55.0,
                longitude = 4.0,
                country = "NL",
                countryName = "Netherlands",
                city = "Amsterdam"
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
    public void GpsReverseGeocodedLocationIsUsedByTodayInsteadOfAmsterdamFallback() {
        var dispatcher = new WebCoreRpcDispatcher();
        Dispatch(dispatcher, "settings.update", new {
            _platform = new { timeZoneId = "Asia/Dubai" },
            section = "locations",
            field = "value",
            value = new {
                useGps = true,
                latitude = 25.3085386,
                longitude = 55.3648474,
                country = "AE",
                countryName = "United Arab Emirates",
                city = "Sharjah"
            }
        });

        var location = JsonSerializer.SerializeToElement(Dispatch(dispatcher, "settings.getSnapshot", new { section = "locations" }));
        Assert.Equal("AE", location.GetProperty("country").GetString());
        Assert.Equal("United Arab Emirates", location.GetProperty("countryName").GetString());
        Assert.Equal("Sharjah", location.GetProperty("city").GetString());

        var today = JsonSerializer.SerializeToElement(Dispatch(dispatcher, "today.getSnapshot", new { }));
        Assert.Equal("Sharjah, United Arab Emirates", today.GetProperty("locationTitle").GetString());
        Assert.Equal("Dubai", today.GetProperty("calculation").GetProperty("effectiveMethod").GetString());
        Assert.DoesNotContain("Amsterdam", today.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IpLocationKeepsApiLocationAndWarnsAboutNetworkAccuracy() {
        var dispatcher = new WebCoreRpcDispatcher();
        Dispatch(dispatcher, "settings.update", new {
            _platform = new { timeZoneId = "Europe/Amsterdam" },
            section = "locations",
            field = "value",
            value = new {
                useGps = false,
                latitude = 25.3085368,
                longitude = 55.3648479,
                timeZoneId = "Asia/Dubai",
                locationSource = "ip",
                country = "AE",
                countryName = "United Arab Emirates",
                city = "Sharjah"
            }
        });

        var location = JsonSerializer.SerializeToElement(Dispatch(dispatcher, "settings.getSnapshot", new { section = "locations" }));

        Assert.False(location.GetProperty("useGps").GetBoolean());
        Assert.Equal("ip", location.GetProperty("locationSource").GetString());
        Assert.True(location.GetProperty("vpnWarning").GetBoolean());
        Assert.Equal("AE", location.GetProperty("country").GetString());
        Assert.Equal("United Arab Emirates", location.GetProperty("countryName").GetString());
        Assert.Equal("Sharjah", location.GetProperty("city").GetString());
        Assert.Equal("Asia/Dubai", location.GetProperty("timeZoneId").GetString());
        Assert.NotEqual("NL", location.GetProperty("country").GetString());
        Assert.NotEqual("Netherlands", location.GetProperty("countryName").GetString());
        Assert.NotEqual("Amsterdam", location.GetProperty("city").GetString());
    }

    [Fact]
    public void BootstrapCleansPersistedCatalogPlaceWhenCoordinatesNoLongerMatch() {
        var stale = WebState.Default();
        stale.UseGps = true;
        stale.CountryCode = "NL";
        stale.Country = "Netherlands";
        stale.City = "Amsterdam";
        stale.Latitude = 25.3085386;
        stale.Longitude = 55.3648474;
        stale.TimeZoneId = "Asia/Dubai";
        var envelope = JsonSerializer.Serialize(new WebExecutionState(
            stale,
            new AppRevision(0, new Dictionary<string, long>(), 0)));

        var result = WebCoreExecutionEngine.Execute(envelope, "app.bootstrap", JsonSerializer.SerializeToElement(new { }));
        var restored = JsonSerializer.Deserialize<WebExecutionState>(result.State)!;
        var bootstrap = JsonSerializer.SerializeToElement(result.Data);
        var today = bootstrap.GetProperty("projections").GetProperty("today");

        Assert.Equal(string.Empty, restored.State.CountryCode);
        Assert.Equal(string.Empty, restored.State.Country);
        Assert.Equal(string.Empty, restored.State.City);
        Assert.DoesNotContain("Amsterdam", today.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.True(today.TryGetProperty("error", out _));
    }

    [Fact]
    public void Settings_mutations_return_and_persist_complete_confirmed_projections() {
        var dispatcher = new WebCoreRpcDispatcher();
        var adhan = Dispatch(dispatcher, "settings.update", new {
            section = "adhan", field = "value", value = new {
                sounds = Array.Empty<object>(), volume = 80, calculationMethod = "Auto",
                calculationMethods = Array.Empty<object>(), madhhab = "Shafi", madhhabs = Array.Empty<object>(),
                highLatitudeRule = "MiddleOfTheNight", highLatitudeRules = Array.Empty<object>(),
                fajrAngle = 18, ishaAngle = 17, isCustomMethod = false,
                offsets = new { fajr = 0, sunrise = 0, dhuhr = 0, asr = 0, maghrib = 0, isha = 0, imsak = 0 },
                clockFormat = "24h", fasting = new { iftarDelay = 0, imsakAdvance = 10 },
                imsakReminders = new[] { new { value = 10, unit = "minute", direction = "before" } },
                iftarReminders = Array.Empty<object>(), perPrayerOverrides = Array.Empty<object>()
            }
        });
        var confirmed = JsonSerializer.SerializeToElement(adhan).GetProperty("projection");
        Assert.Single(confirmed.GetProperty("imsakReminders").EnumerateArray());
        var reread = JsonSerializer.SerializeToElement(Dispatch(dispatcher, "settings.getSnapshot", new { section = "adhan" }));
        Assert.Single(reread.GetProperty("imsakReminders").EnumerateArray());
        Assert.Equal(WebPrayerMonthFactory.EngineId, reread.GetProperty("calculationEngine").GetString());

        var notifications = JsonSerializer.SerializeToElement(Dispatch(dispatcher, "settings.getSnapshot", new { section = "notifications" }));
        Assert.Equal(5, notifications.GetProperty("reminderPrayers").GetArrayLength());
        Assert.Equal(2, notifications.GetProperty("reminderScopes").GetArrayLength());
    }

    [Fact]
    public void Deterministic_same_device_data_calls_stay_below_300ms() {
        var dispatcher = new WebCoreRpcDispatcher();
        foreach (var (method, payload) in new (string, object)[] {
            ("app.bootstrap", new { }),
            ("settings.getSnapshot", new { section = "locations" }),
            ("tasbih.getSnapshot", new { }),
            ("tasbih.addPreset", new { name = "Performance fixture" }),
            ("tasbih.removePreset", new { id = "performance-fixture" }),
            ("onboarding.complete", new { })
        }) {
            var timer = Stopwatch.StartNew();
            Dispatch(dispatcher, method, payload);
            timer.Stop();
            Assert.True(timer.ElapsedMilliseconds < 300, $"{method} took {timer.ElapsedMilliseconds}ms");
        }
    }

    [Fact]
    public void Tasbih_preset_create_and_delete_return_complete_confirmed_collections() {
        var dispatcher = new WebCoreRpcDispatcher();
        var created = JsonSerializer.SerializeToElement(Dispatch(dispatcher, "tasbih.addPreset", new { name = "Temporary preset" }));
        var createdPresets = created.GetProperty("presets").EnumerateArray().ToArray();
        var createdPreset = Assert.Single(createdPresets, preset => preset.GetProperty("name").GetString() == "Temporary preset");
        Assert.Equal(createdPreset.GetProperty("id").GetString(), created.GetProperty("selectedPresetId").GetString());

        var deleted = JsonSerializer.SerializeToElement(Dispatch(dispatcher, "tasbih.removePreset", new { id = createdPreset.GetProperty("id").GetString() }));
        Assert.DoesNotContain(deleted.GetProperty("presets").EnumerateArray(), preset => preset.GetProperty("name").GetString() == "Temporary preset");
        Assert.NotEqual(createdPreset.GetProperty("id").GetString(), deleted.GetProperty("selectedPresetId").GetString());
    }

    [Fact]
    public void Tasbih_mutations_return_the_updated_repeat_mode_and_item_projection() {
        var dispatcher = new WebCoreRpcDispatcher();
        var created = JsonSerializer.SerializeToElement(Dispatch(dispatcher, "tasbih.addPreset", new { name = "Editable preset" }));
        var id = created.GetProperty("selectedPresetId").GetString();

        var updatedPreset = JsonSerializer.SerializeToElement(Dispatch(dispatcher, "tasbih.updatePreset", new {
            id,
            name = "Renamed preset",
            repeatMode = "Reset"
        }));
        var preset = Assert.Single(updatedPreset.GetProperty("presets").EnumerateArray(), item => item.GetProperty("id").GetString() == id);
        Assert.Equal("Renamed preset", preset.GetProperty("name").GetString());
        Assert.Equal("Reset", preset.GetProperty("repeatMode").GetString());

        var addedItem = JsonSerializer.SerializeToElement(Dispatch(dispatcher, "tasbih.addItem", new {
            presetId = id,
            text = "Second item",
            targetCount = 7
        }));
        preset = Assert.Single(addedItem.GetProperty("presets").EnumerateArray(), item => item.GetProperty("id").GetString() == id);
        Assert.Contains(preset.GetProperty("items").EnumerateArray(), item =>
            item.GetProperty("text").GetString() == "Second item" && item.GetProperty("targetCount").GetInt32() == 7);

        var updatedItem = JsonSerializer.SerializeToElement(Dispatch(dispatcher, "tasbih.updateItem", new {
            presetId = id,
            index = 1,
            text = "Edited item",
            targetCount = 9
        }));
        preset = Assert.Single(updatedItem.GetProperty("presets").EnumerateArray(), item => item.GetProperty("id").GetString() == id);
        var item = preset.GetProperty("items")[1];
        Assert.Equal("Edited item", item.GetProperty("text").GetString());
        Assert.Equal(9, item.GetProperty("targetCount").GetInt32());
    }

    [Fact]
    public void TodaySnapshotUsesPrayerIdsAndCoreLabels() {
        var dispatcher = new WebCoreRpcDispatcher();
        Dispatch(dispatcher, "settings.update", new {
            _platform = new { timeZoneId = "Asia/Dubai" },
            section = "locations",
            field = "value",
            value = new {
                useGps = false,
                latitude = 25.2048,
                longitude = 55.2708,
                timeZoneId = "Asia/Dubai",
                country = "AE",
                countryName = "United Arab Emirates",
                city = "Dubai"
            }
        });
        var result = JsonSerializer.SerializeToElement(Dispatch(dispatcher, "today.getSnapshot", new { }));

        Assert.True(result.TryGetProperty("nextPrayerId", out var nextPrayerId));
        Assert.False(string.IsNullOrWhiteSpace(nextPrayerId.GetString()));
        Assert.False(result.TryGetProperty("nextPrayerName", out _));
        Assert.True(result.TryGetProperty("nextPrayerDayId", out _));
        Assert.True(result.TryGetProperty("nextPrayerAt", out var nextPrayerAt));
        Assert.True(nextPrayerAt.GetInt64() > 0);
        Assert.All(result.GetProperty("todayTimings").EnumerateArray(), timing => {
            Assert.True(timing.TryGetProperty("id", out _));
            Assert.True(timing.TryGetProperty("timestamp", out var timestamp));
            Assert.True(timestamp.GetInt64() > 0);
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
