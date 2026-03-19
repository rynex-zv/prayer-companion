using System.Globalization;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public sealed class CalendarMonthPresenterTests {
    private readonly CalendarMonthPresenter _presenter = new();

    [Fact]
    public void NormalizeMonth_ReturnsFirstDayOfSelectedMonth() {
        var normalized = _presenter.NormalizeMonth(new DateTime(2026, 3, 19));

        Assert.Equal(new DateTime(2026, 3, 1), normalized);
    }

    [Theory]
    [InlineData(2026, 3, 19, -1, 2026, 2, 1)]
    [InlineData(2026, 3, 19, 1, 2026, 4, 1)]
    [InlineData(2026, 1, 31, -1, 2025, 12, 1)]
    public void MoveMonth_ShiftsWholeMonths(int year, int month, int day, int offset, int expectedYear, int expectedMonth, int expectedDay) {
        var shifted = _presenter.MoveMonth(new DateTime(year, month, day), offset);

        Assert.Equal(new DateTime(expectedYear, expectedMonth, expectedDay), shifted);
    }

    [Fact]
    public void BuildRows_WhenSelectedMonthChanges_ReturnsRowsForNewMonth() {
        var settings = new AppSettings {
            ClockFormat = ClockFormat.TwentyFourHour,
            Offsets = new PrayerOffsets {
                Fajr = 5,
                Dhuhr = -3
            }
        };

        var marchRows = _presenter.BuildRows(
            new PrayerMonth {
                Year = 2026,
                Month = 3,
                Days = new[] {
                    CreateDay(new DateOnly(2026, 3, 19), "19", "Ramadan", "1447", 5, 10),
                    CreateDay(new DateOnly(2026, 3, 20), "20", "Ramadan", "1447", 5, 9)
                }
            },
            settings,
            CultureInfo.InvariantCulture);

        var aprilRows = _presenter.BuildRows(
            new PrayerMonth {
                Year = 2026,
                Month = 4,
                Days = new[] {
                    CreateDay(new DateOnly(2026, 4, 1), "12", "Shawwal", "1447", 4, 52)
                }
            },
            settings,
            CultureInfo.InvariantCulture);

        Assert.Equal(2, marchRows.Count);
        Assert.Equal(new DateOnly(2026, 3, 19), marchRows[0].SourceDate);
        Assert.Equal("19 Mar", marchRows[0].Date);
        Assert.Equal("19 Ramadan 1447", marchRows[0].Hijri);
        Assert.Equal("05:10", marchRows[0].Fajr);
        Assert.Equal("05:05", marchRows[0].FajrBase);
        Assert.Equal("12:03", marchRows[0].DhuhrBase);
        Assert.True(marchRows[0].ShowFajrBase);
        Assert.True(marchRows[0].ShowDhuhrBase);

        Assert.Single(aprilRows);
        Assert.Equal(new DateOnly(2026, 4, 1), aprilRows[0].SourceDate);
        Assert.Equal("01 Apr", aprilRows[0].Date);
        Assert.Equal("12 Shawwal 1447", aprilRows[0].Hijri);
        Assert.Equal("04:52", aprilRows[0].Fajr);
    }

    private static PrayerDay CreateDay(
        DateOnly date,
        string hijriDay,
        string hijriMonth,
        string hijriYear,
        int fajrHour,
        int fajrMinute) {
        var baseDate = new DateTime(date.Year, date.Month, date.Day);

        return new PrayerDay {
            Date = date,
            Hijri = new HijriDate {
                Day = hijriDay,
                Month = hijriMonth,
                Year = hijriYear
            },
            Timings = new PrayerTimings {
                Fajr = baseDate.AddHours(fajrHour).AddMinutes(fajrMinute),
                Sunrise = baseDate.AddHours(6).AddMinutes(30),
                Dhuhr = baseDate.AddHours(12),
                Asr = baseDate.AddHours(15).AddMinutes(15),
                Maghrib = baseDate.AddHours(18).AddMinutes(20),
                Isha = baseDate.AddHours(19).AddMinutes(40),
                Imsak = baseDate.AddHours(fajrHour).AddMinutes(fajrMinute - 10)
            }
        };
    }
}
