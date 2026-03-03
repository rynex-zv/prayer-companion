using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
using System.Linq;

namespace Pray_Ad_Free.Services;

public sealed class PrayerDataService {
    private readonly SettingsService _settingsService;
    private readonly ILocationProvider _locationProvider;
    private readonly PrayerTimesService _prayerTimesService;
    private readonly ILocalNotificationScheduler _notificationScheduler;
    public event EventHandler<AppSettings>? SettingsChanged;

    public PrayerDataService(
        SettingsService settingsService,
        ILocationProvider locationProvider,
        PrayerTimesService prayerTimesService,
        ILocalNotificationScheduler notificationScheduler) {
        _settingsService = settingsService;
        _locationProvider = locationProvider;
        _prayerTimesService = prayerTimesService;
        _notificationScheduler = notificationScheduler;
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

    public async Task<AppSettings> UpdateLocationAsync(AppSettings settings, CancellationToken cancellationToken) {
        var updatedLocation = await _locationProvider.GetLocationAsync(settings.Location, cancellationToken).ConfigureAwait(false);
        if (updatedLocation.LastUpdatedUtc != settings.Location.LastUpdatedUtc) {
            settings = new AppSettings {
                Location = updatedLocation,
                Method = settings.Method,
                Madhhab = settings.Madhhab,
                HighLatitudeRule = settings.HighLatitudeRule,
                Offsets = settings.Offsets,
                FastingOffsets = settings.FastingOffsets,
                FastingReminders = settings.FastingReminders,
                Notifications = settings.Notifications,
                ClockFormat = settings.ClockFormat,
                TextScale = settings.TextScale,
                Language = settings.Language,
                LanguageSelected = settings.LanguageSelected,
                ThemeMode = settings.ThemeMode,
                ThemeVariant = settings.ThemeVariant,
                AccentIndex = settings.AccentIndex
            };
            SaveSettings(settings);
        }

        return settings;
    }

    public async Task ScheduleNotificationsAsync(AppSettings settings, PrayerMonth month, CancellationToken cancellationToken) {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var day = month.Days.FirstOrDefault(item => item.Date == today);
        if (day == null) {
            return;
        }

        await _notificationScheduler.ScheduleAsync(new[] { day }, settings, cancellationToken).ConfigureAwait(false);
    }
}
