#if ANDROID
using Android.Content;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.Platforms.Android;

internal static class AndroidWidgetEnvironment {
    private static readonly Lazy<WidgetProfileService> WidgetProfiles = new(() => new WidgetProfileService(
        new JsonFileWidgetProfileRepository(WidgetStoragePaths.ProfilePath)), LazyThreadSafetyMode.ExecutionAndPublication);
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

    public static WidgetProfileService CreateWidgetProfileService() {
        var profiles = WidgetProfiles.Value;
        profiles.RefreshFromStorage();
        return profiles;
    }

    public static async Task InitializeLocalizationAsync(AppSettings settings) {
        try {
            await LocalizationManager.InitializeAsync(settings.Language).ConfigureAwait(false);
            LocalizationManager.SetLanguage(settings.Language);
        } catch {
        }
    }

    public static async Task<AndroidPrayerWidgetData?> LoadPrayerDataAsync(Context context, DateTime now) {
        var started = System.Diagnostics.Stopwatch.StartNew();
        var settings = LoadSettings();
        await InitializeLocalizationAsync(settings).ConfigureAwait(false);

        var projectionFactory = new WidgetProjectionFactory();
        if (!HasValidCoordinates(settings.Location.Latitude, settings.Location.Longitude)) {
            return new AndroidPrayerWidgetData {
                Settings = settings,
                LocationTitle = BuildLocationTitle(settings.Location),
                Projection = projectionFactory.Error("A confirmed location is required.", ResolveLanguage(settings.Language)),
                ProjectionBuildMilliseconds = started.ElapsedMilliseconds
            };
        }

        try {
            var today = DateOnly.FromDateTime(now);
            var prayerFactory = new WebPrayerMonthFactory();
            PrayerDay todayDay = prayerFactory.BuildDay(settings, today);
            var tomorrowDay = prayerFactory.BuildDay(settings, today.AddDays(1));
            var snapshot = new WidgetSnapshotFactory().Build(todayDay, tomorrowDay, settings, now);

            return new AndroidPrayerWidgetData {
                Settings = settings,
                Today = todayDay,
                Tomorrow = tomorrowDay,
                Snapshot = snapshot,
                LocationTitle = BuildLocationTitle(settings.Location),
                Projection = projectionFactory.Build(todayDay, tomorrowDay, settings, now, ResolveLanguage(settings.Language), settings.Location.Source),
                ProjectionBuildMilliseconds = started.ElapsedMilliseconds
            };
        } catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) {
            return new AndroidPrayerWidgetData {
                Settings = settings,
                LocationTitle = BuildLocationTitle(settings.Location),
                Projection = projectionFactory.Error(exception.Message, ResolveLanguage(settings.Language)),
                ProjectionBuildMilliseconds = started.ElapsedMilliseconds
            };
        }
    }

    public static WidgetDisplaySize ResolveSize(global::Android.OS.Bundle? options) {
        return ResolveCapabilities(options).Family switch {
            WidgetFamily.Tiny => WidgetDisplaySize.Tiny,
            WidgetFamily.Compact => WidgetDisplaySize.Small,
            WidgetFamily.Medium => WidgetDisplaySize.Medium,
            _ => WidgetDisplaySize.Large
        };
    }

    public static WidgetHostCapabilities ResolveCapabilities(global::Android.OS.Bundle? options) {
        var minWidth = options?.GetInt(global::Android.Appwidget.AppWidgetManager.OptionAppwidgetMinWidth, 0) ?? 0;
        var minHeight = options?.GetInt(global::Android.Appwidget.AppWidgetManager.OptionAppwidgetMinHeight, 0) ?? 0;
        var maxWidth = options?.GetInt(global::Android.Appwidget.AppWidgetManager.OptionAppwidgetMaxWidth, minWidth) ?? minWidth;
        var maxHeight = options?.GetInt(global::Android.Appwidget.AppWidgetManager.OptionAppwidgetMaxHeight, minHeight) ?? minHeight;
        var hostCategory = (global::Android.Appwidget.AppWidgetCategory)(options?.GetInt(
            global::Android.Appwidget.AppWidgetManager.OptionAppwidgetHostCategory,
            (int)global::Android.Appwidget.AppWidgetCategory.HomeScreen) ?? (int)global::Android.Appwidget.AppWidgetCategory.HomeScreen);
        var lockScreen = (hostCategory & global::Android.Appwidget.AppWidgetCategory.Keyguard) != 0;
        return WidgetHostCapabilityResolver.ResolveAndroid(minWidth, maxWidth, minHeight, maxHeight, lockScreen);
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

    private static bool HasValidCoordinates(double latitude, double longitude) {
        return latitude is >= -90 and <= 90 &&
               longitude is >= -180 and <= 180 &&
               !(Math.Abs(latitude) < 0.0001 && Math.Abs(longitude) < 0.0001);
    }

    private static string ResolveLanguage(string language) => string.Equals(language, "ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "en";
}

internal sealed class AndroidPrayerWidgetData {
    public AppSettings Settings { get; init; } = new();
    public PrayerDay? Today { get; init; }
    public PrayerDay? Tomorrow { get; init; }
    public WidgetSnapshotResult? Snapshot { get; init; }
    public string LocationTitle { get; init; } = "";
    public WidgetProjection Projection { get; init; } = new() { Status = "error", Error = "Widget data is unavailable." };
    public long ProjectionBuildMilliseconds { get; init; }
}
#endif
