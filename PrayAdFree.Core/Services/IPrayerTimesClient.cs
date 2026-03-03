using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public interface IPrayerTimesClient {
    Task<PrayerMonth> GetMonthAsync(AppSettings settings, int year, int month, CancellationToken cancellationToken);
}
