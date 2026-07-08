using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Util;
using Android.Views;
using AndroidX.Activity;
using MauiWebber;
using Microsoft.Maui.Controls;
using Plugin.LocalNotification;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Platforms.Android;
using Pray_Ad_Free.Services;
using AndroidColor = Android.Graphics.Color;

namespace Pray_Ad_Free;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity {
    private const string BackLogTag = "PrayAdFree.Back";
    private const string WindowLogTag = "PrayAdFree.Window";
    private static readonly object AlarmIntentLock = new();
    private static string? _lastAlarmPayload;
    private static DateTime _lastAlarmPayloadUtc;
    private bool _handlingBack;

    protected override void OnCreate(Bundle? savedInstanceState) {
        base.OnCreate(savedInstanceState);
        ScheduleOpaqueWindowCheck("OnCreate");
        OnBackPressedDispatcher.AddCallback(this, new ShellBackPressedCallback(this));
        NotifyNotificationIntent(Intent);
        HandleAlarmPresentationIntent(Intent);
        HandleAdhanControlIntent(Intent);
    }

    protected override void OnResume() {
        base.OnResume();
        ScheduleOpaqueWindowCheck("OnResume");
    }

    public override void OnWindowFocusChanged(bool hasFocus) {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus) {
            ScheduleOpaqueWindowCheck("OnWindowFocusChanged");
        }
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

    private void ScheduleOpaqueWindowCheck(string reason) {
        EnsureOpaqueWindow(reason);

        _ = Task.Run(async () => {
            await Task.Delay(250).ConfigureAwait(false);
            RunOnUiThread(() => EnsureOpaqueWindow($"{reason}:250ms"));
            await Task.Delay(750).ConfigureAwait(false);
            RunOnUiThread(() => EnsureOpaqueWindow($"{reason}:1000ms"));
        });
    }

    private void EnsureOpaqueWindow(string reason) {
        try {
            var window = Window;
            if (window == null) {
                return;
            }

            window.ClearFlags(WindowManagerFlags.TranslucentStatus | WindowManagerFlags.TranslucentNavigation);
            window.SetBackgroundDrawable(new ColorDrawable(AndroidColor.Rgb(234, 247, 248)));

            var attributes = window.Attributes;
            if (attributes != null && Math.Abs(attributes.Alpha - 1f) > 0.001f) {
                attributes.Alpha = 1f;
                window.Attributes = attributes;
            }

            if (window.DecorView != null) {
                window.DecorView.Alpha = 1f;
                window.DecorView.SetBackgroundColor(AndroidColor.Rgb(234, 247, 248));
            }

            Log.Debug(WindowLogTag, $"EnsureOpaqueWindow reason={reason};alpha={window.Attributes?.Alpha}");
        } catch (Exception ex) {
            Log.Warn(WindowLogTag, $"EnsureOpaqueWindow failed reason={reason}: {ex}");
        }
    }

    private async Task HandleBackPressedAsync() {
        if (_handlingBack) {
            Log.Debug(BackLogTag, "Ignoring re-entrant back press.");
            return;
        }

        _handlingBack = true;
        try {
            var handled = await TryHandleShellBackAsync().ConfigureAwait(true);
            Log.Debug(BackLogTag, $"TryHandleShellBackAsync handled={handled}");
            if (!handled) {
                MoveTaskToBack(true);
            }
        } catch (Exception ex) {
            Log.Warn(BackLogTag, $"Back handling failed: {ex}");
            MoveTaskToBack(true);
        } finally {
            _handlingBack = false;
        }
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
#pragma warning disable CA1422
                Window?.SetDecorFitsSystemWindows(false);
#pragma warning restore CA1422
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

    private static async Task<bool> TryHandleShellBackAsync() {
        var shell = Shell.Current;
        if (shell == null) {
            Log.Debug(BackLogTag, "Shell.Current is null.");
            return false;
        }

        var currentSection = shell.CurrentItem?.CurrentItem;
        var sectionContentRoute = currentSection?.CurrentItem?.Route ?? string.Empty;
        var sectionStackCount = currentSection?.Navigation?.NavigationStack.Count ?? 0;
        var modalStackCount = currentSection?.Navigation?.ModalStack.Count ?? shell.Navigation.ModalStack.Count;
        var currentLocation = shell.CurrentState?.Location?.OriginalString ?? string.Empty;

        Log.Debug(
            BackLogTag,
            $"route={sectionContentRoute};location={currentLocation};sectionStack={sectionStackCount};modals={modalStackCount}");

        if (TryGetCurrentMauiWebberPage(shell, currentSection) is { } webPage &&
            await webPage.TryHandleBackNavigationAsync().ConfigureAwait(true)) {
            Log.Debug(BackLogTag, "Handled by MauiWebber navigation.");
            return true;
        }

        if (modalStackCount > 0) {
            Log.Debug(BackLogTag, "Popping modal page.");
            await shell.Navigation.PopModalAsync(true);
            return true;
        }

        if (sectionStackCount > 1) {
            Log.Debug(BackLogTag, "Popping section navigation page.");
            await currentSection!.Navigation.PopAsync(true);
            return true;
        }

        var activeTabRoute = sectionContentRoute;
        if (!string.Equals(activeTabRoute, "today", StringComparison.OrdinalIgnoreCase)) {
            Log.Debug(BackLogTag, $"Switching tab to today from {activeTabRoute}.");
            await shell.GoToAsync("//today");
            return true;
        }

        Log.Debug(BackLogTag, "No shell back action available.");
        return false;
    }

    private static MauiWebberPage? TryGetCurrentMauiWebberPage(Shell shell, ShellSection? currentSection) {
        var sectionStack = currentSection?.Navigation?.NavigationStack;
        if (sectionStack?.Count > 0 && sectionStack[^1] is MauiWebberPage sectionWebPage) {
            return sectionWebPage;
        }

        var shellStack = shell.Navigation?.NavigationStack;
        if (shellStack?.Count > 0 && shellStack[^1] is MauiWebberPage shellWebPage) {
            return shellWebPage;
        }

        return shell.CurrentPage as MauiWebberPage;
    }

    private sealed class ShellBackPressedCallback : OnBackPressedCallback {
        private readonly MainActivity _activity;

        public ShellBackPressedCallback(MainActivity activity) : base(true) {
            _activity = activity;
        }

        public override void HandleOnBackPressed() {
            _ = _activity.HandleBackPressedAsync();
        }
    }
}
