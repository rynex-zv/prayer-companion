using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity {
    protected override void OnCreate(Bundle? savedInstanceState) {
        base.OnCreate(savedInstanceState);
        HandleAlarmPresentationIntent(Intent);
        HandleAdhanControlIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent) {
        base.OnNewIntent(intent);
        if (intent != null) {
            Intent = intent;
        }
        HandleAlarmPresentationIntent(intent);
        HandleAdhanControlIntent(intent);
    }

    private void HandleAlarmPresentationIntent(Intent? intent) {
        if (intent == null || !string.Equals(intent.Action, AdhanPlaybackService.AndroidAlarmAction, StringComparison.Ordinal)) {
            return;
        }

        TryEnableAlarmFullscreenPresentation();
    }

    private void TryEnableAlarmFullscreenPresentation() {
        try {
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
