#if ANDROID
using Android.Content;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.Platforms.Android;

internal static class AndroidWidgetEnvironment {
    public static AppSettings LoadSettings() {
        var store = new FileSettingsStore(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrayAdFree",
            "app_settings.json"));
        return new SettingsService(store).Load();
    }

    public static TasbihWidgetStateStore CreateTasbihStateStore() {
        return new TasbihWidgetStateStore(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrayAdFree",
            "widget_state.json"));
    }

    public static async Task InitializeLocalizationAsync(AppSettings settings) {
        try {
            await LocalizationManager.InitializeAsync(settings.Language).ConfigureAwait(false);
            LocalizationManager.SetLanguage(settings.Language);
        } catch {
        }
    }

    public static async Task<AndroidPrayerWidgetData?> LoadPrayerDataAsync(Context context, DateTime now) {
        var settings = LoadSettings();
        await InitializeLocalizationAsync(settings).ConfigureAwait(false);

        if (!HasValidCoordinates(settings.Location.Latitude, settings.Location.Longitude)) {
            return new AndroidPrayerWidgetData {
                Settings = settings,
                LocationTitle = BuildLocationTitle(settings.Location)
            };
        }

        try {
            var prayerTimesService = CreatePrayerTimesService(context);
            var today = DateOnly.FromDateTime(now);
            var currentMonth = await prayerTimesService
                .GetMonthAsync(settings, now.Year, now.Month, CancellationToken.None)
                .ConfigureAwait(false);
            var todayDay = currentMonth.Days.FirstOrDefault(day => day.Date == today);
            var tomorrowDay = currentMonth.Days.FirstOrDefault(day => day.Date == today.AddDays(1));

            if (tomorrowDay == null) {
                var nextMonthDate = now.AddMonths(1);
                var nextMonth = await prayerTimesService
                    .GetMonthAsync(settings, nextMonthDate.Year, nextMonthDate.Month, CancellationToken.None)
                    .ConfigureAwait(false);
                tomorrowDay = nextMonth.Days.FirstOrDefault(day => day.Date == today.AddDays(1));
            }

            WidgetSnapshotResult? snapshot = null;
            if (todayDay != null) {
                snapshot = new WidgetSnapshotFactory().Build(todayDay, tomorrowDay, settings, now);
            }

            return new AndroidPrayerWidgetData {
                Settings = settings,
                Today = todayDay,
                Tomorrow = tomorrowDay,
                Snapshot = snapshot,
                LocationTitle = BuildLocationTitle(settings.Location)
            };
        } catch {
            return new AndroidPrayerWidgetData {
                Settings = settings,
                LocationTitle = BuildLocationTitle(settings.Location)
            };
        }
    }

    public static WidgetDisplaySize ResolveSize(global::Android.OS.Bundle? options) {
        var minWidth = options?.GetInt(global::Android.Appwidget.AppWidgetManager.OptionAppwidgetMinWidth, 0) ?? 0;
        var minHeight = options?.GetInt(global::Android.Appwidget.AppWidgetManager.OptionAppwidgetMinHeight, 0) ?? 0;

        if (minWidth < 120 || minHeight < 90) {
            return WidgetDisplaySize.Tiny;
        }

        if (minWidth < 180 || minHeight < 120) {
            return WidgetDisplaySize.Small;
        }

        if (minWidth < 250 || minHeight < 180) {
            return WidgetDisplaySize.Medium;
        }

        return WidgetDisplaySize.Large;
    }

    public static string BuildLocationTitle(LocationSettings location) {
        if (!string.IsNullOrWhiteSpace(location.City) && !string.IsNullOrWhiteSpace(location.Country)) {
            return $"{location.City}, {location.Country}";
        }

        return "Current location";
    }

    public static string TranslateTomorrow() {
        return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch {
            "ar" => "غدًا",
            "tr" => "Yarin",
            "fr" => "Demain",
            _ => "Tomorrow"
        };
    }

    public static string FormatCountdown(TimeSpan remaining) {
        if (remaining < TimeSpan.Zero) {
            remaining = TimeSpan.Zero;
        }

        var totalHours = (int)Math.Floor(remaining.TotalHours);
        return $"{totalHours:00}:{remaining.Minutes:00}";
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

    private static bool HasValidCoordinates(double latitude, double longitude) {
        return latitude is >= -90 and <= 90 &&
               longitude is >= -180 and <= 180 &&
               !(Math.Abs(latitude) < 0.0001 && Math.Abs(longitude) < 0.0001);
    }
}

internal sealed class AndroidPrayerWidgetData {
    public AppSettings Settings { get; init; } = new();
    public PrayerDay? Today { get; init; }
    public PrayerDay? Tomorrow { get; init; }
    public WidgetSnapshotResult? Snapshot { get; init; }
    public string LocationTitle { get; init; } = "";
}
#endif
