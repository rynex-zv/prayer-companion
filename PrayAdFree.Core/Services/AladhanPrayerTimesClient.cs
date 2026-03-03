using System.Text.Json;
using System.Text.Json.Serialization;
using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public sealed class AladhanPrayerTimesClient : IPrayerTimesClient {
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions {
        PropertyNameCaseInsensitive = true
    };

    public AladhanPrayerTimesClient(HttpClient httpClient) {
        _httpClient = httpClient;
        if (_httpClient.BaseAddress == null) {
            _httpClient.BaseAddress = new Uri("https://api.aladhan.com/v1/");
        }
    }

    public async Task<PrayerMonth> GetMonthAsync(AppSettings settings, int year, int month, CancellationToken cancellationToken) {
        var location = settings.Location;
        if (!IsValidCoordinate(location.Latitude, location.Longitude)) {
            throw new InvalidOperationException("Location is missing or invalid. Enable GPS or set a manual city.");
        }

        var method = settings.Method == CalculationMethod.Auto
            ? MethodResolver.Resolve(location.CountryCode, CalculationMethod.MuslimWorldLeague)
            : settings.Method;

        var school = settings.Madhhab == Madhhab.Hanafi ? 1 : 0;
        var url = $"calendar?latitude={location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                  $"&longitude={location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                  $"&method={(int)method}" +
                  $"&school={school}" +
                  $"&latitudeAdjustmentMethod={(int)settings.HighLatitudeRule}" +
                  $"&month={month}&year={year}" +
                  $"&tune={BuildTune(settings.Offsets)}";

        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<AladhanCalendarResponse>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);
        if (data?.Data == null || data.Data.Count == 0) {
            throw new InvalidOperationException("Aladhan response did not contain data.");
        }

        var timeZoneInfo = TimeZoneInfo.Local;
        var days = data.Data.Select(item => MapDay(item, timeZoneInfo)).ToList();
        return new PrayerMonth {
            Year = year,
            Month = month,
            LocationKey = $"{location.Latitude:F4},{location.Longitude:F4}",
            MethodKey = $"{method}-{settings.Madhhab}-{settings.HighLatitudeRule}",
            FetchedOnUtc = DateTime.UtcNow,
            Days = days
        };
    }

    private static PrayerDay MapDay(AladhanCalendarDay item, TimeZoneInfo timeZone) {
        var date = ParseGregorianDate(item.Date.Gregorian.Date);
        var timings = item.Timings;
        return new PrayerDay {
            Date = date,
            TimeZoneId = timeZone.Id,
            Hijri = new HijriDate {
                Day = item.Date.Hijri.Day,
                Month = item.Date.Hijri.Month.En,
                Year = item.Date.Hijri.Year
            },
            Timings = new PrayerTimings {
                Fajr = PrayerTimings.ParseLocalDateTime(date, TrimTime(timings.Fajr), timeZone),
                Sunrise = PrayerTimings.ParseLocalDateTime(date, TrimTime(timings.Sunrise), timeZone),
                Dhuhr = PrayerTimings.ParseLocalDateTime(date, TrimTime(timings.Dhuhr), timeZone),
                Asr = PrayerTimings.ParseLocalDateTime(date, TrimTime(timings.Asr), timeZone),
                Maghrib = PrayerTimings.ParseLocalDateTime(date, TrimTime(timings.Maghrib), timeZone),
                Isha = PrayerTimings.ParseLocalDateTime(date, TrimTime(timings.Isha), timeZone),
                Imsak = PrayerTimings.ParseLocalDateTime(date, TrimTime(timings.Imsak), timeZone)
            }
        };
    }

    private static string BuildTune(PrayerOffsets offsets) {
        var values = new[] {
            offsets.Imsak,
            offsets.Fajr,
            offsets.Sunrise,
            offsets.Dhuhr,
            offsets.Asr,
            offsets.Maghrib,
            offsets.Isha,
            0,
            0
        };
        return string.Join(",", values);
    }

    private static string TrimTime(string raw) {
        var spaceIndex = raw.IndexOf(' ');
        return spaceIndex > 0 ? raw[..spaceIndex] : raw;
    }

    private static DateOnly ParseGregorianDate(string raw) {
        if (DateTime.TryParseExact(
                raw,
                "dd-MM-yyyy",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var parsed)) {
            return DateOnly.FromDateTime(parsed);
        }

        return DateOnly.FromDateTime(DateTime.Parse(raw, System.Globalization.CultureInfo.CurrentCulture));
    }

    private static bool IsValidCoordinate(double latitude, double longitude) {
        return latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
    }

    private sealed class AladhanCalendarResponse {
        [JsonPropertyName("data")]
        public List<AladhanCalendarDay> Data { get; set; } = new();
    }

    private sealed class AladhanCalendarDay {
        [JsonPropertyName("timings")]
        public AladhanTimings Timings { get; set; } = new();

        [JsonPropertyName("date")]
        public AladhanDate Date { get; set; } = new();
    }

    private sealed class AladhanTimings {
        public string Fajr { get; set; } = "";
        public string Sunrise { get; set; } = "";
        public string Dhuhr { get; set; } = "";
        public string Asr { get; set; } = "";
        public string Maghrib { get; set; } = "";
        public string Isha { get; set; } = "";
        public string Imsak { get; set; } = "";
    }

    private sealed class AladhanDate {
        [JsonPropertyName("gregorian")]
        public AladhanGregorian Gregorian { get; set; } = new();

        [JsonPropertyName("hijri")]
        public AladhanHijri Hijri { get; set; } = new();
    }

    private sealed class AladhanGregorian {
        public string Date { get; set; } = "";
    }

    private sealed class AladhanHijri {
        public string Day { get; set; } = "";
        public string Year { get; set; } = "";
        public AladhanHijriMonth Month { get; set; } = new();
    }

    private sealed class AladhanHijriMonth {
        [JsonPropertyName("en")]
        public string En { get; set; } = "";
    }
}
