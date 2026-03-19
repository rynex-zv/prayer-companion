#if ANDROID
using Android.App;
using Android.Appwidget;
using Android.Content;

namespace Pray_Ad_Free.Platforms.Android;

internal static class WidgetUpdateCoordinator {
    internal const string RefreshAction = "com.rynex.prayadfree.widget.REFRESH";
    private const int RefreshRequestCode = 40401;

    public static void RequestImmediateRefresh(string reason) {
        var context = global::Android.App.Application.Context;
        if (context != null) {
            RequestImmediateRefresh(context, reason);
        }
    }

    public static void RequestImmediateRefresh(Context context, string reason) {
        var appContext = context.ApplicationContext ?? context;
        _ = Task.Run(async () => await UpdateAllAsync(appContext, reason).ConfigureAwait(false));
    }

    public static async Task UpdateAllAsync(Context context, string reason) {
        var manager = AppWidgetManager.GetInstance(context);
        if (manager == null) {
            return;
        }

        var prayerIds = manager.GetAppWidgetIds(new ComponentName(context, Java.Lang.Class.FromType(typeof(PrayerTimesWidgetProvider)))) ?? [];
        var fastingIds = manager.GetAppWidgetIds(new ComponentName(context, Java.Lang.Class.FromType(typeof(FastingWidgetProvider)))) ?? [];
        var tasbihIds = manager.GetAppWidgetIds(new ComponentName(context, Java.Lang.Class.FromType(typeof(TasbihWidgetProvider)))) ?? [];

        if (tasbihIds.Length > 0) {
            TasbihWidgetProvider.UpdateWidgets(context, manager, tasbihIds);
        }

        AndroidPrayerWidgetData? prayerData = null;
        if (prayerIds.Length > 0 || fastingIds.Length > 0) {
            prayerData = await AndroidWidgetEnvironment.LoadPrayerDataAsync(context, DateTime.Now).ConfigureAwait(false);
        }

        if (prayerIds.Length > 0) {
            PrayerTimesWidgetProvider.UpdateWidgets(context, manager, prayerIds, prayerData);
        }

        if (fastingIds.Length > 0) {
            FastingWidgetProvider.UpdateWidgets(context, manager, fastingIds, prayerData);
        }

        ScheduleNextRefresh(context, prayerIds.Length > 0 || fastingIds.Length > 0);
    }

    public static void ScheduleNextRefresh(Context context, bool enabled) {
        var appContext = context.ApplicationContext ?? context;
        if (appContext.GetSystemService(Context.AlarmService) is not AlarmManager alarmManager) {
            return;
        }

        var pendingIntent = BuildRefreshPendingIntent(appContext);
        alarmManager.Cancel(pendingIntent);

        if (!enabled) {
            pendingIntent.Cancel();
            return;
        }

        var localNow = DateTime.Now;
        var nextMinute = new DateTime(localNow.Year, localNow.Month, localNow.Day, localNow.Hour, localNow.Minute, 0, DateTimeKind.Local)
            .AddMinutes(1);
        var triggerAt = new DateTimeOffset(nextMinute).ToUnixTimeMilliseconds();

        try {
            if (OperatingSystem.IsAndroidVersionAtLeast(23)) {
                alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAt, pendingIntent);
            } else {
                alarmManager.SetExact(AlarmType.RtcWakeup, triggerAt, pendingIntent);
            }
        } catch (Java.Lang.SecurityException) {
            alarmManager.Set(AlarmType.RtcWakeup, triggerAt, pendingIntent);
        }
    }

    private static PendingIntent BuildRefreshPendingIntent(Context context) {
        var intent = new Intent(context, typeof(WidgetRefreshReceiver));
        intent.SetAction(RefreshAction);
        var flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23)) {
            flags |= PendingIntentFlags.Immutable;
        }

        return PendingIntent.GetBroadcast(context, RefreshRequestCode, intent, flags)!;
    }
}
#endif
