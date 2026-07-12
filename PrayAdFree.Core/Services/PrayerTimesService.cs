using System.Security.Cryptography;
using System.Text;
using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public sealed class PrayerTimesService {
    public const int CacheSchemaVersion = 2;
    private readonly IPrayerTimesClient _client;
    private readonly PrayerTimesCache _cache;

    public PrayerTimesService(IPrayerTimesClient client, PrayerTimesCache cache) {
        _client = client;
        _cache = cache;
    }

    public async Task<PrayerMonth> GetMonthAsync(AppSettings settings, int year, int month, CancellationToken cancellationToken) {
        var cacheKey = BuildCacheKey(settings, year, month);
        PrayerMonth? cached = null;
        try {
            cached = await _cache.TryReadAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        } catch {
        }

        if (cached != null && cached.FetchedOnUtc.Date == DateTime.UtcNow.Date) {
            return cached;
        }

        var fresh = await _client.GetMonthAsync(settings, year, month, cancellationToken).ConfigureAwait(false);
        try {
            await _cache.WriteAsync(cacheKey, fresh, cancellationToken).ConfigureAwait(false);
        } catch {
        }
        return fresh;
    }

    public static string BuildCacheKey(AppSettings settings, int year, int month) {
        var method = settings.Method == CalculationMethod.Auto
            ? MethodResolver.Resolve(settings.Location.CountryCode, CalculationMethod.MuslimWorldLeague)
            : settings.Method;
        var raw = $"v{CacheSchemaVersion}-{year}-{month}-{settings.Location.Latitude:F4}-{settings.Location.Longitude:F4}-{method}-{settings.Madhhab}-{settings.HighLatitudeRule}-{OffsetsKey(settings.Offsets)}-{SunAnglesKey(settings.SunAngles)}";
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    private static string OffsetsKey(PrayerOffsets offsets) {
        return $"{offsets.Imsak},{offsets.Fajr},{offsets.Sunrise},{offsets.Dhuhr},{offsets.Asr},{offsets.Maghrib},{offsets.Isha}";
    }

    private static string SunAnglesKey(SunAngleSettings sunAngles) {
        return $"{sunAngles.Fajr:0.##},{sunAngles.Isha:0.##}";
    }
}
