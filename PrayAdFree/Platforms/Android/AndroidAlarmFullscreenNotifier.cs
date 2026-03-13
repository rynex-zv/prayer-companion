#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using Android.Util;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.Platforms.Android;

internal static class AndroidAlarmFullscreenNotifier {
    private const string AlarmChannelId = "adhan_alarm_fullscreen";
    private const int AlarmNotificationId = 54009;
    private const string LogTag = "PrayAdFree.Alarm";

    public static void Show(Context context, string payloadText) {
        if (string.IsNullOrWhiteSpace(payloadText)) {
            Log.Warn(LogTag, "Notifier.Show skipped because payload was empty");
            return;
        }

        EnsureChannel(context);

        var launchIntent = BuildAlarmLaunchIntent(context, payloadText);
        var launchPendingIntent = PendingIntent.GetActivity(
            context,
            AlarmNotificationId,
            launchIntent,
            NormalizePendingIntentFlags(PendingIntentFlags.UpdateCurrent))!;

        Notification.Builder builder;
        if (OperatingSystem.IsAndroidVersionAtLeast(26)) {
            builder = new Notification.Builder(context, AlarmChannelId);
        } else {
            builder = new Notification.Builder(context);
        }

        builder
            .SetSmallIcon(context.ApplicationInfo?.Icon ?? global::Android.Resource.Drawable.IcLockIdleAlarm)
            .SetContentTitle(ResolveTitle())
            .SetContentText(ResolveBody())
            .SetCategory(Notification.CategoryAlarm)
            .SetPriority((int)NotificationPriority.Max)
            .SetVisibility(NotificationVisibility.Public)
            .SetAutoCancel(true)
            .SetOngoing(true)
            .SetShowWhen(true)
            .SetVibrate(new long[] { 0, 180, 120, 180 })
            .SetContentIntent(launchPendingIntent);

        var canUseFullScreenIntent = CanUseFullScreenIntent(context);
        Log.Info(LogTag, $"Notifier.Show screenOnUnlocked={IsScreenOnAndUnlocked(context)} canUseFullScreenIntent={canUseFullScreenIntent}");
        if (canUseFullScreenIntent) {
            builder.SetFullScreenIntent(launchPendingIntent, true);
        }

        if (context.GetSystemService(Context.NotificationService) is NotificationManager manager) {
            manager.Notify(AlarmNotificationId, builder.Build());
            Log.Info(LogTag, "Notifier.Show posted fullscreen notification");
        }

        TryVisibleScreenRetryLaunch(context, launchPendingIntent);

        if (!canUseFullScreenIntent) {
            Log.Warn(LogTag, "Notifier.Show falling back to direct activity launch because full-screen intent is unavailable");
            LaunchActivity(context, payloadText);
        }
    }

    public static void LaunchActivity(Context context, string payloadText) {
        Log.Info(LogTag, "Notifier.LaunchActivity requested");
        TryDirectLaunch(context, payloadText);
    }

    public static void LaunchApp(Context context, string payloadText) {
        try {
            context.StartActivity(BuildAppLaunchIntent(payloadText));
            Log.Info(LogTag, "Notifier.LaunchApp succeeded");
        } catch {
            Log.Warn(LogTag, "Notifier.LaunchApp failed");
        }
    }

    public static bool ShouldOpenAppDirectly(Context context) {
        return IsScreenOnAndUnlocked(context);
    }

    public static void Cancel(Context? context) {
        if (context?.GetSystemService(Context.NotificationService) is not NotificationManager manager) {
            return;
        }

        manager.Cancel(AlarmNotificationId);
        Log.Debug(LogTag, "Notifier.Cancel removed fullscreen notification");
    }

    private static void EnsureChannel(Context context) {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26) ||
            context.GetSystemService(Context.NotificationService) is not NotificationManager manager) {
            return;
        }

        if (manager.GetNotificationChannel(AlarmChannelId) != null) {
            return;
        }

        var channel = new NotificationChannel(
            AlarmChannelId,
            "Prayer Alarm Full Screen",
            NotificationImportance.High) {
            Description = "Displays prayer alarms over the lock screen"
        };
        channel.SetSound(null, null);
        channel.EnableVibration(true);
        channel.SetVibrationPattern(new long[] { 0, 180, 120, 180 });
        channel.LockscreenVisibility = NotificationVisibility.Public;
        manager.CreateNotificationChannel(channel);
    }

    private static Intent BuildAlarmLaunchIntent(Context context, string payloadText) {
        var intent = new Intent(context, typeof(AlarmActivity));
        intent.SetAction(AdhanPlaybackService.AndroidAlarmAction);
        intent.PutExtra(AndroidAdhanAlarmScheduler.AlarmPayloadExtra, payloadText);
        intent.AddFlags(
            ActivityFlags.NewTask |
            ActivityFlags.SingleTop |
            ActivityFlags.ClearTop);
        return intent;
    }

    private static Intent BuildAppLaunchIntent(string payloadText) {
        var intent = new Intent(global::Android.App.Application.Context, typeof(global::Pray_Ad_Free.MainActivity));
        intent.SetAction(AdhanPlaybackService.AndroidAlarmAction);
        intent.PutExtra(AndroidAdhanAlarmScheduler.AlarmPayloadExtra, payloadText);
        intent.AddFlags(
            ActivityFlags.NewTask |
            ActivityFlags.SingleTop |
            ActivityFlags.ClearTop);
        return intent;
    }

    private static void TryDirectLaunch(Context context, string payloadText) {
        try {
            context.StartActivity(BuildAlarmLaunchIntent(context, payloadText));
            Log.Info(LogTag, "Notifier.TryDirectLaunch succeeded");
        } catch {
            Log.Warn(LogTag, "Notifier.TryDirectLaunch failed");
        }
    }

    private static void TryVisibleScreenRetryLaunch(Context context, PendingIntent launchPendingIntent) {
        if (!IsScreenOnAndUnlocked(context)) {
            Log.Debug(LogTag, "Notifier retry launch skipped because screen is not on and unlocked");
            return;
        }

        Log.Info(LogTag, "Notifier retry launch scheduled for visible unlocked screen");
        _ = Task.Run(async () => {
            await Task.Delay(250).ConfigureAwait(false);
            TrySendPendingIntent(launchPendingIntent);

            await Task.Delay(900).ConfigureAwait(false);
            TrySendPendingIntent(launchPendingIntent);
        });
    }

    private static bool IsScreenOnAndUnlocked(Context context) {
        try {
            if (context.GetSystemService(Context.PowerService) is not PowerManager powerManager ||
                !powerManager.IsInteractive) {
                return false;
            }

            if (context.GetSystemService(Context.KeyguardService) is not KeyguardManager keyguardManager) {
                return true;
            }

            return !keyguardManager.IsKeyguardLocked;
        } catch {
            return false;
        }
    }

    private static void TrySendPendingIntent(PendingIntent? pendingIntent) {
        if (pendingIntent == null) {
            return;
        }

        try {
            pendingIntent.Send();
            Log.Info(LogTag, "Notifier pending intent send succeeded");
        } catch {
            Log.Warn(LogTag, "Notifier pending intent send failed");
        }
    }

    private static bool CanUseFullScreenIntent(Context context) {
        if (!OperatingSystem.IsAndroidVersionAtLeast(34) ||
            context.GetSystemService(Context.NotificationService) is not NotificationManager manager) {
            return true;
        }

        try {
            var allowed = manager.CanUseFullScreenIntent();
            Log.Info(LogTag, $"Notifier full-screen permission result={allowed}");
            return allowed;
        } catch {
            Log.Warn(LogTag, "Notifier failed while querying full-screen intent permission");
            return false;
        }
    }

    private static PendingIntentFlags NormalizePendingIntentFlags(PendingIntentFlags flags) {
        if (OperatingSystem.IsAndroidVersionAtLeast(23)) {
            flags |= PendingIntentFlags.Immutable;
        }

        return flags;
    }

    private static string ResolveTitle() {
        return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch {
            "ar" => "Prayer alarm",
            "fr" => "Alarme de priere",
            "es" => "Alarma de oracion",
            "tr" => "Namaz alarmi",
            _ => "Prayer alarm"
        };
    }

    private static string ResolveBody() {
        return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch {
            "ar" => "Open alarm now",
            "fr" => "Ouvrir l'alarme maintenant",
            "es" => "Abrir la alarma ahora",
            "tr" => "Alarmi simdi ac",
            _ => "Open alarm now"
        };
    }
}
#endif
