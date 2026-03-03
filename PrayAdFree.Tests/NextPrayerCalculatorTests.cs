using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public class NextPrayerCalculatorTests {
    [Fact]
    public void GetNext_ReturnsNextPrayer() {
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

        var (id, time) = NextPrayerCalculator.GetNext(day, DateTime.Today.AddHours(11));
        Assert.Equal(PrayerId.Dhuhr, id);
        Assert.Equal(DateTime.Today.AddHours(12), time);
    }
}
