using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public class PrayerTimesCacheTests {
    [Fact]
    public async Task WriteAndRead_RoundTrips() {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
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
    }
}
