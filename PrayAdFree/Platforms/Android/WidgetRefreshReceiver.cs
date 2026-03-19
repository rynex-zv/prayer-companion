#if ANDROID
using Android.App;
using Android.Content;

namespace Pray_Ad_Free.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class WidgetRefreshReceiver : BroadcastReceiver {
    public override void OnReceive(Context? context, Intent? intent) {
        if (context == null || !string.Equals(intent?.Action, WidgetUpdateCoordinator.RefreshAction, StringComparison.Ordinal)) {
            return;
        }

        WidgetUpdateCoordinator.RequestImmediateRefresh(context, "Alarm");
    }
}
#endif
