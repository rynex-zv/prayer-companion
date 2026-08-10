using System.Text.Json;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests.Web;

public sealed class AppFallbackRejectionTests {
    private static readonly DateOnly TestDate = new(2026, 8, 6);

    [Fact]
    public void Prayer_calculation_rejects_unknown_time_zone_instead_of_using_device_zone() {
        var settings = Settings(CalculationMethod.MuslimWorldLeague, timeZoneId: "Invalid/Not-A-Time-Zone");

        var error = Assert.Throws<ArgumentException>(() => new WebPrayerMonthFactory().BuildDay(settings, TestDate));

        Assert.Contains("time-zone", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Automatic_calculation_rejects_unknown_country_instead_of_using_MWL() {
        var settings = Settings(CalculationMethod.Auto, countryCode: "ZZ");

        var error = Assert.Throws<ArgumentException>(() => new WebPrayerMonthFactory().BuildDay(settings, TestDate));

        Assert.Contains("country code 'ZZ'", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prayer_calculation_rejects_undefined_method_instead_of_using_MWL() {
        var settings = Settings((CalculationMethod)123456);

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new WebPrayerMonthFactory().BuildDay(settings, TestDate));

        Assert.Contains("calculationMethod", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prayer_calculation_rejects_undefined_high_latitude_rule_instead_of_using_middle_of_night() {
        var settings = Settings(CalculationMethod.MuslimWorldLeague, highLatitudeRule: (HighLatitudeRule)123456);

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new WebPrayerMonthFactory().BuildDay(settings, TestDate));

        Assert.Contains("highLatitudeRule", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(CalculationMethod.Jafari)]
    [InlineData(CalculationMethod.Tehran)]
    public void Methods_requiring_a_Maghrib_angle_are_not_approximated(CalculationMethod method) {
        Assert.DoesNotContain(method, CalculationMethodPresetCatalog.SupportedMethods);
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WebPrayerMonthFactory().BuildDay(Settings(method), TestDate));
        Assert.Contains("not supported", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Display_preset_rejects_unknown_method_instead_of_showing_MWL_values() {
        var error = Assert.Throws<InvalidOperationException>(() =>
            CalculationMethodPresetCatalog.ResolvePreset(Settings((CalculationMethod)123456)));
        Assert.Contains("No verified display preset", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Qibla_commands_reject_unknown_modes_instead_of_using_compass_or_none() {
        var dispatcher = new WebCoreRpcDispatcher();

        var displayError = Assert.Throws<ArgumentException>(() =>
            dispatcher.Dispatch("qibla.setDisplayMode", Json("""{"mode":"teleport"}""")));
        var filterError = Assert.Throws<ArgumentException>(() =>
            dispatcher.Dispatch("qibla.setVisualFilter", Json("""{"mode":"blur-everything"}""")));

        Assert.Contains("qibla.displayMode", displayError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("qibla.visualFilter", filterError.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Settings_query_rejects_unknown_section_instead_of_returning_full_snapshot() {
        var dispatcher = new WebCoreRpcDispatcher();

        var error = Assert.Throws<ArgumentException>(() =>
            dispatcher.Dispatch("settings.getSnapshot", Json("""{"section":"secretFallbackPage"}""")));

        Assert.Contains("Unknown settings section", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Tasbih_selection_rejects_unknown_id_instead_of_returning_unchanged_success() {
        var dispatcher = new WebCoreRpcDispatcher();

        var error = Assert.Throws<ArgumentException>(() =>
            dispatcher.Dispatch("tasbih.selectPreset", Json("""{"id":"does-not-exist"}""")));

        Assert.Contains("Unknown Tasbih preset ID", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("tasbih.updatePreset", "{\"id\":\"missing\",\"name\":\"x\"}")]
    [InlineData("tasbih.addItem", "{\"presetId\":\"missing\",\"text\":\"x\",\"targetCount\":1}")]
    [InlineData("tasbih.updateItem", "{\"presetId\":\"missing\",\"index\":0,\"text\":\"x\"}")]
    [InlineData("tasbih.removeItem", "{\"presetId\":\"missing\",\"index\":0}")]
    public void Tasbih_mutations_reject_unknown_targets(string method, string payload) {
        var dispatcher = new WebCoreRpcDispatcher();
        Assert.ThrowsAny<ArgumentException>(() => dispatcher.Dispatch(method, Json(payload)));
    }

    [Theory]
    [InlineData("app.setLanguage", "{\"language\":42}")]
    [InlineData("app.setLanguage", "{\"language\":\"xx\"}")]
    [InlineData("app.setTheme", "{\"theme\":\"invisible\"}")]
    [InlineData("qibla.updateHeading", "{\"heading\":\"north\"}")]
    [InlineData("tasbih.addItem", "{\"presetId\":\"after-prayer\",\"text\":\"x\",\"targetCount\":0}")]
    public void Typed_commands_reject_invalid_present_values(string method, string payload) {
        var dispatcher = new WebCoreRpcDispatcher();
        Assert.ThrowsAny<ArgumentException>(() => dispatcher.Dispatch(method, Json(payload)));
    }

    [Fact]
    public void Settings_patch_rejects_unknown_field_instead_of_returning_success() {
        var dispatcher = new WebCoreRpcDispatcher();
        var error = Assert.Throws<ArgumentException>(() => dispatcher.Dispatch(
            "settings.update",
            Json("""{"section":"adhan","field":"pretendSaved","value":true}""")));
        Assert.Contains("Unsupported settings patch", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Corrupt_persisted_web_state_is_not_replaced_with_Amsterdam_defaults() {
        var error = Assert.Throws<InvalidDataException>(() =>
            WebCoreExecutionEngine.Execute("{broken-json", "app.bootstrap", Json("{}")));
        Assert.Contains("not replaced", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static AppSettings Settings(
        CalculationMethod method,
        string countryCode = "NL",
        string? timeZoneId = null,
        HighLatitudeRule highLatitudeRule = HighLatitudeRule.MiddleOfTheNight) => new() {
        Location = new LocationSettings {
            City = "Amsterdam",
            Country = "Netherlands",
            CountryCode = countryCode,
            Latitude = 52.3676,
            Longitude = 4.9041,
            TimeZoneId = timeZoneId ?? TimeZoneInfo.Local.Id
        },
        Method = method,
        Madhhab = Madhhab.Shafi,
        HighLatitudeRule = highLatitudeRule,
        SunAngles = new SunAngleSettings { Fajr = 18, Isha = 17 },
        Offsets = PrayerOffsets.Default,
        FastingOffsets = new FastingOffsets { ImsakAdvanceMinutes = 10 }
    };
}
