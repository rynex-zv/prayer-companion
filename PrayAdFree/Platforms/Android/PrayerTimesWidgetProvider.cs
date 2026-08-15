#if ANDROID
using Android.App;
using Android.Appwidget;
using Android.Content;
using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Platforms.Android;

#if PRAY_WIDGETS
[BroadcastReceiver(Enabled = true, Exported = true, Label = "@string/widget_prayer_times_label")]
#else
[BroadcastReceiver(Enabled = false, Exported = false, Label = "@string/widget_prayer_times_label")]
#endif
[IntentFilterAttribute([AppWidgetManager.ActionAppwidgetUpdate])]
[MetaData("android.appwidget.provider", Resource = "@xml/prayer_times_widget_info")]
public sealed class PrayerTimesWidgetProvider : AppWidgetProvider {
    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds) {
        if (context != null) WidgetUpdateCoordinator.RequestImmediateRefresh(context, "PrayerWidgetUpdate");
    }

    public override void OnAppWidgetOptionsChanged(Context? context, AppWidgetManager? appWidgetManager, int appWidgetId, global::Android.OS.Bundle? newOptions) {
        if (context != null) WidgetUpdateCoordinator.RequestImmediateRefresh(context, "PrayerWidgetResize");
    }

    public override void OnEnabled(Context? context) {
        if (context != null) WidgetUpdateCoordinator.RequestImmediateRefresh(context, "PrayerWidgetEnabled");
    }

    public override void OnDisabled(Context? context) {
        if (context != null) WidgetUpdateCoordinator.RequestImmediateRefresh(context, "PrayerWidgetDisabled");
    }

    public override void OnDeleted(Context? context, int[]? appWidgetIds) {
        if (context == null || appWidgetIds == null) return;
        var profiles = AndroidWidgetEnvironment.CreateWidgetProfileService();
        foreach (var id in appWidgetIds) profiles.Unassign($"android:prayer:{id}");
    }

    internal static void UpdateWidgets(Context context, AppWidgetManager manager, int[] ids, AndroidPrayerWidgetData? data) =>
        AndroidSharedWidgetRenderer.UpdateWidgets(
            context,
            manager,
            ids,
            data?.Projection ?? new WidgetProjection { Status = "error", Error = "Prayer data is unavailable.", GeneratedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
            WidgetTemplateKind.DailyPrayer,
            "prayer",
            data?.ProjectionBuildMilliseconds ?? 0);
}
#endif
