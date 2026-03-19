#if ANDROID
using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilterAttribute([AppWidgetManager.ActionAppwidgetUpdate])]
[MetaData("android.appwidget.provider", Resource = "@xml/fasting_widget_info")]
public sealed class FastingWidgetProvider : AppWidgetProvider {
    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds) {
        if (context == null) {
            return;
        }

        WidgetUpdateCoordinator.RequestImmediateRefresh(context, "FastingWidgetUpdate");
    }

    public override void OnAppWidgetOptionsChanged(Context? context, AppWidgetManager? appWidgetManager, int appWidgetId, global::Android.OS.Bundle? newOptions) {
        if (context == null) {
            return;
        }

        WidgetUpdateCoordinator.RequestImmediateRefresh(context, "FastingWidgetResize");
    }

    public override void OnEnabled(Context? context) {
        if (context == null) {
            return;
        }

        WidgetUpdateCoordinator.RequestImmediateRefresh(context, "FastingWidgetEnabled");
    }

    public override void OnDisabled(Context? context) {
        if (context == null) {
            return;
        }

        WidgetUpdateCoordinator.RequestImmediateRefresh(context, "FastingWidgetDisabled");
    }

    internal static void UpdateWidgets(Context context, AppWidgetManager manager, int[] appWidgetIds, AndroidPrayerWidgetData? data) {
        foreach (var appWidgetId in appWidgetIds) {
            var size = AndroidWidgetEnvironment.ResolveSize(manager.GetAppWidgetOptions(appWidgetId));
            var layout = size switch {
                WidgetDisplaySize.Tiny => Resource.Layout.widget_fasting_tiny,
                WidgetDisplaySize.Small => Resource.Layout.widget_fasting_compact,
                WidgetDisplaySize.Large => Resource.Layout.widget_fasting_large,
                _ => Resource.Layout.widget_fasting_regular
            };
            var views = new RemoteViews(context.PackageName, layout);

            views.SetTextViewText(Resource.Id.fasting_widget_title, LocalizationManager.Translate("ImsakAndIftarTitle"));
            if (data?.Snapshot?.Fasting == null) {
                BindPlaceholder(views, size);
            } else if (size == WidgetDisplaySize.Tiny) {
                var nextKey = data.Snapshot.Fasting.IsImsakNext ? "Imsak" : "Iftar";
                views.SetTextViewText(Resource.Id.fasting_widget_tiny_next_label, LocalizationManager.Translate(nextKey));
                views.SetTextViewText(
                    Resource.Id.fasting_widget_tiny_next_time,
                    TimeFormatHelper.FormatTime(data.Snapshot.Fasting.NextTargetTime, data.Settings.ClockFormat));
            } else if (size == WidgetDisplaySize.Small) {
                var nextKey = data.Snapshot.Fasting.IsImsakNext ? "Imsak" : "Iftar";
                views.SetTextViewText(Resource.Id.fasting_widget_next_label, LocalizationManager.Translate(nextKey));
                views.SetTextViewText(
                    Resource.Id.fasting_widget_next_time,
                    TimeFormatHelper.FormatTime(data.Snapshot.Fasting.NextTargetTime, data.Settings.ClockFormat));
                views.SetTextViewText(
                    Resource.Id.fasting_widget_countdown,
                    AndroidWidgetEnvironment.FormatCountdown(data.Snapshot.Fasting.Remaining));
            } else {
                views.SetTextViewText(Resource.Id.fasting_widget_imsak_time, TimeFormatHelper.FormatTime(data.Snapshot.Fasting.ImsakTime, data.Settings.ClockFormat));
                views.SetTextViewText(Resource.Id.fasting_widget_iftar_time, TimeFormatHelper.FormatTime(data.Snapshot.Fasting.IftarTime, data.Settings.ClockFormat));
                var nextKey = data.Snapshot.Fasting.IsImsakNext ? "Imsak" : "Iftar";
                views.SetTextViewText(Resource.Id.fasting_widget_next_target, LocalizationManager.Translate(nextKey));
                if (size == WidgetDisplaySize.Large && data.Today != null) {
                    views.SetTextViewText(Resource.Id.fasting_widget_location, data.LocationTitle);
                    views.SetTextViewText(Resource.Id.fasting_widget_date, data.Today.Date.ToString("ddd, dd MMM yyyy"));
                }

                views.SetTextViewText(Resource.Id.fasting_widget_countdown, AndroidWidgetEnvironment.FormatCountdown(data.Snapshot.Fasting.Remaining));
            }

            manager.UpdateAppWidget(appWidgetId, views);
        }
    }

    private static void BindPlaceholder(RemoteViews views, WidgetDisplaySize size) {
        if (size == WidgetDisplaySize.Tiny) {
            views.SetTextViewText(Resource.Id.fasting_widget_tiny_next_label, "--");
            views.SetTextViewText(Resource.Id.fasting_widget_tiny_next_time, "--");
            return;
        }

        if (size == WidgetDisplaySize.Small) {
            views.SetTextViewText(Resource.Id.fasting_widget_next_label, "--");
            views.SetTextViewText(Resource.Id.fasting_widget_next_time, "--");
            views.SetTextViewText(Resource.Id.fasting_widget_countdown, "--:--");
            return;
        }

        views.SetTextViewText(Resource.Id.fasting_widget_imsak_time, "--");
        views.SetTextViewText(Resource.Id.fasting_widget_iftar_time, "--");
        views.SetTextViewText(Resource.Id.fasting_widget_next_target, "--");
        views.SetTextViewText(Resource.Id.fasting_widget_countdown, "--:--");
        if (size == WidgetDisplaySize.Large) {
            views.SetTextViewText(Resource.Id.fasting_widget_location, "--");
            views.SetTextViewText(Resource.Id.fasting_widget_date, "--");
        }
    }
}
#endif
