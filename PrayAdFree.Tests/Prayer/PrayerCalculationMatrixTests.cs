using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
using Batoulapps.Adhan.Internal;

namespace PrayAdFree.Tests.Prayer;

public sealed class PrayerCalculationMatrixTests {
    private readonly WebPrayerMonthFactory _factory = new();

    public static IEnumerable<object[]> SupportedMethods() =>
        CalculationMethodPresetCatalog.SupportedMethods.Select(method => new object[] { method });

    [Theory]
    [MemberData(nameof(SupportedMethods))]
    public void Every_exposed_method_produces_ordered_finite_times(CalculationMethod method) {
        var day = _factory.BuildDay(Settings(method, "Europe/Amsterdam"), new DateOnly(2026, 8, 6));
        var values = new[] {
            day.Timings.Fajr, day.Timings.Sunrise, day.Timings.Dhuhr,
            day.Timings.Asr, day.Timings.Maghrib, day.Timings.Isha
        };

        Assert.All(values, value => Assert.InRange(value.Date, new DateTime(2026, 8, 6), new DateTime(2026, 8, 7)));
        Assert.True(values.Zip(values.Skip(1), (left, right) => left < right).All(value => value),
            $"{method} produced unordered times: {string.Join(", ", values.Select(value => value.ToString("HH:mm")))}");
    }

    [Fact]
    public void Auto_method_requires_a_known_country_instead_of_silent_substitution() {
        var settings = Settings(CalculationMethod.Auto, "Europe/Amsterdam", countryCode: "XX");

        Assert.Throws<ArgumentException>(() => _factory.BuildDay(settings, new DateOnly(2026, 8, 6)));
    }

    [Fact]
    public void Unknown_timezone_is_rejected_instead_of_using_device_timezone() {
        Assert.Throws<ArgumentException>(() =>
            _factory.BuildDay(Settings(CalculationMethod.MuslimWorldLeague, "Invalid/NoSuchZone"), new DateOnly(2026, 8, 6)));
    }

    [Theory]
    [InlineData(2026, 3, 29)]
    [InlineData(2026, 10, 25)]
    public void Amsterdam_dst_transition_converts_each_prayer_instant_independently(int year, int month, int day) {
        var date = new DateOnly(year, month, day);
        var settings = Settings(CalculationMethod.MuslimWorldLeague, "Europe/Amsterdam");
        var result = _factory.BuildDay(settings, date);
        Assert.Equal(DateTimeKind.Unspecified, result.Timings.Fajr.Kind);
        Assert.True(result.Timings.Fajr < result.Timings.Sunrise);
        Assert.True(result.Timings.Sunrise < result.Timings.Dhuhr);
    }

    [Theory]
    [InlineData(HighLatitudeRule.MiddleOfTheNight)]
    [InlineData(HighLatitudeRule.SeventhOfTheNight)]
    [InlineData(HighLatitudeRule.TwilightAngle)]
    public void High_latitude_rules_keep_Oslo_schedule_ordered(HighLatitudeRule rule) {
        var settings = Settings(
            CalculationMethod.MuslimWorldLeague,
            "Europe/Oslo",
            countryCode: "NO",
            latitude: 59.9139,
            longitude: 10.7522,
            highLatitudeRule: rule);
        var day = _factory.BuildDay(settings, new DateOnly(2026, 6, 21));

        Assert.True(day.Timings.Fajr < day.Timings.Sunrise);
        Assert.True(day.Timings.Maghrib < day.Timings.Isha);
    }

    [Theory]
    [InlineData(CalculationMethod.Jafari, 4.0)]
    [InlineData(CalculationMethod.Tehran, 4.5)]
    public void Shia_methods_use_their_exact_Maghrib_solar_angle(CalculationMethod method, double angle) {
        var date = new DateOnly(2026, 8, 6);
        var settings = Settings(method, "Europe/Amsterdam");
        var result = _factory.BuildDay(settings, date);
        var coordinates = new Batoulapps.Adhan.Coordinates(settings.Location.Latitude, settings.Location.Longitude);
        var utcDate = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var decimalHours = new Batoulapps.Adhan.Internal.SolarTime(utcDate, coordinates)
            .HourAngle(-angle, afterTransit: true);
        var expectedUtc = utcDate.AddHours(decimalHours)
            .Round(TimeSpan.FromMinutes(1));
        var actualUtc = TimeZoneInfo.ConvertTimeToUtc(result.Timings.Maghrib, TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam"));

        Assert.Equal(expectedUtc, actualUtc);
    }

    private static AppSettings Settings(
        CalculationMethod method,
        string timeZoneId,
        string countryCode = "NL",
        double latitude = 52.3676,
        double longitude = 4.9041,
        HighLatitudeRule highLatitudeRule = HighLatitudeRule.MiddleOfTheNight) => new() {
        Location = new LocationSettings {
            City = "Test city", Country = "Test country", CountryCode = countryCode,
            Latitude = latitude, Longitude = longitude, TimeZoneId = timeZoneId
        },
        Method = method,
        Madhhab = Madhhab.Shafi,
        HighLatitudeRule = highLatitudeRule,
        SunAngles = new SunAngleSettings { Fajr = 18, Isha = 17 },
        Offsets = PrayerOffsets.Default,
        FastingOffsets = new FastingOffsets { ImsakAdvanceMinutes = 10 }
    };
}
