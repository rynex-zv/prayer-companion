#if ANDROID
using PrayAdFree.Core.Models;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.Platforms.Android;

internal static class AndroidAlarmLaunchCoordinator {
    private static readonly object SyncRoot = new();
    private static string? _pendingPayloadText;
    private static bool _dispatchInProgress;

    public static void Enqueue(string? payloadText) {
        if (string.IsNullOrWhiteSpace(payloadText) ||
            !AdhanAlarmPayload.TryParse(payloadText, out _)) {
            return;
        }

        lock (SyncRoot) {
            _pendingPayloadText = payloadText;
        }
    }

    public static void TryDispatchPending(string reason) {
        string? payloadText;
        lock (SyncRoot) {
            if (_dispatchInProgress || string.IsNullOrWhiteSpace(_pendingPayloadText)) {
                return;
            }

            payloadText = _pendingPayloadText;
            _dispatchInProgress = true;
        }

        if (!AdhanAlarmPayload.TryParse(payloadText, out var payload)) {
            CompleteDispatch(payloadText);
            return;
        }

        if (App.Services?.GetService(typeof(AdhanPlaybackService)) is not AdhanPlaybackService playbackService) {
            CompleteDispatch(null, keepPending: true);
            return;
        }

        _ = Task.Run(async () => {
            try {
                await playbackService.HandleAndroidAlarmLaunchAsync(payload, reason).ConfigureAwait(false);
                CompleteDispatch(payloadText);
            } catch {
                CompleteDispatch(null, keepPending: true);
            }
        });
    }

    private static void CompleteDispatch(string? payloadText, bool keepPending = false) {
        lock (SyncRoot) {
            _dispatchInProgress = false;
            if (!keepPending &&
                !string.IsNullOrWhiteSpace(payloadText) &&
                string.Equals(_pendingPayloadText, payloadText, StringComparison.Ordinal)) {
                _pendingPayloadText = null;
            }
        }
    }
}
#endif
