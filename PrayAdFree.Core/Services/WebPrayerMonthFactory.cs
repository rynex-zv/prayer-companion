using AdhanCalculationMethod = Batoulapps.Adhan.CalculationMethod;
using AdhanHighLatitudeRule = Batoulapps.Adhan.HighLatitudeRule;
using AdhanMadhab = Batoulapps.Adhan.Madhab;
using Batoulapps.Adhan;
using Batoulapps.Adhan.Internal;
using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

/// <summary>
/// The single deterministic prayer-time engine used by WebAssembly, Windows,
/// Android, and every other host. Keep platform transport out of this class.
/// </summary>
public sealed class WebPrayerMonthFactory {
    public const string EngineId = "SharedCoreAdhan";

    public PrayerMonth BuildMonth(AppSettings settings, int year, int month) {
        return new PrayerMonth {
            Year = year,
            Month = month,
            LocationKey = $"{settings.Location.Latitude:F4},{settings.Location.Longitude:F4}",
            MethodKey = $"{EngineId}-{ResolveMethod(settings)}-{settings.Madhhab}-{settings.HighLatitudeRule}",
            FetchedOnUtc = DateTime.UtcNow,
            Days = Enumerable.Range(1, DateTime.DaysInMonth(year, month))
                .Select(day => BuildDay(settings, new DateOnly(year, month, day)))
                .ToArray()
        };
    }

    public PrayerDay BuildDay(AppSettings settings, DateOnly date) {
        if (!IsValidCoordinate(settings.Location.Latitude, settings.Location.Longitude)) {
            throw new InvalidOperationException("Location is missing or invalid. Enable GPS or set a manual city.");
        }

        var coordinates = new Coordinates(settings.Location.Latitude, settings.Location.Longitude);
        var parameters = BuildParameters(settings);
        var calculated = new Batoulapps.Adhan.PrayerTimes(
            coordinates,
            new DateComponents(date.Year, date.Month, date.Day),
            parameters);
        var timeZone = ResolveTimeZone(settings.Location.TimeZoneId);
        // Convert every astronomical instant independently. A single noon
        // offset is wrong on daylight-saving transition days: Fajr can occur
        // before the clock change while Dhuhr occurs after it.
        DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            timeZone);
        var baseFajr = Local(calculated.Fajr);
        var resolvedMethod = ResolveMethod(settings);
        var baseMaghribUtc = resolvedMethod switch {
            Models.CalculationMethod.Jafari => CalculateAngleTimeUtc(coordinates, date, 4.0),
            Models.CalculationMethod.Tehran => CalculateAngleTimeUtc(coordinates, date, 4.5),
            _ => calculated.Maghrib
        };
        var hijri = BuildHijri(date);
        var ramadanIshaAdjustment = resolvedMethod == Models.CalculationMethod.UmmAlQura && hijri.MonthNumber == 9
            ? 30
            : 0;
        return new PrayerDay {
            Date = date,
            TimeZoneId = timeZone.Id,
            Hijri = hijri.Value,
            Timings = new PrayAdFree.Core.Models.PrayerTimings {
                // PrayerDay stores the base Imsak clock. The user-configured
                // fasting advance is applied by FastingSnapshotFactory exactly once.
                Imsak = baseFajr.AddMinutes(settings.Offsets.Imsak),
                Fajr = baseFajr.AddMinutes(settings.Offsets.Fajr),
                Sunrise = Local(calculated.Sunrise).AddMinutes(settings.Offsets.Sunrise),
                Dhuhr = Local(calculated.Dhuhr).AddMinutes(settings.Offsets.Dhuhr),
                Asr = Local(calculated.Asr).AddMinutes(settings.Offsets.Asr),
                Maghrib = Local(baseMaghribUtc).AddMinutes(settings.Offsets.Maghrib),
                Isha = Local(calculated.Isha).AddMinutes(settings.Offsets.Isha + ramadanIshaAdjustment)
            }
        };
    }

    private static CalculationParameters BuildParameters(AppSettings settings) {
        var method = AppInputContract.RequiredEnum(ResolveMethod(settings), "calculationMethod");
        var parameters = method switch {
            Models.CalculationMethod.Karachi => AdhanCalculationMethod.KARACHI.GetParameters(),
            Models.CalculationMethod.Isna => AdhanCalculationMethod.NORTH_AMERICA.GetParameters(),
            Models.CalculationMethod.MuslimWorldLeague => AdhanCalculationMethod.MUSLIM_WORLD_LEAGUE.GetParameters(),
            Models.CalculationMethod.UmmAlQura => AdhanCalculationMethod.UMM_AL_QURA.GetParameters(),
            Models.CalculationMethod.Egypt => AdhanCalculationMethod.EGYPTIAN.GetParameters(),
            Models.CalculationMethod.Kuwait => AdhanCalculationMethod.KUWAIT.GetParameters(),
            Models.CalculationMethod.Qatar => AdhanCalculationMethod.QATAR.GetParameters(),
            Models.CalculationMethod.Singapore => AdhanCalculationMethod.SINGAPORE.GetParameters(),
            Models.CalculationMethod.Moonsighting => AdhanCalculationMethod.MOON_SIGHTING_COMMITTEE.GetParameters(),
            Models.CalculationMethod.Dubai => AdhanCalculationMethod.DUBAI.GetParameters(),
            Models.CalculationMethod.Jafari => Angles(16, 14),
            Models.CalculationMethod.Tehran => Angles(17.7, 14),
            Models.CalculationMethod.Custom => new CalculationParameters(settings.SunAngles.Fajr, settings.SunAngles.Isha, AdhanCalculationMethod.OTHER),
            _ => CustomParameters(method)
        };
        parameters.Madhab = settings.Madhhab == Madhhab.Hanafi ? AdhanMadhab.HANAFI : AdhanMadhab.SHAFI;
        parameters.HighLatitudeRule = AppInputContract.RequiredEnum(settings.HighLatitudeRule, "highLatitudeRule") switch {
            Models.HighLatitudeRule.SeventhOfTheNight => AdhanHighLatitudeRule.SEVENTH_OF_THE_NIGHT,
            Models.HighLatitudeRule.TwilightAngle => AdhanHighLatitudeRule.TWILIGHT_ANGLE,
            Models.HighLatitudeRule.MiddleOfTheNight => AdhanHighLatitudeRule.MIDDLE_OF_THE_NIGHT,
            _ => throw new InvalidOperationException("Unreachable high-latitude rule.")
        };
        return parameters;
    }

    private static CalculationParameters CustomParameters(Models.CalculationMethod method) {
        return method switch {
            Models.CalculationMethod.Gulf => Interval(19.5, 90),
            Models.CalculationMethod.France => Angles(12, 12),
            Models.CalculationMethod.Turkey => Angles(18, 17),
            Models.CalculationMethod.Russia => Angles(16, 15),
            Models.CalculationMethod.Jakim => Angles(20, 18),
            Models.CalculationMethod.Tunisia => Angles(18, 18),
            Models.CalculationMethod.Algeria => Angles(18, 17),
            Models.CalculationMethod.Kemenag => Angles(20, 18),
            Models.CalculationMethod.Morocco => Angles(19, 17),
            Models.CalculationMethod.Portugal => WithMaghribAdjustment(Interval(18, 77), 3),
            Models.CalculationMethod.Jordan => WithMaghribAdjustment(Angles(18, 18), 5),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method,
                "The selected calculation method is not supported by the shared calculation engine.")
        };
    }

    private static CalculationParameters Angles(double fajr, double isha) =>
        new(fajr, isha, AdhanCalculationMethod.OTHER);

    private static CalculationParameters Interval(double fajr, int ishaMinutes) =>
        new(fajr, ishaMinutes, AdhanCalculationMethod.OTHER);

    private static CalculationParameters WithMaghribAdjustment(CalculationParameters parameters, int minutes) {
        parameters.Adjustments.Maghrib = minutes;
        return parameters;
    }

    private static DateTime CalculateAngleTimeUtc(Coordinates coordinates, DateOnly date, double angleBelowHorizon) {
        // Adhan .NET 0.9 predates the Maghrib-angle parameter supported by the
        // other Batoulapps Adhan implementations. SolarTime is the same
        // astronomical engine used by PrayerTimes, so calculate only the
        // missing evening hour angle here rather than substituting sunset.
        var utcDate = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var decimalHours = new SolarTime(utcDate, coordinates).HourAngle(-angleBelowHorizon, afterTransit: true);
        if (!double.IsFinite(decimalHours)) {
            throw new InvalidOperationException($"The {angleBelowHorizon:0.##} degree Maghrib angle is not computable for this date and location.");
        }

        return utcDate.AddHours(decimalHours).Round(TimeSpan.FromMinutes(1));
    }

    private static Models.CalculationMethod ResolveMethod(AppSettings settings) =>
        settings.Method == Models.CalculationMethod.Auto
            ? MethodResolver.ResolveRequired(settings.Location.CountryCode)
            : settings.Method;

    private static TimeZoneInfo ResolveTimeZone(string id) {
        if (string.IsNullOrWhiteSpace(id)) {
            throw new ArgumentException("Location time-zone ID is required; the device time zone will not be substituted.", nameof(id));
        }

        try {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        } catch (TimeZoneNotFoundException exception) {
            throw new ArgumentException($"Unknown location time-zone ID '{id}'.", nameof(id), exception);
        } catch (InvalidTimeZoneException exception) {
            throw new ArgumentException($"Invalid location time-zone data for '{id}'.", nameof(id), exception);
        }
    }

    private static (HijriDate Value, int MonthNumber) BuildHijri(DateOnly date) {
        var calendar = new System.Globalization.HijriCalendar();
        var value = date.ToDateTime(TimeOnly.MinValue);
        var month = calendar.GetMonth(value);
        string[] names = ["Muharram", "Safar", "Rabi al-awwal", "Rabi al-thani", "Jumada al-awwal", "Jumada al-thani", "Rajab", "Shaban", "Ramadan", "Shawwal", "Dhu al-Qadah", "Dhu al-Hijjah"];
        return (new HijriDate {
            Day = calendar.GetDayOfMonth(value).ToString("00"),
            Month = names[Math.Clamp(month - 1, 0, names.Length - 1)],
            Year = calendar.GetYear(value).ToString()
        }, month);
    }

    private static bool IsValidCoordinate(double latitude, double longitude) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180 &&
        !(Math.Abs(latitude) < 0.000001 && Math.Abs(longitude) < 0.000001);
}
