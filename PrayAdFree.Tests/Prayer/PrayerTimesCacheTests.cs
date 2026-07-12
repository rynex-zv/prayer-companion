using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public class PrayerTimesCacheTests {
    [Fact]
    public async Task WriteAndRead_RoundTrips() {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            var cache = new PrayerTimesCache(tempDir);
            var month = new PrayerMonth {
                Year = 2025,
                Month = 1,
                LocationKey = "0,0",
                MethodKey = "test",
                FetchedOnUtc = DateTime.UtcNow,
                Days = new List<PrayerDay>()
            };

            await cache.WriteAsync("key", month, CancellationToken.None);
            var loaded = await cache.TryReadAsync("key", CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(month.Year, loaded?.Year);
        } finally {
            if (Directory.Exists(tempDir)) {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task TryReadAsync_InvalidJson_ReturnsNull() {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try {
            var cache = new PrayerTimesCache(tempDir);
            var path = CachePath(tempDir, "broken");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, "{ not-json");

            var loaded = await cache.TryReadAsync("broken", CancellationToken.None);

            Assert.Null(loaded);
        } finally {
            if (Directory.Exists(tempDir)) {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WriteAsync_TargetFileLocked_DoesNotCorruptExistingCache() {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try {
            var cache = new PrayerTimesCache(tempDir);
            var original = new PrayerMonth {
                Year = 2025,
                Month = 1,
                LocationKey = "0,0",
                MethodKey = "test",
                FetchedOnUtc = DateTime.UtcNow,
                Days = new List<PrayerDay>()
            };
            await cache.WriteAsync("locked", original, CancellationToken.None);

            var path = CachePath(tempDir, "locked");
            await using var lockStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

            var updated = new PrayerMonth {
                Year = 2026,
                Month = 2,
                LocationKey = "1,1",
                MethodKey = "new",
                FetchedOnUtc = DateTime.UtcNow,
                Days = new List<PrayerDay>()
            };

            await Assert.ThrowsAnyAsync<Exception>(() => cache.WriteAsync("locked", updated, CancellationToken.None));

            lockStream.Dispose();
            var loaded = await cache.TryReadAsync("locked", CancellationToken.None);
            Assert.NotNull(loaded);
            Assert.Equal(original.Year, loaded!.Year);
        } finally {
            if (Directory.Exists(tempDir)) {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ClearAsync_RemovesOnlyReconstructablePrayerCache() {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            var authoritative = Path.Combine(tempDir, "app_settings.json");
            Directory.CreateDirectory(tempDir);
            await File.WriteAllTextAsync(authoritative, "user-data");
            var cache = new PrayerTimesCache(tempDir);
            await cache.WriteAsync("key", new PrayerMonth { Year = 2025, Month = 1, FetchedOnUtc = DateTime.UtcNow }, default);

            await cache.ClearAsync();

            Assert.Null(await cache.TryReadAsync("key", default));
            Assert.Equal("user-data", await File.ReadAllTextAsync(authoritative));
        } finally {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string CachePath(string root, string key) =>
        Path.Combine(root, "PrayerTimesCache", $"v{PrayerTimesService.CacheSchemaVersion}", $"{key}.json");
}
