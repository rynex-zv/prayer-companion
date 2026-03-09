using System;
using System.Threading;
using System.Threading.Tasks;
using PrayAdFree.Core.Services;

namespace Pray_Ad_Free.Services;

public sealed class NotificationBootstrapper {
    private readonly SettingsService _settingsService;
    private readonly PrayerDataService _dataService;
    private readonly IAppLogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _lastRunUtc = DateTime.MinValue;

    public NotificationBootstrapper(SettingsService settingsService, PrayerDataService dataService, IAppLogger logger) {
        _settingsService = settingsService;
        _dataService = dataService;
        _logger = logger;
    }

    public async Task EnsureScheduledAsync(string reason, bool requestPermissions) {
        if (DateTime.UtcNow - _lastRunUtc < TimeSpan.FromMinutes(5)) {
            return;
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try {
            if (DateTime.UtcNow - _lastRunUtc < TimeSpan.FromMinutes(5)) {
                return;
            }

            var settings = _settingsService.Load();
            var month = await _dataService.GetMonthAsync(settings, DateTime.Today, CancellationToken.None).ConfigureAwait(false);
            await _dataService.ScheduleNotificationsAsync(settings, month, CancellationToken.None).ConfigureAwait(false);
            _lastRunUtc = DateTime.UtcNow;
            _logger.LogEvent("NotificationSchedule", $"{reason}|{_lastRunUtc:O}");
        } catch (Exception ex) {
            _logger.LogException(ex, "NotificationBootstrapper.Schedule");
        } finally {
            _gate.Release();
        }
    }
}
