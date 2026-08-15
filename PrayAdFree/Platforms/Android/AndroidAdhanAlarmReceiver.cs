#if ANDROID
using Android.Content;
using Android.Util;
using PrayAdFree.Core.Models;
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

        Log.Info(LogTag, $"Receiver handling receivedAtUtc={DateTime.UtcNow:O} payloadLength={payload.Length}");
        // Make the alarm payload visible to app bootstrap before Android opens
        // the activity. Otherwise React can render an inactive /alarm snapshot
        // while the background dispatch is still starting the playback service.
        AndroidAlarmLaunchCoordinator.Enqueue(payload);
        var pendingResult = GoAsync();
        _ = Task.Run(async () => {
            try {
                await HandleAsync(context, payload).ConfigureAwait(false);
            } finally {
                pendingResult?.Finish();
            }
        });
    }

    private static async Task HandleAsync(Context context, string payloadText) {
        LocalizationBootstrapper.EnsureInitialized();

        var services = ResolveServices(context);
        var decision = await ResolveDecisionAsync(services).ConfigureAwait(false);

        Log.Info(LogTag, $"Receiver fallback decision {AndroidAlarmCapabilityService.BuildLogDetails(decision)}");
        if (services?.GetService(typeof(IAppLogger)) is IAppLogger logger) {
            logger.LogEvent("AlarmFallbackDecision", $"source=receiver;{AndroidAlarmCapabilityService.BuildLogDetails(decision)}");
        }

        switch (decision.PresentationMode) {
            case AlarmPresentationMode.Overlay:
                Log.Info(LogTag, "Receiver selected overlay branch");
                AlarmOverlayService.Start(context, payloadText);
                return;
            case AlarmPresentationMode.FullscreenActivity:
                Log.Info(LogTag, "Receiver selected fullscreen notification/activity branch");
                AndroidAlarmFullscreenNotifier.LaunchActivity(context, payloadText);
                AndroidAlarmFullscreenNotifier.Show(context, payloadText);
                return;
            case AlarmPresentationMode.ControlNotification:
                Log.Info(LogTag, "Receiver selected control notification branch");
                await StartControlNotificationFallbackAsync(services, payloadText).ConfigureAwait(false);
                return;
            default:
                Log.Warn(LogTag, "Receiver selected unsupported branch");
                return;
        }
    }

    private static async Task<AndroidAlarmCapabilityDecision> ResolveDecisionAsync(IServiceProvider? services) {
        if (services?.GetService(typeof(AndroidAlarmCapabilityService)) is AndroidAlarmCapabilityService capabilityService) {
            return await capabilityService.GetCurrentDecisionAsync().ConfigureAwait(false);
        }

        Log.Warn(LogTag, "Receiver could not resolve capability service; using fullscreen fallback");
        return new AndroidAlarmCapabilityDecision(
            Permissions: new AlarmPermissionState(true, true, true, false),
            ScreenOnAndUnlocked: false,
            SchedulingMode: AlarmSchedulingMode.ExactAlarm,
            PresentationMode: AlarmPresentationMode.FullscreenActivity,
            SupportStatus: AlarmSupportStatus.FullSupport);
    }

    private static async Task StartControlNotificationFallbackAsync(IServiceProvider? services, string payloadText) {
        if (!AdhanAlarmPayload.TryParse(payloadText, out var payload)) {
            Log.Warn(LogTag, "Receiver control notification branch could not parse alarm payload");
            return;
        }

        if (services?.GetService(typeof(AdhanPlaybackService)) is not AdhanPlaybackService playbackService) {
            Log.Warn(LogTag, "Receiver control notification branch could not resolve playback service");
            return;
        }

        await playbackService.HandleAndroidAlarmLaunchAsync(
            payload,
            source: "AndroidReceiver.ControlNotification",
            presentationMode: AlarmPresentationMode.ControlNotification).ConfigureAwait(false);
    }

    private static IServiceProvider? ResolveServices(Context context) {
        if (App.Services != null) {
            return App.Services;
        }

        return Microsoft.Maui.IPlatformApplication.Current?.Services;
    }
}
#endif
