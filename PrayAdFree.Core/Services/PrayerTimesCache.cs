using System.Text.Json;
using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public sealed class PrayerTimesCache {
    private readonly string _cacheDirectory;
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions {
        WriteIndented = true
    };

    public PrayerTimesCache(string cacheDirectory) {
        _cacheDirectory = cacheDirectory;
        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<PrayerMonth?> TryReadAsync(string cacheKey, CancellationToken cancellationToken) {
        var path = GetPath(cacheKey);
        if (!File.Exists(path)) {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<PrayerMonth>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteAsync(string cacheKey, PrayerMonth month, CancellationToken cancellationToken) {
        var path = GetPath(cacheKey);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, month, _jsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private string GetPath(string cacheKey) {
        return Path.Combine(_cacheDirectory, $"{cacheKey}.json");
    }
}
