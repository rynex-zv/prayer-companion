using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public sealed class WebPrayerMonthFactory {
    public PrayerMonth BuildMonth(AppSettings settings, int year, int month) {
        return new PrayerMonth {
            Year = year,
            Month = month,
            LocationKey = $"{settings.Location.Latitude:F4},{settings.Location.Longitude:F4}",
            MethodKey = "core-local-solar",
            FetchedOnUtc = DateTime.UtcNow,
            Days = Enumerable.Range(1, DateTime.DaysInMonth(year, month))
                .Select(day => BuildDay(settings, new DateOnly(year, month, day)))
                .ToArray()
        };
    }

    public PrayerDay BuildDay(AppSettings settings, DateOnly date) {
        var baseDate = date.ToDateTime(TimeOnly.MinValue);
        var latitude = settings.Location.Latitude;
        var longitude = settings.Location.Longitude;
        var solar = CalculateSolarTimes(date, latitude, longitude);
        var sunrise = baseDate.Add(solar.Sunrise);
        var sunset = baseDate.Add(solar.Sunset);
        var noon = baseDate.Add(solar.Noon);
        var dayLength = sunset - sunrise;
        var fajrAngle = settings.SunAngles.Fajr > 0 ? settings.SunAngles.Fajr : 18d;
        var ishaAngle = settings.SunAngles.Isha > 0 ? settings.SunAngles.Isha : 17d;
        var fajr = sunrise - TimeSpan.FromMinutes(AngleMinutes(fajrAngle, latitude, date));
        var isha = sunset + TimeSpan.FromMinutes(AngleMinutes(ishaAngle, latitude, date));
        var asr = noon + TimeSpan.FromTicks((sunset - noon).Ticks * (settings.Madhhab == Madhhab.Hanafi ? 2 : 1) / 3);

        return new PrayerDay {
            Date = date,
            TimeZoneId = TimeZoneInfo.Local.Id,
            Hijri = BuildApproximateHijri(date),
            Timings = new PrayerTimings {
                Imsak = fajr.AddMinutes(-10 + settings.Offsets.Imsak),
                Fajr = fajr.AddMinutes(settings.Offsets.Fajr),
                Sunrise = sunrise.AddMinutes(settings.Offsets.Sunrise),
                Dhuhr = noon.AddMinutes(settings.Offsets.Dhuhr),
                Asr = asr.AddMinutes(settings.Offsets.Asr),
                Maghrib = sunset.AddMinutes(settings.Offsets.Maghrib),
                Isha = isha.AddMinutes(settings.Offsets.Isha)
            }
        };
    }

    private static (TimeSpan Sunrise, TimeSpan Noon, TimeSpan Sunset) CalculateSolarTimes(DateOnly date, double latitude, double longitude) {
        var dayOfYear = date.DayOfYear;
        var gamma = 2d * Math.PI / 365d * (dayOfYear - 1);
        var equationOfTime = 229.18d * (0.000075d + 0.001868d * Math.Cos(gamma) - 0.032077d * Math.Sin(gamma) - 0.014615d * Math.Cos(2d * gamma) - 0.040849d * Math.Sin(2d * gamma));
        var declination = 0.006918d - 0.399912d * Math.Cos(gamma) + 0.070257d * Math.Sin(gamma) - 0.006758d * Math.Cos(2d * gamma) + 0.000907d * Math.Sin(2d * gamma) - 0.002697d * Math.Cos(3d * gamma) + 0.00148d * Math.Sin(3d * gamma);
        var latRad = DegreesToRadians(latitude);
        var zenith = DegreesToRadians(90.833d);
        var hourAngle = Math.Acos(Clamp((Math.Cos(zenith) / (Math.Cos(latRad) * Math.Cos(declination))) - Math.Tan(latRad) * Math.Tan(declination), -1d, 1d));
        var offsetMinutes = TimeZoneInfo.Local.GetUtcOffset(date.ToDateTime(new TimeOnly(12, 0))).TotalMinutes;
        var solarNoon = 720d - 4d * longitude - equationOfTime + offsetMinutes;
        var daylightMinutes = RadiansToDegrees(hourAngle) * 4d;
        return (
            TimeSpan.FromMinutes(ClampMinutes(solarNoon - daylightMinutes)),
            TimeSpan.FromMinutes(ClampMinutes(solarNoon)),
            TimeSpan.FromMinutes(ClampMinutes(solarNoon + daylightMinutes)));
    }

    private static double AngleMinutes(double angle, double latitude, DateOnly date) {
        var seasonal = 1d + Math.Abs(latitude) / 180d + Math.Abs(Math.Sin(2d * Math.PI * date.DayOfYear / 365d)) * 0.25d;
        return Math.Clamp(angle * 4d * seasonal, 45d, 140d);
    }

    private static HijriDate BuildApproximateHijri(DateOnly date) {
        var jd = (int)Math.Floor(date.ToDateTime(TimeOnly.MinValue).ToOADate() + 2415018.5);
        var islamic = jd - 1948440 + 10632;
        var n = (int)Math.Floor((islamic - 1) / 10631d);
        islamic -= 10631 * n;
        var j = (int)Math.Floor((10985 - islamic) / 5316d) * (int)Math.Floor(50 * islamic / 17719d) + (int)Math.Floor(islamic / 5670d) * (int)Math.Floor(43 * islamic / 15238d);
        islamic = islamic - (int)Math.Floor((30 - j) / 15d) * (int)Math.Floor(17719 * j / 50d) - (int)Math.Floor(j / 16d) * (int)Math.Floor(15238 * j / 43d) + 29;
        var month = (int)Math.Floor(24 * islamic / 709d);
        var day = islamic - (int)Math.Floor(709 * month / 24d);
        var year = 30 * n + j - 30;
        string[] monthNames = ["Muharram", "Safar", "Rabi al-awwal", "Rabi al-thani", "Jumada al-awwal", "Jumada al-thani", "Rajab", "Shaban", "Ramadan", "Shawwal", "Dhu al-Qadah", "Dhu al-Hijjah"];
        return new HijriDate {
            Day = Math.Clamp(day, 1, 30).ToString("00"),
            Month = monthNames[Math.Clamp(month - 1, 0, monthNames.Length - 1)],
            Year = year.ToString()
        };
    }

    private static double DegreesToRadians(double value) => value * Math.PI / 180d;
    private static double RadiansToDegrees(double value) => value * 180d / Math.PI;
    private static double Clamp(double value, double min, double max) => Math.Min(Math.Max(value, min), max);
    private static double ClampMinutes(double value) => Clamp(value, 0d, 1439d);
}
