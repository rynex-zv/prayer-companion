using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using Microsoft.Extensions.DependencyInjection;
using PrayAdFree.Core.Models;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.Platforms.Android;

[Activity(
    Theme = "@style/PrayAdFree.AlarmFullscreen",
    LaunchMode = LaunchMode.SingleTop,
    Exported = false,
    ExcludeFromRecents = true,
    NoHistory = true,
    ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public sealed class AlarmActivity : AppCompatActivity {
    private TextView? _clockText;
    private TextView? _offsetText;
    private TextView? _prayerNameText;
    private TextView? _reminderText;
    private NumberPicker? _snoozePicker;
    private global::Android.Widget.Button? _decreaseButton;
    private global::Android.Widget.Button? _increaseButton;
    private global::Android.Widget.Button? _snoozeButton;
    private global::Android.Widget.Button? _stopButton;
    private string? _payloadText;
    private AdhanAlarmPayload _payload;
    private bool _isBusy;

    protected override void OnCreate(Bundle? savedInstanceState) {
        base.OnCreate(savedInstanceState);
        ConfigureForLockScreen();
        SetContentView(global::Pray_Ad_Free.Resource.Layout.alarm_activity);
        BindViews();
        HandleAlarmIntent(Intent, "OnCreate");
    }

    protected override void OnNewIntent(Intent? intent) {
        base.OnNewIntent(intent);
        if (intent != null) {
            Intent = intent;
        }

        HandleAlarmIntent(intent, "OnNewIntent");
    }

    protected override void OnResume() {
        base.OnResume();
        AndroidAlarmFullscreenNotifier.Cancel(this);
        if (!string.IsNullOrWhiteSpace(_payloadText)) {
            AndroidAlarmLaunchCoordinator.Enqueue(_payloadText);
            AndroidAlarmLaunchCoordinator.TryDispatchPending("AlarmActivity.OnResume");
        }
    }

    private void ConfigureForLockScreen() {
        try {
            if (OperatingSystem.IsAndroidVersionAtLeast(27)) {
                SetShowWhenLocked(true);
                SetTurnScreenOn(true);
            } else {
#pragma warning disable CS0618
                Window?.AddFlags(WindowManagerFlags.ShowWhenLocked | WindowManagerFlags.TurnScreenOn);
#pragma warning restore CS0618
            }

            Window?.AddFlags(WindowManagerFlags.KeepScreenOn | WindowManagerFlags.DismissKeyguard);

            if (OperatingSystem.IsAndroidVersionAtLeast(30)) {
                Window?.SetDecorFitsSystemWindows(false);
                if (Window?.InsetsController != null) {
                    Window.InsetsController.Hide(WindowInsets.Type.StatusBars() | WindowInsets.Type.NavigationBars());
                    Window.InsetsController.SystemBarsBehavior = (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
                }
            } else if (Window?.DecorView != null) {
#pragma warning disable CS0618
                Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(
                    SystemUiFlags.Fullscreen |
                    SystemUiFlags.HideNavigation |
                    SystemUiFlags.ImmersiveSticky);
#pragma warning restore CS0618
            }
        } catch {
        }
    }

    private void BindViews() {
        _clockText = FindViewById<TextView>(global::Pray_Ad_Free.Resource.Id.alarmClockText);
        _offsetText = FindViewById<TextView>(global::Pray_Ad_Free.Resource.Id.alarmOffsetText);
        _prayerNameText = FindViewById<TextView>(global::Pray_Ad_Free.Resource.Id.alarmPrayerNameText);
        _reminderText = FindViewById<TextView>(global::Pray_Ad_Free.Resource.Id.alarmReminderText);
        _snoozePicker = FindViewById<NumberPicker>(global::Pray_Ad_Free.Resource.Id.alarmSnoozePicker);
        _decreaseButton = FindViewById<global::Android.Widget.Button>(global::Pray_Ad_Free.Resource.Id.alarmDecreaseButton);
        _increaseButton = FindViewById<global::Android.Widget.Button>(global::Pray_Ad_Free.Resource.Id.alarmIncreaseButton);
        _snoozeButton = FindViewById<global::Android.Widget.Button>(global::Pray_Ad_Free.Resource.Id.alarmSnoozeButton);
        _stopButton = FindViewById<global::Android.Widget.Button>(global::Pray_Ad_Free.Resource.Id.alarmStopButton);

        if (_snoozePicker != null) {
            _snoozePicker.WrapSelectorWheel = false;
            _snoozePicker.MinValue = 4;
            _snoozePicker.MaxValue = 30;
            _snoozePicker.Value = 10;
        }

        if (_decreaseButton != null) {
            _decreaseButton.Click += (_, _) => AdjustPicker(-1);
        }

        if (_increaseButton != null) {
            _increaseButton.Click += (_, _) => AdjustPicker(1);
        }

        if (_snoozeButton != null) {
            _snoozeButton.Text = ResolveSnoozeButtonTitle();
            _snoozeButton.Click += async (_, _) => await SnoozeAsync();
        }

        if (_stopButton != null) {
            _stopButton.Text = ResolveStopButtonTitle();
            _stopButton.Click += async (_, _) => await StopAsync();
        }
    }

    private void HandleAlarmIntent(Intent? intent, string reason) {
        if (!TryGetAlarmPayload(intent, out var payloadText, out var payload)) {
            FinishAlarmUi();
            return;
        }

        _payloadText = payloadText;
        _payload = payload;
        AndroidAlarmFullscreenNotifier.Cancel(this);
        ApplyFallbackPresentation(payload);
        _ = InitializeAlarmAsync(reason);
    }

    private async Task InitializeAlarmAsync(string reason) {
        if (string.IsNullOrWhiteSpace(_payloadText)) {
            return;
        }

        AndroidAlarmLaunchCoordinator.Enqueue(_payloadText);
        AndroidAlarmLaunchCoordinator.TryDispatchPending($"AlarmActivity.{reason}");

        var playbackService = await WaitForPlaybackServiceAsync(TimeSpan.FromSeconds(6)).ConfigureAwait(false);
        if (playbackService == null) {
            return;
        }

        var model = await playbackService.BuildAlarmPresentationModelAsync(_payload).ConfigureAwait(false);
        RunOnUiThread(() => ApplyPresentation(model));
    }

    private void ApplyFallbackPresentation(AdhanAlarmPayload payload) {
        if (_clockText != null) {
            _clockText.Text = payload.BasePrayerTime.ToLocalTime().ToString("HH:mm");
        }

        if (_offsetText != null) {
            _offsetText.Text = ResolveDelayOffsetText(payload);
        }

        if (_prayerNameText != null) {
            _prayerNameText.Text = ResolvePrayerName(payload.Prayer);
        }

        if (_reminderText != null) {
            _reminderText.Text = ResolveFallbackReminderText();
        }
    }

    private void ApplyPresentation(AlarmPresentationModel model) {
        if (_clockText != null) {
            _clockText.Text = model.PrayerClock;
        }

        if (_offsetText != null) {
            _offsetText.Text = model.DelayFromBase;
        }

        if (_prayerNameText != null) {
            _prayerNameText.Text = model.PrayerName;
        }

        if (_reminderText != null) {
            _reminderText.Text = string.IsNullOrWhiteSpace(model.ReminderText)
                ? ResolveFallbackReminderText()
                : model.ReminderText;
        }

        if (_snoozePicker != null) {
            _snoozePicker.MinValue = model.MinDelayMinutes;
            _snoozePicker.MaxValue = model.MaxDelayMinutes;
            _snoozePicker.Value = Math.Clamp(model.InitialDelayMinutes, model.MinDelayMinutes, model.MaxDelayMinutes);
        }
    }

    private async Task StopAsync() {
        if (_isBusy) {
            return;
        }

        await ExecuteBusyAsync(async playbackService => {
            if (playbackService != null) {
                await playbackService.StopAsync().ConfigureAwait(false);
            }

            FinishAlarmUi();
        }).ConfigureAwait(false);
    }

    private async Task SnoozeAsync() {
        if (_isBusy) {
            return;
        }

        var delayMinutes = _snoozePicker?.Value ?? 10;
        await ExecuteBusyAsync(async playbackService => {
            if (playbackService == null) {
                return;
            }

            var scheduled = await playbackService.SnoozeAlarmAsync(_payload, delayMinutes).ConfigureAwait(false);
            if (scheduled) {
                FinishAlarmUi();
            }
        }).ConfigureAwait(false);
    }

    private async Task ExecuteBusyAsync(Func<AdhanPlaybackService?, Task> action) {
        _isBusy = true;
        SetButtonsEnabled(false);
        try {
            var playbackService = await WaitForPlaybackServiceAsync(TimeSpan.FromSeconds(4)).ConfigureAwait(false);
            await action(playbackService).ConfigureAwait(false);
        } finally {
            _isBusy = false;
            RunOnUiThread(() => SetButtonsEnabled(true));
        }
    }

    private void SetButtonsEnabled(bool enabled) {
        if (_decreaseButton != null) {
            _decreaseButton.Enabled = enabled;
        }

        if (_increaseButton != null) {
            _increaseButton.Enabled = enabled;
        }

        if (_snoozeButton != null) {
            _snoozeButton.Enabled = enabled;
        }

        if (_stopButton != null) {
            _stopButton.Enabled = enabled;
        }
    }

    private void AdjustPicker(int delta) {
        if (_snoozePicker == null) {
            return;
        }

        var next = Math.Clamp(_snoozePicker.Value + delta, _snoozePicker.MinValue, _snoozePicker.MaxValue);
        _snoozePicker.Value = next;
    }

    private void FinishAlarmUi() {
        RunOnUiThread(() => {
            AndroidAlarmFullscreenNotifier.Cancel(this);
            try {
                FinishAndRemoveTask();
            } catch {
                Finish();
            }
        });
    }

    private static bool TryGetAlarmPayload(Intent? intent, out string payloadText, out AdhanAlarmPayload payload) {
        payloadText = string.Empty;
        payload = default;
        if (intent == null) {
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
            var text = extras.Get(key)?.ToString();
            if (!string.IsNullOrWhiteSpace(text) && AdhanAlarmPayload.TryParse(text, out payload)) {
                payloadText = text;
                return true;
            }
        }

        return false;
    }

    private static async Task<AdhanPlaybackService?> WaitForPlaybackServiceAsync(TimeSpan timeout) {
        var deadlineUtc = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadlineUtc) {
            var services = ResolveServices();
            if (services?.GetService(typeof(AdhanPlaybackService)) is AdhanPlaybackService playbackService) {
                return playbackService;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        return null;
    }

    private static IServiceProvider? ResolveServices() {
        if (App.Services != null) {
            return App.Services;
        }

        if (global::Android.App.Application.Context is MainApplication mainApplication) {
            return mainApplication.Services;
        }

        return null;
    }

    private static string ResolveDelayOffsetText(AdhanAlarmPayload payload) {
        var offset = payload.NotifyTime - payload.BasePrayerTime;
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var absolute = offset < TimeSpan.Zero ? offset.Negate() : offset;
        var totalHours = (int)Math.Floor(absolute.TotalHours);
        return $"{sign}{totalHours}:{absolute.Minutes:00}";
    }

    private static string ResolvePrayerName(PrayerId prayer) {
        return prayer switch {
            PrayerId.Fajr => "Fajr",
            PrayerId.Dhuhr => "Dhuhr",
            PrayerId.Asr => "Asr",
            PrayerId.Maghrib => "Maghrib",
            PrayerId.Isha => "Isha",
            _ => "Prayer"
        };
    }

    private static string ResolveFallbackReminderText() {
        return "Prayer alarm is ringing now";
    }

    private static string ResolveSnoozeButtonTitle() {
        return "Snooze";
    }

    private static string ResolveStopButtonTitle() {
        return "Stop";
    }
}
