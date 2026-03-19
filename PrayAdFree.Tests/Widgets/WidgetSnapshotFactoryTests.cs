using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public class WidgetSnapshotFactoryTests {
    [Fact]
    public void DailyPrayerSnapshotFactory_UsesOffsetsAndMarksNextPrayer() {
        var now = DateTime.Today.AddHours(4).AddMinutes(45);
        var settings = new AppSettings {
            Offsets = new PrayerOffsets {
                Fajr = 5,
                Sunrise = 0,
                Dhuhr = 0,
                Asr = 0,
                Maghrib = 0,
                Isha = 0,
                Imsak = 0
            }
        };
        var day = new PrayerDay {
            Date = DateOnly.FromDateTime(now),
            Timings = new PrayerTimings {
                Fajr = DateTime.Today.AddHours(5),
                Sunrise = DateTime.Today.AddHours(6),
                Dhuhr = DateTime.Today.AddHours(12),
                Asr = DateTime.Today.AddHours(15),
                Maghrib = DateTime.Today.AddHours(18),
                Isha = DateTime.Today.AddHours(19),
                Imsak = DateTime.Today.AddHours(4).AddMinutes(30)
            }
        };

        var snapshot = new DailyPrayerSnapshotFactory().Build(day, settings, now);

        Assert.Equal(PrayerId.Fajr, snapshot.NextPrayerId);
        Assert.Equal(DateTime.Today.AddHours(5), snapshot.NextPrayerTime);
        Assert.Equal(DateTime.Today.AddHours(4).AddMinutes(55), snapshot.NextPrayerBaseTime);
        Assert.Contains(snapshot.Entries, item => item.Prayer == PrayerId.Fajr && item.ShowBaseTime && item.IsNext);
    }

    [Fact]
    public void FastingSnapshotFactory_AfterIftar_TargetsTomorrowImsak() {
        var now = DateTime.Today.AddHours(22);
        var settings = new AppSettings {
            FastingOffsets = new FastingOffsets {
                ImsakAdvanceMinutes = 10,
                IftarDelayMinutes = 5
            }
        };
        var today = new PrayerDay {
            Date = DateOnly.FromDateTime(now),
            Timings = new PrayerTimings {
                Fajr = DateTime.Today.AddHours(5),
                Sunrise = DateTime.Today.AddHours(6),
                Dhuhr = DateTime.Today.AddHours(12),
                Asr = DateTime.Today.AddHours(15),
                Maghrib = DateTime.Today.AddHours(18),
                Isha = DateTime.Today.AddHours(19),
                Imsak = DateTime.Today.AddHours(4).AddMinutes(40)
            }
        };
        var tomorrow = new PrayerDay {
            Date = DateOnly.FromDateTime(now.AddDays(1)),
            Timings = new PrayerTimings {
                Fajr = DateTime.Today.AddDays(1).AddHours(5),
                Sunrise = DateTime.Today.AddDays(1).AddHours(6),
                Dhuhr = DateTime.Today.AddDays(1).AddHours(12),
                Asr = DateTime.Today.AddDays(1).AddHours(15),
                Maghrib = DateTime.Today.AddDays(1).AddHours(18),
                Isha = DateTime.Today.AddDays(1).AddHours(19),
                Imsak = DateTime.Today.AddDays(1).AddHours(4).AddMinutes(35)
            }
        };

        var snapshot = new FastingSnapshotFactory().Build(today, tomorrow, settings, now);

        Assert.True(snapshot.IsImsakNext);
        Assert.False(snapshot.IsIftarNext);
        Assert.Equal(DateTime.Today.AddDays(1).AddHours(4).AddMinutes(25), snapshot.NextTargetTime);
    }

    [Fact]
    public void WidgetSnapshotFactory_BuildsPrayerAndFastingSnapshotsTogether() {
        var now = DateTime.Today.AddHours(10);
        var settings = new AppSettings();
        var today = new PrayerDay {
            Date = DateOnly.FromDateTime(now),
            Timings = new PrayerTimings {
                Fajr = DateTime.Today.AddHours(5),
                Sunrise = DateTime.Today.AddHours(6),
                Dhuhr = DateTime.Today.AddHours(12),
                Asr = DateTime.Today.AddHours(15),
                Maghrib = DateTime.Today.AddHours(18),
                Isha = DateTime.Today.AddHours(19),
                Imsak = DateTime.Today.AddHours(4).AddMinutes(30)
            }
        };

        var result = new WidgetSnapshotFactory().Build(today, null, settings, now);

        Assert.Equal(PrayerId.Dhuhr, result.DailyPrayer.NextPrayerId);
        Assert.True(result.Fasting.IsIftarNext);
    }
}
