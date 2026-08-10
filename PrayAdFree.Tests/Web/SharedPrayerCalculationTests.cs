using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests.Web;

public sealed class SharedPrayerCalculationTests {
    [Fact]
    public async Task Browser_factory_and_native_client_return_identical_prayer_times() {
        var settings = Settings(CalculationMethod.MuslimWorldLeague, PrayerOffsets.Default);
        var date = new DateOnly(2026, 8, 6);
        var factory = new WebPrayerMonthFactory();
        var browserDay = factory.BuildDay(settings, date);
        var nativeMonth = await new SharedCorePrayerTimesClient(factory)
            .GetMonthAsync(settings, date.Year, date.Month, CancellationToken.None);
        var nativeDay = Assert.Single(nativeMonth.Days, item => item.Date == date);

        Assert.Equal(WebPrayerMonthFactory.EngineId, nativeMonth.MethodKey.Split('-')[0]);
        Assert.Equal(browserDay.Timings.Fajr, nativeDay.Timings.Fajr);
        Assert.Equal(browserDay.Timings.Sunrise, nativeDay.Timings.Sunrise);
        Assert.Equal(browserDay.Timings.Dhuhr, nativeDay.Timings.Dhuhr);
        Assert.Equal(browserDay.Timings.Asr, nativeDay.Timings.Asr);
        Assert.Equal(browserDay.Timings.Maghrib, nativeDay.Timings.Maghrib);
        Assert.Equal(browserDay.Timings.Isha, nativeDay.Timings.Isha);
    }

    [Fact]
    public void Shared_engine_applies_the_same_user_offsets_exactly_once() {
        var factory = new WebPrayerMonthFactory();
        var date = new DateOnly(2026, 8, 6);
        var baseline = factory.BuildDay(Settings(CalculationMethod.MuslimWorldLeague, PrayerOffsets.Default), date);
        var adjusted = factory.BuildDay(Settings(CalculationMethod.MuslimWorldLeague, new PrayerOffsets {
            Fajr = 2, Sunrise = 3, Dhuhr = 4, Asr = 5, Maghrib = 6, Isha = 7, Imsak = 8
        }), date);

        Assert.Equal(baseline.Timings.Fajr.AddMinutes(2), adjusted.Timings.Fajr);
        Assert.Equal(baseline.Timings.Sunrise.AddMinutes(3), adjusted.Timings.Sunrise);
        Assert.Equal(baseline.Timings.Dhuhr.AddMinutes(4), adjusted.Timings.Dhuhr);
        Assert.Equal(baseline.Timings.Asr.AddMinutes(5), adjusted.Timings.Asr);
        Assert.Equal(baseline.Timings.Maghrib.AddMinutes(6), adjusted.Timings.Maghrib);
        Assert.Equal(baseline.Timings.Isha.AddMinutes(7), adjusted.Timings.Isha);
        Assert.Equal(baseline.Timings.Imsak.AddMinutes(8), adjusted.Timings.Imsak);
    }

    [Fact]
    public void Fasting_advance_is_applied_once_after_shared_calculation() {
        var settings = Settings(CalculationMethod.MuslimWorldLeague, PrayerOffsets.Default);
        var day = new WebPrayerMonthFactory().BuildDay(settings, new DateOnly(2026, 8, 6));
        var snapshot = new FastingSnapshotFactory().Build(day, null, settings, day.Timings.Fajr.AddHours(-1));

        Assert.Equal(day.Timings.Fajr.AddMinutes(-10), snapshot.ImsakTime);
    }

    [Fact]
    public void Portugal_and_Jordan_apply_their_documented_Maghrib_adjustments() {
        var factory = new WebPrayerMonthFactory();
        var date = new DateOnly(2026, 8, 6);
        var baseline = factory.BuildDay(Settings(CalculationMethod.Custom, PrayerOffsets.Default), date);
        var portugal = factory.BuildDay(Settings(CalculationMethod.Portugal, PrayerOffsets.Default), date);
        var jordan = factory.BuildDay(Settings(CalculationMethod.Jordan, PrayerOffsets.Default), date);

        Assert.Equal(baseline.Timings.Maghrib.AddMinutes(3), portugal.Timings.Maghrib);
        Assert.Equal(baseline.Timings.Maghrib.AddMinutes(5), jordan.Timings.Maghrib);
    }

    [Fact]
    public void UmmAlQura_adds_thirty_minutes_to_Isha_during_Ramadan() {
        var factory = new WebPrayerMonthFactory();
        var ramadanDate = Enumerable.Range(0, 366)
            .Select(offset => new DateOnly(2026, 1, 1).AddDays(offset))
            .First(date => factory.BuildDay(Settings(CalculationMethod.UmmAlQura, PrayerOffsets.Default), date).Hijri.Month == "Ramadan");
        var ummAlQura = factory.BuildDay(Settings(CalculationMethod.UmmAlQura, PrayerOffsets.Default), ramadanDate);
        var qatar = factory.BuildDay(Settings(CalculationMethod.Qatar, PrayerOffsets.Default), ramadanDate);

        Assert.Equal(qatar.Timings.Isha.AddMinutes(30), ummAlQura.Timings.Isha);
    }

    private static AppSettings Settings(CalculationMethod method, PrayerOffsets offsets) => new() {
        Location = new LocationSettings {
            City = "Amsterdam",
            Country = "Netherlands",
            CountryCode = "NL",
            Latitude = 52.3676,
            Longitude = 4.9041,
            TimeZoneId = TimeZoneInfo.Local.Id
        },
        Method = method,
        Madhhab = Madhhab.Shafi,
        HighLatitudeRule = HighLatitudeRule.MiddleOfTheNight,
        SunAngles = new SunAngleSettings { Fajr = 18, Isha = 17 },
        Offsets = offsets,
        FastingOffsets = new FastingOffsets { ImsakAdvanceMinutes = 10 }
    };
}
