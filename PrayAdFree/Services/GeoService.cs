using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace Pray_Ad_Free.Services;

public sealed class GeoService : IGeoLookupService {
    private const int CacheSchemaVersion = 1;
    private const double DefaultRadiusKm = 20;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(30);
    private static readonly TimeSpan MinRequestInterval = TimeSpan.FromSeconds(1);

    private readonly IReadOnlyList<IGeoProvider> _providers;
    private readonly string _cachePath;
    private readonly List<GeoCacheEntry> _cacheEntries = new();
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private int _index;
    private DateTime _lastRequestUtc = DateTime.MinValue;

    public GeoService(IEnumerable<IGeoProvider> providers, string cachePath) {
        _providers = providers.ToList();
        _cachePath = cachePath;
        LoadCache();
    }

    public async Task<GeoLocationResult?> ReverseAsync(double latitude, double longitude, CancellationToken cancellationToken) {
        var cached = TryGetCached(latitude, longitude);
        if (cached != null) {
            return cached;
        }

        var result = await TryProvidersAsync(p => p.ReverseAsync(latitude, longitude, cancellationToken)).ConfigureAwait(false);
        if (result != null) {
            await AddCacheAsync(result, DefaultRadiusKm).ConfigureAwait(false);
        }

        return result;
    }

    public IReadOnlyList<GeoLocationResult> GetKnownPlaces() {
        var cached = _cacheEntries
            .Where(entry => (DateTime.UtcNow - entry.TimestampUtc) <= CacheTtl)
            .Select(entry => new GeoLocationResult {
                City = entry.City,
                Country = entry.Country,
                CountryCode = entry.CountryCode,
                Latitude = entry.Latitude,
                Longitude = entry.Longitude
            })
            .ToList();

        var defaults = DefaultPlaces
            .Select(place => new GeoLocationResult {
                City = place.city,
                Country = place.country,
                CountryCode = place.countryCode,
                Latitude = place.latitude,
                Longitude = place.longitude
            });

        return cached
            .Concat(defaults)
            .GroupBy(place => $"{place.CountryCode}|{place.City}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    public async Task<GeoLocationResult?> ForwardAsync(string query, CancellationToken cancellationToken) {
        var result = await TryProvidersAsync(p => p.ForwardAsync(query, cancellationToken)).ConfigureAwait(false);
        if (result != null) {
            await AddCacheAsync(result, DefaultRadiusKm).ConfigureAwait(false);
        }

        return result;
    }

    private async Task<GeoLocationResult?> TryProvidersAsync(Func<IGeoProvider, Task<GeoLocationResult?>> action) {
        if (_providers.Count == 0) {
            return null;
        }

        await _requestLock.WaitAsync().ConfigureAwait(false);
        try {
            var wait = _lastRequestUtc + MinRequestInterval - DateTime.UtcNow;
            if (wait > TimeSpan.Zero) {
                await Task.Delay(wait).ConfigureAwait(false);
            }

            var start = Interlocked.Increment(ref _index);
            for (var i = 0; i < _providers.Count; i++) {
                var provider = _providers[(start + i) % _providers.Count];
                try {
                    var result = await action(provider).ConfigureAwait(false);
                    _lastRequestUtc = DateTime.UtcNow;
                    if (result != null) {
                        return result;
                    }
                } catch {
                    _lastRequestUtc = DateTime.UtcNow;
                }
            }
        } finally {
            _requestLock.Release();
        }

        return null;
    }

    private GeoLocationResult? TryGetCached(double latitude, double longitude) {
        var now = DateTime.UtcNow;
        foreach (var entry in _cacheEntries) {
            if ((now - entry.TimestampUtc) > CacheTtl) {
                continue;
            }

            var distance = HaversineKm(latitude, longitude, entry.Latitude, entry.Longitude);
            if (distance <= entry.RadiusKm) {
                return new GeoLocationResult {
                    City = entry.City,
                    Country = entry.Country,
                    CountryCode = entry.CountryCode,
                    Latitude = entry.Latitude,
                    Longitude = entry.Longitude
                };
            }
        }

        return null;
    }

    private async Task AddCacheAsync(GeoLocationResult result, double radiusKm) {
        await _cacheLock.WaitAsync().ConfigureAwait(false);
        try {
            _cacheEntries.RemoveAll(entry => (DateTime.UtcNow - entry.TimestampUtc) > CacheTtl);
            _cacheEntries.Insert(0, new GeoCacheEntry {
                City = result.City,
                Country = result.Country,
                CountryCode = result.CountryCode,
                Latitude = result.Latitude,
                Longitude = result.Longitude,
                RadiusKm = radiusKm,
                TimestampUtc = DateTime.UtcNow
            });
            if (_cacheEntries.Count > 50) {
                _cacheEntries.RemoveRange(50, _cacheEntries.Count - 50);
            }

            await SaveCacheAsync().ConfigureAwait(false);
        } finally {
            _cacheLock.Release();
        }
    }

    private void LoadCache() {
        try {
            if (!File.Exists(_cachePath)) {
                return;
            }

            var json = File.ReadAllText(_cachePath);
            var document = JsonSerializer.Deserialize<GeoCacheDocument>(json);
            if (document?.SchemaVersion == CacheSchemaVersion) {
                _cacheEntries.Clear();
                _cacheEntries.AddRange(document.Entries.Where(entry =>
                    (DateTime.UtcNow - entry.TimestampUtc) <= CacheTtl));
            } else {
                TryDeleteCache();
            }
        } catch {
            TryDeleteCache();
        }
    }

    private Task SaveCacheAsync() {
        try {
            var directory = Path.GetDirectoryName(_cachePath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            var document = new GeoCacheDocument(CacheSchemaVersion, _cacheEntries);
            var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
            var tempPath = _cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _cachePath, overwrite: true);
        } catch {
            // ignore write failures
        }

        return Task.CompletedTask;
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2) {
        const double radius = 6371;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2))
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return radius * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    private void TryDeleteCache() {
        try { if (File.Exists(_cachePath)) File.Delete(_cachePath); } catch { }
    }

    private static readonly (string country, string countryCode, string city, double latitude, double longitude)[] DefaultPlaces = [
        ("Netherlands", "NL", "Amsterdam", 52.3676, 4.9041),
        ("Netherlands", "NL", "Rotterdam", 51.9244, 4.4777),
        ("Netherlands", "NL", "Utrecht", 52.0907, 5.1214),
        ("Saudi Arabia", "SA", "Makkah", 21.3891, 39.8579),
        ("Saudi Arabia", "SA", "Madinah", 24.5247, 39.5692),
        ("Saudi Arabia", "SA", "Riyadh", 24.7136, 46.6753)
    ];

    private sealed class GeoCacheEntry {
        public string City { get; set; } = "";
        public string Country { get; set; } = "";
        public string CountryCode { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double RadiusKm { get; set; }
        public DateTime TimestampUtc { get; set; }
    }

    private sealed record GeoCacheDocument(int SchemaVersion, List<GeoCacheEntry> Entries);
}
