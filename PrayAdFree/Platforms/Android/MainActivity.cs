using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Plugin.LocalNotification;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Platforms.Android;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity {
    private static readonly object AlarmIntentLock = new();
    private static string? _lastAlarmPayload;
    private static DateTime _lastAlarmPayloadUtc;

    protected override void OnCreate(Bundle? savedInstanceState) {
        base.OnCreate(savedInstanceState);
        NotifyNotificationIntent(Intent);
        HandleAlarmPresentationIntent(Intent);
        HandleAdhanControlIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent) {
        base.OnNewIntent(intent);
        if (intent != null) {
            Intent = intent;
        }
        NotifyNotificationIntent(intent);
        HandleAlarmPresentationIntent(intent);
        HandleAdhanControlIntent(intent);
    }

    private static void NotifyNotificationIntent(Intent? intent) {
        if (intent == null || IsAlarmIntent(intent)) {
            return;
        }

        try {
            LocalNotificationCenter.NotifyNotificationTapped(intent);
        } catch {
        }
    }

    private void HandleAlarmPresentationIntent(Intent? intent) {
        if (!TryGetAlarmPayload(intent, out var payloadText, out _)) {
            return;
        }

        AndroidAlarmFullscreenNotifier.Cancel(this);
        TryEnableAlarmFullscreenPresentation();
        DispatchAlarmIntent(payloadText);
    }

    private static bool IsAlarmIntent(Intent intent) {
        if (string.Equals(intent.Action, AdhanPlaybackService.AndroidAlarmAction, StringComparison.Ordinal)) {
            return true;
        }

        var directPayload = intent.GetStringExtra(AndroidAdhanAlarmScheduler.AlarmPayloadExtra);
        if (!string.IsNullOrWhiteSpace(directPayload) && AdhanAlarmPayload.TryParse(directPayload, out _)) {
            return true;
        }

        var extras = intent.Extras;
        if (extras == null) {
            return false;
        }

        var keys = extras.KeySet();
        if (keys == null) {
            return false;
        }

        foreach (var key in keys) {
            if (string.IsNullOrWhiteSpace(key)) {
                continue;
            }

            var value = extras.Get(key);
            var text = value?.ToString();
            if (!string.IsNullOrWhiteSpace(text) && AdhanAlarmPayload.TryParse(text, out _)) {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetAlarmPayload(Intent? intent, out string payloadText, out AdhanAlarmPayload payload) {
        payloadText = string.Empty;
        payload = default;
        if (intent == null || !IsAlarmIntent(intent)) {
            return false;
        }

        var directPayload = intent.GetStringExtra(AndroidAdhanAlarmScheduler.AlarmPayloadExtra);
        if (!string.IsNullOrWhiteSpace(directPayload) && AdhanAlarmPayload.TryParse(directPayload, out payload)) {
            payloadText = directPayload;
            return true;
        }

        var extras = intent.Extras;
        if (extras == null) {
            return false;
        }

        var keys = extras.KeySet();
        if (keys == null) {
            return false;
        }

        foreach (var key in keys) {
            if (string.IsNullOrWhiteSpace(key)) {
                continue;
            }

            var text = extras.Get(key)?.ToString();
            if (!string.IsNullOrWhiteSpace(text) && AdhanAlarmPayload.TryParse(text, out payload)) {
                payloadText = text;
                return true;
            }
        }

        return false;
    }

    private static void DispatchAlarmIntent(string payloadText) {
        if (string.IsNullOrWhiteSpace(payloadText) || !ShouldDispatchAlarmPayload(payloadText)) {
            return;
        }

        AndroidAlarmLaunchCoordinator.Enqueue(payloadText);
        AndroidAlarmLaunchCoordinator.TryDispatchPending("MainActivity");
    }

    private static bool ShouldDispatchAlarmPayload(string payloadText) {
        lock (AlarmIntentLock) {
            var now = DateTime.UtcNow;
            if (string.Equals(_lastAlarmPayload, payloadText, StringComparison.Ordinal) &&
                now - _lastAlarmPayloadUtc < TimeSpan.FromSeconds(5)) {
                return false;
            }

            _lastAlarmPayload = payloadText;
            _lastAlarmPayloadUtc = now;
            return true;
        }
    }

    private void TryEnableAlarmFullscreenPresentation() {
        try {
            TryWakeScreenForAlarm();

            if (OperatingSystem.IsAndroidVersionAtLeast(27)) {
                SetShowWhenLocked(true);
                SetTurnScreenOn(true);
            } else {
#pragma warning disable CS0618
                Window?.AddFlags(
                    WindowManagerFlags.ShowWhenLocked |
                    WindowManagerFlags.TurnScreenOn);
#pragma warning restore CS0618
            }

            Window?.AddFlags(
                WindowManagerFlags.KeepScreenOn |
                WindowManagerFlags.DismissKeyguard);

            if (OperatingSystem.IsAndroidVersionAtLeast(30)) {
                Window?.SetDecorFitsSystemWindows(false);
                var insetsController = Window?.InsetsController;
                if (insetsController != null) {
                    insetsController.Hide(WindowInsets.Type.StatusBars() | WindowInsets.Type.NavigationBars());
                    insetsController.SystemBarsBehavior = (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
                }
                return;
            }

#pragma warning disable CS0618
            if (Window?.DecorView != null) {
                Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(
                    SystemUiFlags.Fullscreen |
                    SystemUiFlags.HideNavigation |
                    SystemUiFlags.ImmersiveSticky);
            }
#pragma warning restore CS0618
        } catch {
        }
    }

    private void TryWakeScreenForAlarm() {
        try {
            if (GetSystemService(PowerService) is not PowerManager powerManager) {
                return;
            }

#pragma warning disable CS0618
            using var wakeLock = powerManager.NewWakeLock(
                WakeLockFlags.ScreenBright | WakeLockFlags.AcquireCausesWakeup,
                "PrayAdFree:AlarmWake");
#pragma warning restore CS0618
            wakeLock?.Acquire(15_000);
        } catch {
        }
    }

    private static void HandleAdhanControlIntent(Intent? intent) {
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
