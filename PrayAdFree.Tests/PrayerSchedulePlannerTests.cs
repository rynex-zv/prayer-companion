using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public class PrayerSchedulePlannerTests {
    [Fact]
    public void BuildSchedule_UsesAdjustedPrayerTimesForMainAdhan() {
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
        var settings = new NotificationSettings { EnableAdhan = true, MinutesBefore = 10 };
        var planner = new PrayerSchedulePlanner();

        var schedule = planner.BuildSchedule(day, settings);

        Assert.Contains(schedule, item => item.Prayer == PrayerId.Fajr && item.Time == DateTime.Today.AddHours(5));
        Assert.DoesNotContain(schedule, item => item.Prayer == PrayerId.Fajr && item.Time == DateTime.Today.AddHours(4).AddMinutes(50));
    }
}
