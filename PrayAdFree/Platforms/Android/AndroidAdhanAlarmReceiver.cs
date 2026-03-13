#if ANDROID
using Android.Content;
using Android.Util;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = false, DirectBootAware = true)]
public sealed class AndroidAdhanAlarmReceiver : BroadcastReceiver {
    private const string LogTag = "PrayAdFree.Alarm";

    public override void OnReceive(Context? context, Intent? intent) {
        if (context == null ||
            intent == null ||
            !string.Equals(intent.Action, AdhanPlaybackService.AndroidAlarmAction, StringComparison.Ordinal)) {
            Log.Debug(LogTag, $"Receiver ignored action={intent?.Action ?? "<null>"} contextNull={context == null}");
            return;
        }

        var payload = intent.GetStringExtra(AndroidAdhanAlarmScheduler.AlarmPayloadExtra);
        if (string.IsNullOrWhiteSpace(payload)) {
            Log.Warn(LogTag, "Receiver missing alarm payload");
            return;
        }

        Log.Info(LogTag, $"Receiver handling alarm payloadLength={payload.Length}");

        if (AlarmOverlayService.ShouldShowOverlay(context)) {
            Log.Info(LogTag, "Receiver selected overlay branch");
            AlarmOverlayService.Start(context, payload);
            return;
        }

        Log.Info(LogTag, "Receiver selected fullscreen notification/activity branch");
        AndroidAlarmFullscreenNotifier.LaunchActivity(context, payload);
        AndroidAlarmFullscreenNotifier.Show(context, payload);
    }
}
#endif
