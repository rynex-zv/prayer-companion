#if ANDROID
using Android.Util;
using PrayAdFree.Core.Models;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.Platforms.Android;

internal static class AndroidAlarmLaunchCoordinator {
    private const string LogTag = "PrayAdFree.Alarm";
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
        Log.Info(LogTag, $"Coordinator.Enqueue atUtc={DateTime.UtcNow:O} payloadLength={payloadText.Length}");
    }

    public static bool TryGetPendingPayload(out AdhanAlarmPayload payload) {
        string? payloadText;
        lock (SyncRoot) {
            payloadText = _pendingPayloadText;
        }

        return AdhanAlarmPayload.TryParse(payloadText, out payload);
    }

    public static void TryDispatchPending(string reason) {
        string? payloadText;
        lock (SyncRoot) {
            if (_dispatchInProgress || string.IsNullOrWhiteSpace(_pendingPayloadText)) {
                Log.Info(LogTag, $"Coordinator.Dispatch skipped reason={reason} inProgress={_dispatchInProgress} hasPending={!string.IsNullOrWhiteSpace(_pendingPayloadText)}");
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
            Log.Warn(LogTag, $"Coordinator.Dispatch services_unavailable reason={reason}");
            CompleteDispatch(null, keepPending: true);
            return;
        }

        Log.Info(LogTag, $"Coordinator.Dispatch start reason={reason} atUtc={DateTime.UtcNow:O}");
        _ = Task.Run(async () => {
            try {
                await playbackService.HandleAndroidAlarmLaunchAsync(payload, reason).ConfigureAwait(false);
                Log.Info(LogTag, $"Coordinator.Dispatch complete reason={reason} atUtc={DateTime.UtcNow:O}");
                CompleteDispatch(payloadText);
            } catch (Exception exception) {
                Log.Error(LogTag, $"Coordinator.Dispatch failed reason={reason} error={exception}");
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
