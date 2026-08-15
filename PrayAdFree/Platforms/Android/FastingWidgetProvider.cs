#if ANDROID
using Android.App;
using Android.Appwidget;
using Android.Content;
using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Platforms.Android;

#if PRAY_WIDGETS
[BroadcastReceiver(Enabled = true, Exported = true, Label = "@string/widget_fasting_label")]
#else
[BroadcastReceiver(Enabled = false, Exported = false, Label = "@string/widget_fasting_label")]
#endif
[IntentFilterAttribute([AppWidgetManager.ActionAppwidgetUpdate])]
[MetaData("android.appwidget.provider", Resource = "@xml/fasting_widget_info")]
public sealed class FastingWidgetProvider : AppWidgetProvider {
    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds) {
        if (context != null) WidgetUpdateCoordinator.RequestImmediateRefresh(context, "FastingWidgetUpdate");
    }

    public override void OnAppWidgetOptionsChanged(Context? context, AppWidgetManager? appWidgetManager, int appWidgetId, global::Android.OS.Bundle? newOptions) {
        if (context != null) WidgetUpdateCoordinator.RequestImmediateRefresh(context, "FastingWidgetResize");
    }

    public override void OnEnabled(Context? context) {
        if (context != null) WidgetUpdateCoordinator.RequestImmediateRefresh(context, "FastingWidgetEnabled");
    }

    public override void OnDisabled(Context? context) {
        if (context != null) WidgetUpdateCoordinator.RequestImmediateRefresh(context, "FastingWidgetDisabled");
    }

    public override void OnDeleted(Context? context, int[]? appWidgetIds) {
        if (context == null || appWidgetIds == null) return;
        var profiles = AndroidWidgetEnvironment.CreateWidgetProfileService();
        foreach (var id in appWidgetIds) profiles.Unassign($"android:fasting:{id}");
    }

    internal static void UpdateWidgets(Context context, AppWidgetManager manager, int[] ids, AndroidPrayerWidgetData? data) =>
        AndroidSharedWidgetRenderer.UpdateWidgets(
            context,
            manager,
            ids,
            data?.Projection ?? new WidgetProjection { Status = "error", Error = "Fasting data is unavailable.", GeneratedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
            WidgetTemplateKind.Fasting,
            "fasting",
            data?.ProjectionBuildMilliseconds ?? 0);
}
#endif
