using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
using System.Linq;

namespace Pray_Ad_Free.Services;

public sealed class PrayerDataService {
    private static readonly TimeSpan GpsRefreshInterval = TimeSpan.FromMinutes(15);
    private const double MeaningfulMovementMeters = 500;

    private readonly SettingsService _settingsService;
    private readonly ILocationProvider _locationProvider;
    private readonly PrayerTimesService _prayerTimesService;
    private readonly ILocalNotificationScheduler _notificationScheduler;
    private readonly IAppLogger _logger;
    private readonly SemaphoreSlim _locationUpdateGate = new(1, 1);
    private DateTime _lastGpsRefreshUtc = DateTime.MinValue;

    public event EventHandler<AppSettings>? SettingsChanged;

    public PrayerDataService(
        SettingsService settingsService,
        ILocationProvider locationProvider,
        PrayerTimesService prayerTimesService,
        ILocalNotificationScheduler notificationScheduler,
        IAppLogger logger) {
        _settingsService = settingsService;
        _locationProvider = locationProvider;
        _prayerTimesService = prayerTimesService;
        _notificationScheduler = notificationScheduler;
        _logger = logger;
    }

    public AppSettings LoadSettings() => _settingsService.Load();

    public void SaveSettings(AppSettings settings) {
        _settingsService.Save(settings);
        SettingsChanged?.Invoke(this, settings);
    }

    public async Task<PrayerMonth> GetMonthAsync(AppSettings settings, DateTime date, CancellationToken cancellationToken) {
        var updatedSettings = await UpdateLocationAsync(settings, cancellationToken).ConfigureAwait(false);
        return await _prayerTimesService.GetMonthAsync(updatedSettings, date.Year, date.Month, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PrayerDay?> GetTodayAsync(AppSettings settings, CancellationToken cancellationToken) {
        var month = await GetMonthAsync(settings, DateTime.Today, cancellationToken).ConfigureAwait(false);
        return month.Days.FirstOrDefault(day => day.Date == DateOnly.FromDateTime(DateTime.Today));
    }

    public async Task<AppSettings> UpdateLocationAsync(AppSettings settings, CancellationToken cancellationToken, bool forceRefresh = false) {
        await _locationUpdateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            if (settings.Location.Mode == LocationMode.Gps &&
                LocationUpdatePolicy.ShouldThrottleGpsRefresh(DateTime.UtcNow, _lastGpsRefreshUtc, GpsRefreshInterval, forceRefresh)) {
                _logger.LogEvent("GpsRefreshSkipped", "throttled");
                return settings;
            }

            var updatedLocation = await _locationProvider.GetLocationAsync(settings.Location, cancellationToken).ConfigureAwait(false);

            if (settings.Location.Mode == LocationMode.Gps) {
                _lastGpsRefreshUtc = DateTime.UtcNow;
            }

            if (!LocationUpdatePolicy.HasMeaningfulLocationChange(
                    settings.Location,
                    updatedLocation,
                    MeaningfulMovementMeters,
                    out var distanceMeters)) {
                if (settings.Location.Mode == LocationMode.Gps) {
                    _logger.LogEvent("GpsRefreshSkipped", "no_meaningful_change");
                }
                return settings;
            }

            if (settings.Location.Mode == LocationMode.Gps) {
                _logger.LogEvent("GpsRefreshApplied", $"distance={distanceMeters:F1}");
            }

            settings = CloneSettingsWithLocation(settings, updatedLocation);
            SaveSettings(settings);
            return settings;
        } finally {
            _locationUpdateGate.Release();
        }
    }

    public async Task ScheduleNotificationsAsync(AppSettings settings, PrayerMonth month, CancellationToken cancellationToken, bool requestPermissions = true) {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var daysToSchedule = month.Days
            .Where(item => item.Date >= today)
            .OrderBy(item => item.Date)
            .ToList();

        if (daysToSchedule.Count < 30) {
            var nextMonthDate = DateTime.Today.AddMonths(1);
            var nextMonth = await _prayerTimesService
                .GetMonthAsync(settings, nextMonthDate.Year, nextMonthDate.Month, cancellationToken)
                .ConfigureAwait(false);

            foreach (var day in nextMonth.Days.Where(item => item.Date >= today).OrderBy(item => item.Date)) {
                daysToSchedule.Add(day);
            }
        }

        var finalDays = daysToSchedule
            .GroupBy(item => item.Date)
            .Select(group => group.First())
            .OrderBy(item => item.Date)
            .Take(45)
            .ToList();

        if (finalDays.Count == 0) {
            return;
        }

        await _notificationScheduler.ScheduleAsync(finalDays, settings, cancellationToken, requestPermissions).ConfigureAwait(false);
    }

    private static AppSettings CloneSettingsWithLocation(AppSettings settings, LocationSettings location) {
        return new AppSettings {
            Location = location,
            Method = settings.Method,
            Madhhab = settings.Madhhab,
            HighLatitudeRule = settings.HighLatitudeRule,
            Offsets = settings.Offsets,
            FastingOffsets = settings.FastingOffsets,
            FastingReminders = settings.FastingReminders,
            Notifications = settings.Notifications,
            Qibla = settings.Qibla,
            ClockFormat = settings.ClockFormat,
            TextScale = settings.TextScale,
            Tasbih = settings.Tasbih,
            Language = settings.Language,
            LanguageSelected = settings.LanguageSelected,
            ThemeMode = settings.ThemeMode,
            ThemeVariant = settings.ThemeVariant,
            AccentIndex = settings.AccentIndex
        };
    }
}
