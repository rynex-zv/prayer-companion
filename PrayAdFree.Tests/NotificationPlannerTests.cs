using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public sealed class NotificationPlannerTests {
    [Fact]
    public void BuildSchedule_Disabled_ReturnsEmpty() {
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
        var settings = new NotificationSettings { EnableAdhan = false };
        var planner = new PrayerSchedulePlanner();

        var schedule = planner.BuildSchedule(day, settings);

        Assert.Empty(schedule);
    }

    [Fact]
    public void BuildSchedule_IncludesFivePrayers() {
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

        Assert.Equal(5, schedule.Count);
        Assert.Contains(schedule, item => item.Prayer == PrayerId.Fajr);
        Assert.Contains(schedule, item => item.Prayer == PrayerId.Dhuhr);
        Assert.Contains(schedule, item => item.Prayer == PrayerId.Asr);
        Assert.Contains(schedule, item => item.Prayer == PrayerId.Maghrib);
        Assert.Contains(schedule, item => item.Prayer == PrayerId.Isha);
    }
}
