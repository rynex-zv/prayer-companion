using System.Globalization;
using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public sealed class CalendarMonthPresenter {
    public DateTime NormalizeMonth(DateTime date) => new(date.Year, date.Month, 1);

    public DateTime MoveMonth(DateTime selectedMonth, int offsetMonths) =>
        NormalizeMonth(selectedMonth).AddMonths(offsetMonths);

    public IReadOnlyList<CalendarDayRow> BuildRows(
        PrayerMonth month,
        AppSettings settings,
        CultureInfo? culture = null) {
        culture ??= CultureInfo.CurrentCulture;

        return month.Days
            .Select(day => new CalendarDayRow {
                SourceDate = day.Date,
                Date = day.Date.ToDateTime(TimeOnly.MinValue).ToString("dd MMM", culture),
                Hijri = day.Hijri.Date,

                Fajr = FormatTime(day.Timings.Fajr, settings.ClockFormat, culture),
                FajrBase = FormatTime(day.Timings.Fajr.AddMinutes(-settings.Offsets.Fajr), settings.ClockFormat, culture),
                ShowFajrBase = settings.Offsets.Fajr != 0,
                Sunrise = FormatTime(day.Timings.Sunrise, settings.ClockFormat, culture),

                Dhuhr = FormatTime(day.Timings.Dhuhr, settings.ClockFormat, culture),
                DhuhrBase = FormatTime(day.Timings.Dhuhr.AddMinutes(-settings.Offsets.Dhuhr), settings.ClockFormat, culture),
                ShowDhuhrBase = settings.Offsets.Dhuhr != 0,

                Asr = FormatTime(day.Timings.Asr, settings.ClockFormat, culture),
                AsrBase = FormatTime(day.Timings.Asr.AddMinutes(-settings.Offsets.Asr), settings.ClockFormat, culture),
                ShowAsrBase = settings.Offsets.Asr != 0,

                Maghrib = FormatTime(day.Timings.Maghrib, settings.ClockFormat, culture),
                MaghribBase = FormatTime(day.Timings.Maghrib.AddMinutes(-settings.Offsets.Maghrib), settings.ClockFormat, culture),
                ShowMaghribBase = settings.Offsets.Maghrib != 0,

                Isha = FormatTime(day.Timings.Isha, settings.ClockFormat, culture),
                IshaBase = FormatTime(day.Timings.Isha.AddMinutes(-settings.Offsets.Isha), settings.ClockFormat, culture),
                ShowIshaBase = settings.Offsets.Isha != 0
            })
            .ToList();
    }

    private static string FormatTime(DateTime time, ClockFormat format, CultureInfo culture) {
        return format switch {
            ClockFormat.TwelveHour => time.ToString("h:mm tt", culture),
            ClockFormat.TwentyFourHour => time.ToString("HH:mm", culture),
            _ => time.ToString("t", culture)
        };
    }
}
