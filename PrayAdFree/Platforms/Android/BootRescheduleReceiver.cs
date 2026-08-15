#if ANDROID
using Android.App;
using Android.Content;
using Android.Util;

namespace Pray_Ad_Free.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = true, DirectBootAware = true)]
[IntentFilterAttribute(new[] {
    Intent.ActionBootCompleted,
    Intent.ActionLockedBootCompleted,
    Intent.ActionMyPackageReplaced,
    Intent.ActionDateChanged,
    Intent.ActionTimeChanged,
    Intent.ActionTimezoneChanged,
    "android.intent.action.QUICKBOOT_POWERON",
    "com.htc.intent.action.QUICKBOOT_POWERON"
})]
public sealed class BootRescheduleReceiver : BroadcastReceiver {
    private const string LogTag = "PrayAdFree.Boot";

    public override void OnReceive(Context? context, Intent? intent) {
        if (context == null) {
            return;
        }

        var action = intent?.Action ?? "(none)";
        var pending = GoAsync();
        if (pending == null) {
            _ = Task.Run(async () => {
                try {
                    Log.Info(LogTag, $"Received boot broadcast: {action}");
                    await BootNotificationRescheduler.RescheduleAsync(context).ConfigureAwait(false);
                    await WidgetUpdateCoordinator.UpdateAllAsync(context, "BootReceiver").ConfigureAwait(false);
                } catch (Exception ex) {
                    Log.Error(LogTag, $"Boot receiver failed: {ex}");
                }
            });
            return;
        }

        _ = Task.Run(async () => {
            try {
                Log.Info(LogTag, $"Received boot broadcast: {action}");
                await BootNotificationRescheduler.RescheduleAsync(context).ConfigureAwait(false);
                await WidgetUpdateCoordinator.UpdateAllAsync(context, "BootReceiver").ConfigureAwait(false);
            } catch (Exception ex) {
                Log.Error(LogTag, $"Boot receiver failed: {ex}");
            } finally {
                pending.Finish();
            }
        });
    }
}
#endif
