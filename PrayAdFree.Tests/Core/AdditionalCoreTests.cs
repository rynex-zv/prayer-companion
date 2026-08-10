using System.Globalization;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public class AdditionalCoreTests {
    [Fact]
    public void PrayerSchedulePlanner_DoesNotIncludeImsakOrSunrise() {
        var timings = new PrayerTimings {
            Fajr = DateTime.Today.AddHours(5),
            Sunrise = DateTime.Today.AddHours(6),
            Dhuhr = DateTime.Today.AddHours(12),
            Asr = DateTime.Today.AddHours(15),
            Maghrib = DateTime.Today.AddHours(18),
            Isha = DateTime.Today.AddHours(19),
            Imsak = DateTime.Today.AddHours(4).AddMinutes(30)
        };
        var day = new PrayerDay { Date = DateOnly.FromDateTime(DateTime.Today), Timings = timings };
        var settings = new NotificationSettings { EnableAdhan = true };
        var planner = new PrayerSchedulePlanner();

        var schedule = planner.BuildSchedule(day, settings);

        Assert.DoesNotContain(schedule, item => item.Prayer == PrayerId.Imsak);
        Assert.DoesNotContain(schedule, item => item.Prayer == PrayerId.Sunrise);
    }

    [Fact]
    public void MethodResolver_IsCaseInsensitive() {
        var method = MethodResolver.ResolveRequired("sa");
        Assert.Equal(CalculationMethod.UmmAlQura, method);
    }

    [Fact]
    public void NextPrayerCalculator_AfterIsha_ReturnsNextDayFajr() {
        var date = DateOnly.FromDateTime(DateTime.Today);
        var timings = new PrayerTimings {
            Fajr = DateTime.Today.AddHours(5),
            Sunrise = DateTime.Today.AddHours(6),
            Dhuhr = DateTime.Today.AddHours(12),
            Asr = DateTime.Today.AddHours(15),
            Maghrib = DateTime.Today.AddHours(18),
            Isha = DateTime.Today.AddHours(19),
            Imsak = DateTime.Today.AddHours(4).AddMinutes(30)
        };
        var day = new PrayerDay { Date = date, Timings = timings };

        var (id, time) = NextPrayerCalculator.GetNext(day, DateTime.Today.AddHours(23));

        Assert.Equal(PrayerId.Fajr, id);
        Assert.Equal(DateTime.Today.AddDays(1).Date.AddHours(5), time);
    }

    [Fact]
    public void NextPrayerCalculator_AfterFajr_ReturnsSunrise() {
        var date = DateOnly.FromDateTime(DateTime.Today);
        var timings = new PrayerTimings {
            Fajr = DateTime.Today.AddHours(5),
            Sunrise = DateTime.Today.AddHours(6),
            Dhuhr = DateTime.Today.AddHours(12),
            Asr = DateTime.Today.AddHours(15),
            Maghrib = DateTime.Today.AddHours(18),
            Isha = DateTime.Today.AddHours(19),
            Imsak = DateTime.Today.AddHours(4).AddMinutes(30)
        };
        var day = new PrayerDay { Date = date, Timings = timings };

        var (id, time) = NextPrayerCalculator.GetNext(day, DateTime.Today.AddHours(5).AddMinutes(10));

        Assert.Equal(PrayerId.Sunrise, id);
        Assert.Equal(DateTime.Today.AddHours(6), time);
    }

    [Fact]
    public void PrayerTimings_ParseLocalDateTime_UsesTimezoneOffset() {
        var date = new DateOnly(2025, 1, 15);
        var timeZone = TimeZoneInfo.Utc;

        var result = PrayerTimings.ParseLocalDateTime(date, "05:30", timeZone);

        var local = new DateTime(2025, 1, 15, 5, 30, 0, DateTimeKind.Unspecified);
        var expected = TimeZoneInfo.ConvertTime(local, timeZone, TimeZoneInfo.Local);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PrayerTimings_Get_ThrowsOnUnknownPrayer() {
        var timings = new PrayerTimings();
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => timings.Get((PrayerId)999));
        Assert.Contains("Unknown prayer id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
