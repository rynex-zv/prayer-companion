using Android.Content;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class AdhanControlActionReceiver : BroadcastReceiver {
    public override void OnReceive(Context? context, Intent? intent) {
        if (intent == null || !string.Equals(intent.Action, AdhanPlaybackService.AndroidControlAction, StringComparison.Ordinal)) {
            return;
        }

        var actionId = intent.GetIntExtra(AdhanPlaybackService.AndroidControlActionIdExtra, int.MinValue);
        if (actionId == int.MinValue) {
            return;
        }

        _ = Task.Run(async () => {
            try {
                if (App.Services?.GetService(typeof(IAdhanPlaybackService)) is not IAdhanPlaybackService playbackService) {
                    return;
                }

                if (playbackService is AdhanPlaybackService concrete) {
                    await concrete.HandleControlActionAsync(actionId).ConfigureAwait(false);
                    return;
                }

                if (actionId == AdhanPlaybackService.StopActionId ||
                    actionId == AdhanPlaybackService.AndroidDismissControlActionId) {
                    await playbackService.StopAsync().ConfigureAwait(false);
                }
            } catch {
            }
        });
    }
}
