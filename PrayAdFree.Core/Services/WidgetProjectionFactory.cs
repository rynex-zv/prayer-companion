using System.Globalization;
using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public sealed class WidgetProjectionFactory {
    private readonly DailyPrayerSnapshotFactory _daily = new();
    private readonly FastingSnapshotFactory _fasting = new();

    public WidgetProjection Build(
        PrayerDay today,
        PrayerDay? tomorrow,
        AppSettings settings,
        DateTime now,
        string? language = null,
        string? locationSource = null,
        string? tasbihPresetName = null,
        string? tasbihText = null,
        int tasbihCount = 0,
        int tasbihTarget = 0) {
        ArgumentNullException.ThrowIfNull(today);
        ArgumentNullException.ThrowIfNull(settings);
        var normalizedLanguage = string.Equals(language ?? settings.Language, "ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "en";
        var daily = _daily.Build(today, tomorrow, settings, now);
        var fasting = _fasting.Build(today, tomorrow, settings, now);
        var culture = normalizedLanguage == "ar" ? CultureInfo.GetCultureInfo("ar") : CultureInfo.GetCultureInfo("en");
        var location = BuildLocation(settings.Location);

        return new WidgetProjection {
            GeneratedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Language = normalizedLanguage,
            IsRtl = normalizedLanguage == "ar",
            LocationTitle = location,
            LocationSource = locationSource ?? settings.Location.Source,
            HijriDate = today.Hijri.Date,
            GregorianDate = now.ToString("dddd, dd MMMM yyyy", culture),
            NextPrayerId = daily.NextPrayerId.ToString(),
            NextPrayerName = PrayerName(daily.NextPrayerId, normalizedLanguage),
            NextPrayerTime = FormatClock(daily.NextPrayerTime, settings.ClockFormat, culture),
            NextPrayerAtUnixMilliseconds = ToUnixMilliseconds(daily.NextPrayerTime, settings.Location.TimeZoneId),
            IsNextPrayerTomorrow = daily.IsNextPrayerTomorrow,
            PrayerRows = daily.Entries.Select(entry => new WidgetPrayerRow {
                Id = entry.Prayer.ToString(),
                Name = PrayerName(entry.Prayer, normalizedLanguage),
                Time = FormatClock(entry.AdjustedTime, settings.ClockFormat, culture),
                TargetUnixMilliseconds = ToUnixMilliseconds(entry.AdjustedTime, settings.Location.TimeZoneId),
                IsNext = entry.IsNext
            }).ToArray(),
            ImsakTime = FormatClock(fasting.ImsakTime, settings.ClockFormat, culture),
            IftarTime = FormatClock(fasting.IftarTime, settings.ClockFormat, culture),
            FastingTargetName = fasting.IsIftarNext ? Text("iftar", normalizedLanguage) : Text("imsak", normalizedLanguage),
            FastingTargetAtUnixMilliseconds = ToUnixMilliseconds(fasting.NextTargetTime, settings.Location.TimeZoneId),
            TasbihPresetName = tasbihPresetName ?? "",
            TasbihText = tasbihText ?? "",
            TasbihCount = Math.Max(0, tasbihCount),
            TasbihTarget = Math.Max(0, tasbihTarget),
            QiblaBearingDegrees = (int)Math.Round(QiblaCalculator.CalculateBearing(settings.Location.Latitude, settings.Location.Longitude), MidpointRounding.AwayFromZero)
        };
    }

    public WidgetProjection Error(string message, string language = "en") => new() {
        GeneratedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        Language = language,
        IsRtl = language == "ar",
        Status = "error",
        Error = string.IsNullOrWhiteSpace(message) ? Text("unavailable", language) : message
    };

    public static string Text(string key, string language) => (language == "ar", key) switch {
        (true, "tomorrow") => "غدًا",
        (true, "imsak") => "الإمساك",
        (true, "iftar") => "الإفطار",
        (true, "countdown") => "الوقت المتبقي",
        (true, "location") => "الموقع",
        (true, "qibla") => "اتجاه القبلة",
        (true, "increment") => "زيادة التسبيح",
        (true, "reset") => "إعادة ضبط التسبيح",
        (true, "unavailable") => "البيانات غير متاحة",
        (true, "dataUnavailable") => "بيانات الأداة غير متاحة",
        (true, "lastUpdate") => "آخر تحديث",
        (false, "tomorrow") => "Tomorrow",
        (false, "imsak") => "Imsak",
        (false, "iftar") => "Iftar",
        (false, "countdown") => "Remaining",
        (false, "location") => "Location",
        (false, "qibla") => "Qibla direction",
        (false, "increment") => "Increment Tasbih",
        (false, "reset") => "Reset Tasbih",
        (false, "dataUnavailable") => "Widget data unavailable",
        (false, "lastUpdate") => "Last update",
        _ => "Unavailable"
    };

    private static string BuildLocation(LocationSettings location) =>
        !string.IsNullOrWhiteSpace(location.City) && !string.IsNullOrWhiteSpace(location.Country)
            ? $"{location.City}, {location.Country}"
            : !string.IsNullOrWhiteSpace(location.City) ? location.City : location.Country;

    private static string PrayerName(PrayerId prayer, string language) => language == "ar" ? prayer switch {
        PrayerId.Fajr => "الفجر",
        PrayerId.Sunrise => "الشروق",
        PrayerId.Dhuhr => "الظهر",
        PrayerId.Asr => "العصر",
        PrayerId.Maghrib => "المغرب",
        PrayerId.Isha => "العشاء",
        PrayerId.Imsak => "الإمساك",
        _ => prayer.ToString()
    } : prayer.ToString();

    private static string FormatClock(DateTime value, ClockFormat format, CultureInfo culture) => format switch {
        ClockFormat.TwelveHour => value.ToString("h:mm tt", culture),
        ClockFormat.TwentyFourHour => value.ToString("HH:mm", culture),
        _ => value.ToString(culture.DateTimeFormat.ShortTimePattern, culture)
    };

    private static long ToUnixMilliseconds(DateTime local, string timeZoneId) {
        try {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            return new DateTimeOffset(unspecified, zone.GetUtcOffset(unspecified)).ToUnixTimeMilliseconds();
        } catch (TimeZoneNotFoundException) {
            throw new InvalidOperationException($"Unknown widget time zone: {timeZoneId}");
        } catch (InvalidTimeZoneException exception) {
            throw new InvalidOperationException($"Invalid widget time zone: {timeZoneId}", exception);
        }
    }
}
