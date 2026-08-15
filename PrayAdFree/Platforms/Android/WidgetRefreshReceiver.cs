#if ANDROID
using Android.App;
using Android.Content;

namespace Pray_Ad_Free.Platforms.Android;

#if PRAY_WIDGETS
[BroadcastReceiver(Enabled = true, Exported = false)]
#else
[BroadcastReceiver(Enabled = false, Exported = false)]
#endif
public sealed class WidgetRefreshReceiver : BroadcastReceiver {
    public override void OnReceive(Context? context, Intent? intent) {
        if (context == null || !string.Equals(intent?.Action, WidgetUpdateCoordinator.RefreshAction, StringComparison.Ordinal)) {
            return;
        }

        WidgetUpdateCoordinator.RequestImmediateRefresh(context, "Alarm");
    }
}
#endif
