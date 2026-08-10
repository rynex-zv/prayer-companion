using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public sealed class SharedCorePrayerTimesClient : IPrayerTimesClient {
    private readonly WebPrayerMonthFactory _factory;

    public SharedCorePrayerTimesClient(WebPrayerMonthFactory factory) {
        _factory = factory;
    }

    public Task<PrayerMonth> GetMonthAsync(AppSettings settings, int year, int month, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_factory.BuildMonth(settings, year, month));
    }
}
