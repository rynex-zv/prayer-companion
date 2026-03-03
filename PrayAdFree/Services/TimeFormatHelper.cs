using System.Globalization;
using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Services;

public static class TimeFormatHelper {
    public static string FormatTime(DateTime time, ClockFormat format) {
        var culture = CultureInfo.CurrentCulture;
        return format switch {
            ClockFormat.TwelveHour => time.ToString("h:mm tt", culture),
            ClockFormat.TwentyFourHour => time.ToString("HH:mm", culture),
            _ => time.ToString("t", culture)
        };
    }
}
