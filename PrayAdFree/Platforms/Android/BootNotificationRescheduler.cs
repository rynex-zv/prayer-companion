#if ANDROID
using Android.Content;
using Android.Util;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.Platforms.Android;

internal static class BootNotificationRescheduler {
    private const string LogTag = "PrayAdFree.Boot";

    public static async Task RescheduleAsync(Context context) {
        try {
            var settings = LoadSettings();
            if (!ShouldSchedule(settings)) {
                Log.Info(LogTag, "Skipping boot reschedule because notifications are disabled.");
                return;
            }

            if (!HasValidCoordinates(settings.Location.Latitude, settings.Location.Longitude)) {
                Log.Warn(LogTag, "Skipping boot reschedule because location coordinates are invalid.");
                return;
            }

            try {
                await LocalizationManager.InitializeAsync(settings.Language).ConfigureAwait(false);
            } catch {
            }

            var prayerTimesService = CreatePrayerTimesService(context);
            var days = await BuildDaysToScheduleAsync(prayerTimesService, settings).ConfigureAwait(false);
            if (days.Count == 0) {
                Log.Warn(LogTag, "No upcoming days found while rescheduling after boot.");
                return;
            }

            var scheduler = new LocalNotificationScheduler(new PrayerSchedulePlanner(), new AppLogger());
            await scheduler.ScheduleAsync(days, settings, CancellationToken.None, requestPermissions: false).ConfigureAwait(false);
            Log.Info(LogTag, $"Rescheduled {days.Count} day(s) of notifications after boot.");
        } catch (Exception ex) {
            Log.Error(LogTag, $"Boot reschedule failed: {ex}");
        }
    }

    private static AppSettings LoadSettings() {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrayAdFree",
            "app_settings.json");
        var store = new FileSettingsStore(path);
        var service = new SettingsService(store);
        return service.Load();
    }

    private static PrayerTimesService CreatePrayerTimesService(Context context) {
        var cacheDirectory = context.FilesDir?.AbsolutePath;
        if (string.IsNullOrWhiteSpace(cacheDirectory)) {
            cacheDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        var client = new AladhanPrayerTimesClient(new HttpClient());
        var cache = new PrayerTimesCache(cacheDirectory!);
        return new PrayerTimesService(client, cache);
    }

    private static async Task<List<PrayerDay>> BuildDaysToScheduleAsync(PrayerTimesService prayerTimesService, AppSettings settings) {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var currentDate = DateTime.Today;
        var currentMonth = await prayerTimesService
            .GetMonthAsync(settings, currentDate.Year, currentDate.Month, CancellationToken.None)
            .ConfigureAwait(false);

        var daysToSchedule = currentMonth.Days
            .Where(item => item.Date >= today)
            .OrderBy(item => item.Date)
            .ToList();

        if (daysToSchedule.Count < 30) {
            var nextMonthDate = currentDate.AddMonths(1);
            var nextMonth = await prayerTimesService
                .GetMonthAsync(settings, nextMonthDate.Year, nextMonthDate.Month, CancellationToken.None)
                .ConfigureAwait(false);

            foreach (var day in nextMonth.Days.Where(item => item.Date >= today).OrderBy(item => item.Date)) {
                daysToSchedule.Add(day);
            }
        }

        return daysToSchedule
            .GroupBy(item => item.Date)
            .Select(group => group.First())
            .OrderBy(item => item.Date)
            .Take(45)
            .ToList();
    }

    private static bool ShouldSchedule(AppSettings settings) {
        var hasFastingReminders = settings.FastingReminders.ImsakRemindersMinutes.Count > 0
            || settings.FastingReminders.IftarRemindersMinutes.Count > 0;
        var hasAdhanReminders = settings.Notifications.ReminderOffsetsMinutes.Count > 0;
        return settings.Notifications.EnableAdhan || hasFastingReminders || hasAdhanReminders;
    }

    private static bool HasValidCoordinates(double latitude, double longitude) {
        return latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180 &&
               !(Math.Abs(latitude) < 0.0001 && Math.Abs(longitude) < 0.0001);
    }
}
#endif
