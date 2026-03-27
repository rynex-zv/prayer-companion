#if ANDROID
using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Graphics;
using Android.Widget;
using PrayAdFree.Core.Models;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = true, Label = "@string/widget_prayer_times_label")]
[IntentFilterAttribute([AppWidgetManager.ActionAppwidgetUpdate])]
[MetaData("android.appwidget.provider", Resource = "@xml/prayer_times_widget_info")]
public sealed class PrayerTimesWidgetProvider : AppWidgetProvider {
    private static readonly int[] NameIdsRegular = [
        Resource.Id.prayer_row_1_name,
        Resource.Id.prayer_row_2_name,
        Resource.Id.prayer_row_3_name,
        Resource.Id.prayer_row_4_name,
        Resource.Id.prayer_row_5_name,
        Resource.Id.prayer_row_6_name
    ];

    private static readonly int[] TimeIdsRegular = [
        Resource.Id.prayer_row_1_time,
        Resource.Id.prayer_row_2_time,
        Resource.Id.prayer_row_3_time,
        Resource.Id.prayer_row_4_time,
        Resource.Id.prayer_row_5_time,
        Resource.Id.prayer_row_6_time
    ];

    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds) {
        if (context == null) {
            return;
        }

        WidgetUpdateCoordinator.RequestImmediateRefresh(context, "PrayerWidgetUpdate");
    }

    public override void OnAppWidgetOptionsChanged(Context? context, AppWidgetManager? appWidgetManager, int appWidgetId, global::Android.OS.Bundle? newOptions) {
        if (context == null) {
            return;
        }

        WidgetUpdateCoordinator.RequestImmediateRefresh(context, "PrayerWidgetResize");
    }

    public override void OnEnabled(Context? context) {
        if (context == null) {
            return;
        }

        WidgetUpdateCoordinator.RequestImmediateRefresh(context, "PrayerWidgetEnabled");
    }

    public override void OnDisabled(Context? context) {
        if (context == null) {
            return;
        }

        WidgetUpdateCoordinator.RequestImmediateRefresh(context, "PrayerWidgetDisabled");
    }

    internal static void UpdateWidgets(Context context, AppWidgetManager manager, int[] appWidgetIds, AndroidPrayerWidgetData? data) {
        foreach (var appWidgetId in appWidgetIds) {
            var size = AndroidWidgetEnvironment.ResolveSize(manager.GetAppWidgetOptions(appWidgetId));
            var layout = size switch {
                WidgetDisplaySize.Tiny => Resource.Layout.widget_prayer_times_tiny,
                WidgetDisplaySize.Small => Resource.Layout.widget_prayer_times_compact,
                WidgetDisplaySize.Large => Resource.Layout.widget_prayer_times_expanded,
                _ => Resource.Layout.widget_prayer_times_regular
            };

            var views = new RemoteViews(context.PackageName, layout);
            BindCommonHeader(views, data);

            if (data?.Snapshot?.DailyPrayer == null || data.Today == null) {
                BindPlaceholder(views, size);
            } else if (size == WidgetDisplaySize.Tiny) {
                BindTiny(views, data.Snapshot.DailyPrayer, data.Settings);
            } else if (size == WidgetDisplaySize.Small) {
                BindCompact(views, data.Snapshot.DailyPrayer, data.Settings);
            } else {
                BindRows(views, data.Snapshot.DailyPrayer, data.Settings);
                if (size == WidgetDisplaySize.Large) {
                    views.SetTextViewText(Resource.Id.prayer_widget_gregorian, data.Today.Date.ToString("ddd, dd MMM yyyy"));
                    views.SetTextViewText(Resource.Id.prayer_widget_hijri, data.Today.Hijri.Date);
                    views.SetTextViewText(Resource.Id.prayer_widget_location, data.LocationTitle);
                } else {
                    views.SetTextViewText(Resource.Id.prayer_widget_subtitle, data.LocationTitle);
                }
            }

            manager.UpdateAppWidget(appWidgetId, views);
        }
    }

    private static void BindCommonHeader(RemoteViews views, AndroidPrayerWidgetData? data) {
        views.SetTextViewText(Resource.Id.prayer_widget_title, LocalizationManager.Translate("TodayPrayTimesLabel"));
        if (data?.Snapshot?.DailyPrayer != null) {
            var nextPrayer = data.Snapshot.DailyPrayer;
            var suffix = nextPrayer.IsNextPrayerTomorrow ? $" • {AndroidWidgetEnvironment.TranslateTomorrow()}" : string.Empty;
            views.SetTextViewText(
                Resource.Id.prayer_widget_next,
                $"{LocalizationManager.Translate("NextPrayer")}: {LocalizationManager.TranslatePrayer(nextPrayer.NextPrayerId)}{suffix}");
        } else {
            views.SetTextViewText(Resource.Id.prayer_widget_next, "Updating...");
        }
    }

    private static void BindPlaceholder(RemoteViews views, WidgetDisplaySize size) {
        if (size == WidgetDisplaySize.Tiny) {
            views.SetTextViewText(Resource.Id.prayer_widget_tiny_name, "--");
            views.SetTextViewText(Resource.Id.prayer_widget_tiny_time, "--");
            return;
        }

        if (size == WidgetDisplaySize.Small) {
            views.SetTextViewText(Resource.Id.prayer_widget_next_name, "--");
            views.SetTextViewText(Resource.Id.prayer_widget_next_time, "--");
            views.SetTextViewText(Resource.Id.prayer_widget_follow_1_name, "--");
            views.SetTextViewText(Resource.Id.prayer_widget_follow_1_time, "--");
            views.SetTextViewText(Resource.Id.prayer_widget_follow_2_name, "--");
            views.SetTextViewText(Resource.Id.prayer_widget_follow_2_time, "--");
            return;
        }

        foreach (var id in NameIdsRegular) {
            views.SetTextViewText(id, "--");
        }

        foreach (var id in TimeIdsRegular) {
            views.SetTextViewText(id, "--");
        }
    }

    private static void BindTiny(RemoteViews views, DailyPrayerSnapshot snapshot, PrayAdFree.Core.Models.AppSettings settings) {
        views.SetTextViewText(Resource.Id.prayer_widget_tiny_name, LocalizationManager.TranslatePrayer(snapshot.NextPrayerId));
        views.SetTextViewText(Resource.Id.prayer_widget_tiny_time, FormatPrayerTime(snapshot.NextPrayerTime, snapshot.NextPrayerBaseTime, settings));
    }

    private static void BindCompact(RemoteViews views, DailyPrayerSnapshot snapshot, PrayAdFree.Core.Models.AppSettings settings) {
        views.SetTextViewText(Resource.Id.prayer_widget_next_name, LocalizationManager.TranslatePrayer(snapshot.NextPrayerId));
        views.SetTextViewText(Resource.Id.prayer_widget_next_time, FormatPrayerTime(snapshot.NextPrayerTime, snapshot.NextPrayerBaseTime, settings));

        var following = GetFollowingEntries(snapshot, 2);
        SetCompactRow(views, Resource.Id.prayer_widget_follow_1_name, Resource.Id.prayer_widget_follow_1_time, following.ElementAtOrDefault(0), settings);
        SetCompactRow(views, Resource.Id.prayer_widget_follow_2_name, Resource.Id.prayer_widget_follow_2_time, following.ElementAtOrDefault(1), settings);
    }

    private static void BindRows(RemoteViews views, DailyPrayerSnapshot snapshot, PrayAdFree.Core.Models.AppSettings settings) {
        var normalColor = global::Android.Graphics.Color.ParseColor("#F5F7FA");
        var accentColor = global::Android.Graphics.Color.ParseColor("#6DE7C8");

        for (var i = 0; i < NameIdsRegular.Length; i++) {
            var entry = i < snapshot.Entries.Count ? snapshot.Entries[i] : null;
            if (entry == null) {
                views.SetTextViewText(NameIdsRegular[i], "--");
                views.SetTextViewText(TimeIdsRegular[i], "--");
                continue;
            }

            views.SetTextViewText(NameIdsRegular[i], LocalizationManager.TranslatePrayer(entry.Prayer));
            views.SetTextViewText(TimeIdsRegular[i], FormatPrayerTime(entry.AdjustedTime, entry.ShowBaseTime ? entry.BaseTime : null, settings));
            views.SetTextColor(NameIdsRegular[i], entry.IsNext ? accentColor : normalColor);
            views.SetTextColor(TimeIdsRegular[i], entry.IsNext ? accentColor : normalColor);
        }
    }

    private static void SetCompactRow(RemoteViews views, int nameId, int timeId, DailyPrayerSnapshotEntry? entry, PrayAdFree.Core.Models.AppSettings settings) {
        if (entry == null) {
            views.SetTextViewText(nameId, "--");
            views.SetTextViewText(timeId, "--");
            return;
        }

        views.SetTextViewText(nameId, LocalizationManager.TranslatePrayer(entry.Prayer));
        views.SetTextViewText(timeId, FormatPrayerTime(entry.AdjustedTime, entry.ShowBaseTime ? entry.BaseTime : null, settings));
    }

    private static IReadOnlyList<DailyPrayerSnapshotEntry> GetFollowingEntries(DailyPrayerSnapshot snapshot, int count) {
        var list = snapshot.Entries.ToList();
        var start = list.FindIndex(item => item.IsNext);
        if (start < 0) {
            start = 0;
        }

        var results = new List<DailyPrayerSnapshotEntry>(count);
        for (var offset = 1; offset <= count; offset++) {
            results.Add(list[(start + offset) % list.Count]);
        }

        return results;
    }

    private static string FormatPrayerTime(DateTime adjustedTime, DateTime? baseTime, PrayAdFree.Core.Models.AppSettings settings) {
        var formatted = TimeFormatHelper.FormatTime(adjustedTime, settings.ClockFormat);
        if (!baseTime.HasValue) {
            return formatted;
        }

        return $"{formatted} ({TimeFormatHelper.FormatTime(baseTime.Value, settings.ClockFormat)})";
    }
}
#endif
