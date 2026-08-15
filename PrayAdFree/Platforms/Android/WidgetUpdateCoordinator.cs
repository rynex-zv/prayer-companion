#if ANDROID
using Android.App;
using Android.Appwidget;
using Android.Content;

namespace Pray_Ad_Free.Platforms.Android;

internal static class WidgetUpdateCoordinator {
    internal const string RefreshAction = "com.rynex.prayer.widget.REFRESH";
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

        var nextBoundary = prayerData?.Projection is { Status: "ready" } projection
            ? new[] { projection.NextPrayerAtUnixMilliseconds, projection.FastingTargetAtUnixMilliseconds }
                .Where(value => value > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                .DefaultIfEmpty(new DateTimeOffset(DateTime.Today.AddDays(1)).ToUnixTimeMilliseconds())
                .Min()
            : (long?)null;
        ScheduleNextRefresh(context, prayerIds.Length > 0 || fastingIds.Length > 0 ? nextBoundary : null);
    }

    public static void ScheduleNextRefresh(Context context, long? targetUnixMilliseconds) {
        var appContext = context.ApplicationContext ?? context;
        if (appContext.GetSystemService(Context.AlarmService) is not AlarmManager alarmManager) {
            return;
        }

        var pendingIntent = BuildRefreshPendingIntent(appContext);
        alarmManager.Cancel(pendingIntent);

        if (!targetUnixMilliseconds.HasValue) {
            pendingIntent.Cancel();
            return;
        }

        // RemoteViews Chronometer owns the live countdown. Wake native code only
        // after the next prayer/fasting boundary so the projection can advance.
        var triggerAt = Math.Max(
            DateTimeOffset.UtcNow.AddSeconds(5).ToUnixTimeMilliseconds(),
            targetUnixMilliseconds.Value + 2_000);

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
