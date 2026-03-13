#if ANDROID
using Android.Content;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = false, DirectBootAware = true)]
public sealed class AndroidAdhanAlarmReceiver : BroadcastReceiver {
    public override void OnReceive(Context? context, Intent? intent) {
        if (context == null ||
            intent == null ||
            !string.Equals(intent.Action, AdhanPlaybackService.AndroidAlarmAction, StringComparison.Ordinal)) {
            return;
        }

        var payload = intent.GetStringExtra(AndroidAdhanAlarmScheduler.AlarmPayloadExtra);
        if (string.IsNullOrWhiteSpace(payload)) {
            return;
        }

        var launchIntent = new Intent(context, typeof(MainActivity));
        launchIntent.SetAction(AdhanPlaybackService.AndroidAlarmAction);
        launchIntent.PutExtra(AndroidAdhanAlarmScheduler.AlarmPayloadExtra, payload);
        launchIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop | ActivityFlags.ReorderToFront);
        context.StartActivity(launchIntent);
    }
}
#endif
