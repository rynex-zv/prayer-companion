using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public class NextPrayerCalculatorTests {
    [Fact]
    public void GetNext_ReturnsChronologicalNearestPrayer() {
        var date = DateOnly.FromDateTime(DateTime.Today);
        var timings = new PrayerTimings {
            Fajr = DateTime.Today.AddHours(21).AddMinutes(50),
            Sunrise = DateTime.Today.AddHours(7),
            Dhuhr = DateTime.Today.AddHours(12),
            Asr = DateTime.Today.AddHours(15),
            Maghrib = DateTime.Today.AddHours(18),
            Isha = DateTime.Today.AddHours(21).AddMinutes(30),
            Imsak = DateTime.Today.AddHours(21).AddMinutes(40)
        };
        var day = new PrayerDay { Date = date, Timings = timings };

        var (id, time) = NextPrayerCalculator.GetNext(day, DateTime.Today.AddHours(20).AddMinutes(45));

        Assert.Equal(PrayerId.Isha, id);
        Assert.Equal(DateTime.Today.AddHours(21).AddMinutes(30), time);
    }

    [Fact]
    public void GetNext_DoesNotFallbackToFajrWhenCloserPrayerExistsAfterMidnightBoundaryCase() {
        var date = DateOnly.FromDateTime(DateTime.Today);
        var timings = new PrayerTimings {
            Fajr = DateTime.Today.AddHours(21).AddMinutes(50),
            Sunrise = DateTime.Today.AddHours(7).AddMinutes(7),
            Dhuhr = DateTime.Today.AddHours(12).AddMinutes(51),
            Asr = DateTime.Today.AddHours(15).AddMinutes(52),
            Maghrib = DateTime.Today.AddHours(18).AddMinutes(57),
            Isha = DateTime.Today.AddHours(20).AddMinutes(3),
            Imsak = DateTime.Today.AddHours(21).AddMinutes(40)
        };
        var day = new PrayerDay { Date = date, Timings = timings };

        var now = DateTime.Today.AddHours(22);
        var (id, time) = NextPrayerCalculator.GetNext(day, now);

        Assert.Equal(PrayerId.Sunrise, id);
        Assert.Equal(DateTime.Today.AddDays(1).AddHours(7).AddMinutes(7), time);
    }

    [Fact]
    public void GetNext_AdvancesSamePrayerToNextDayWhenAlreadyPassed() {
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
        Assert.Equal(DateTime.Today.AddDays(1).AddHours(5), time);
    }
}
