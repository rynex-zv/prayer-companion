#if ANDROID
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Provider;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.Interop;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using PrayAdFree.Core.Models;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.Platforms.Android;

[Service(Exported = false, ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeSystemExempted)]
public sealed class AlarmOverlayService : Service {
    private const string StartAction = "com.rynex.prayadfree.ALARM_OVERLAY_START";
    private const string StopAction = "com.rynex.prayadfree.ALARM_OVERLAY_STOP";
    private const string OverlayChannelId = "adhan_alarm_overlay";
    private const int OverlayNotificationId = 54010;
    private const string LogTag = "PrayAdFree.Alarm";
    private const int DefaultSnoozeDelayMinutes = 10;
    private const int DefaultMaxSnoozeDelayMinutes = 30;

    private IWindowManager? _windowManager;
    private global::Android.Views.View? _overlayView;
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
    private bool _canSnooze = true;

    public override IBinder? OnBind(Intent? intent) {
        return null;
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId) {
        LocalizationBootstrapper.EnsureInitialized();
        Log.Info(LogTag, $"OverlayService.OnStartCommand action={intent?.Action ?? "<null>"}");
        if (string.Equals(intent?.Action, StopAction, StringComparison.Ordinal)) {
            Log.Info(LogTag, "OverlayService received stop action");
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        StartForegroundCore();
        HandleAlarmIntent(intent);
        return StartCommandResult.Sticky;
    }

    public override void OnDestroy() {
        base.OnDestroy();
        Log.Info(LogTag, "OverlayService.OnDestroy");
        RemoveOverlay();
        try {
            StopForeground(StopForegroundFlags.Remove);
        } catch {
        }
    }

    public static bool ShouldShowOverlay(Context context) {
        var visibleUnlocked = AndroidAlarmFullscreenNotifier.ShouldOpenAppDirectly(context);
        var canDraw = Settings.CanDrawOverlays(context);
        var result = visibleUnlocked && canDraw;
        Log.Info(LogTag, $"OverlayService.ShouldShowOverlay visibleUnlocked={visibleUnlocked} canDrawOverlays={canDraw} result={result}");
        return result;
    }

    public static void Start(Context context, string payloadText) {
        if (string.IsNullOrWhiteSpace(payloadText)) {
            Log.Warn(LogTag, "OverlayService.Start skipped because payload was empty");
            return;
        }

        var intent = new Intent(context, typeof(AlarmOverlayService));
        intent.SetAction(StartAction);
        intent.PutExtra(AndroidAdhanAlarmScheduler.AlarmPayloadExtra, payloadText);
        if (OperatingSystem.IsAndroidVersionAtLeast(26)) {
            Log.Info(LogTag, "OverlayService.Start launching as foreground service");
            context.StartForegroundService(intent);
            return;
        }

        Log.Info(LogTag, "OverlayService.Start launching as background service");
        context.StartService(intent);
    }

    public static void StopOverlay(Context? context) {
        if (context == null) {
            return;
        }

        try {
            var intent = new Intent(context, typeof(AlarmOverlayService));
            intent.SetAction(StopAction);
            if (OperatingSystem.IsAndroidVersionAtLeast(26)) {
                context.StartForegroundService(intent);
                return;
            }

            context.StartService(intent);
        } catch {
            Log.Warn(LogTag, "OverlayService.StopOverlay failed");
        }
    }

    private void StartForegroundCore() {
        EnsureChannel();
        var notification = BuildForegroundNotification();
        if (OperatingSystem.IsAndroidVersionAtLeast(29)) {
            StartForeground(OverlayNotificationId, notification, global::Android.Content.PM.ForegroundService.TypeSystemExempted);
            return;
        }

        StartForeground(OverlayNotificationId, notification);
    }

    private Notification BuildForegroundNotification() {
        Notification.Builder builder;
        if (OperatingSystem.IsAndroidVersionAtLeast(26)) {
            builder = new Notification.Builder(this, OverlayChannelId);
        } else {
            builder = new Notification.Builder(this);
        }

        builder
            .SetSmallIcon(ApplicationInfo?.Icon ?? global::Android.Resource.Drawable.IcLockIdleAlarm)
            .SetContentTitle(ResolveOverlayTitle())
            .SetContentText(ResolveOverlayBody())
            .SetCategory(Notification.CategoryAlarm)
            .SetOngoing(true)
            .SetAutoCancel(false)
            .SetVisibility(NotificationVisibility.Secret)
            .SetOnlyAlertOnce(true);

        return builder.Build();
    }

    private void EnsureChannel() {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26) ||
            GetSystemService(NotificationService) is not NotificationManager manager) {
            return;
        }

        if (manager.GetNotificationChannel(OverlayChannelId) != null) {
            return;
        }

        var channel = new NotificationChannel(
            OverlayChannelId,
            LocalizationManager.Translate("AlarmScreenTitle"),
            NotificationImportance.Low) {
            Description = LocalizationManager.Translate("AdhanPlaybackStopHint")
        };
        channel.SetSound(null, null);
        channel.EnableVibration(false);
        channel.LockscreenVisibility = NotificationVisibility.Secret;
        manager.CreateNotificationChannel(channel);
    }

    private void HandleAlarmIntent(Intent? intent) {
        if (!TryGetAlarmPayload(intent, out var payloadText, out var payload)) {
            Log.Warn(LogTag, "OverlayService could not parse alarm payload from intent");
            StopSelf();
            return;
        }

        Log.Info(LogTag, $"OverlayService parsed alarm payloadLength={payloadText.Length}");
        _payloadText = payloadText;
        _payload = payload;
        AndroidAlarmFullscreenNotifier.Cancel(this);
        ShowOverlay(payload);
        _ = InitializeOverlayAsync();
    }

    private void ShowOverlay(AdhanAlarmPayload payload) {
        MainThread.BeginInvokeOnMainThread(() => {
            RemoveOverlay();

            _windowManager = ResolveWindowManager();
            if (_windowManager == null) {
                Log.Warn(LogTag, "OverlayService has no WindowManager");
                StopSelf();
                return;
            }

            var inflater = LayoutInflater.From(this);
            var view = inflater?.Inflate(global::Pray_Ad_Free.Resource.Layout.alarm_activity, null);
            if (view == null) {
                Log.Warn(LogTag, "OverlayService failed to inflate overlay view");
                StopSelf();
                return;
            }

            _overlayView = view;
            BindViews(view);
            ApplyFallbackPresentation(payload);

            var type = OperatingSystem.IsAndroidVersionAtLeast(26)
                ? WindowManagerTypes.ApplicationOverlay
                : WindowManagerTypes.Phone;
            var flags = WindowManagerFlags.LayoutInScreen | WindowManagerFlags.KeepScreenOn | WindowManagerFlags.Fullscreen;
            var layoutParams = new WindowManagerLayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent,
                type,
                flags,
                Format.Translucent) {
                Gravity = GravityFlags.Top | GravityFlags.Start
            };

            try {
                _windowManager.AddView(view, layoutParams);
                Log.Info(LogTag, $"OverlayService added overlay view type={type}");
            } catch (Exception ex) {
                Log.Warn(LogTag, $"OverlayService failed to add overlay view: {ex.GetType().Name}");
                StopSelf();
            }
        });
    }

    private IWindowManager? ResolveWindowManager() {
        var rawWindowManager = GetSystemService(WindowService);
        if (rawWindowManager == null) {
            Log.Warn(LogTag, "OverlayService ResolveWindowManager raw service was null");
            return null;
        }

        try {
            var windowManager = rawWindowManager.JavaCast<IWindowManager>();
            Log.Info(LogTag, $"OverlayService resolved WindowManager from {rawWindowManager.Class?.CanonicalName ?? rawWindowManager.GetType().FullName ?? "<unknown>"}");
            return windowManager;
        } catch (Exception ex) {
            Log.Warn(LogTag, $"OverlayService failed to cast WindowManager service type={rawWindowManager.Class?.CanonicalName ?? rawWindowManager.GetType().FullName ?? "<unknown>"} error={ex.GetType().Name}");
            return null;
        }
    }

    private void RemoveOverlay() {
        MainThread.BeginInvokeOnMainThread(() => {
            if (_overlayView == null || _windowManager == null) {
                _overlayView = null;
                return;
            }

            try {
                _windowManager.RemoveViewImmediate(_overlayView);
                Log.Info(LogTag, "OverlayService removed overlay view");
            } catch {
                Log.Warn(LogTag, "OverlayService failed while removing overlay view");
            }

            _overlayView = null;
            _windowManager = null;
        });
    }

    private void BindViews(global::Android.Views.View view) {
        _clockText = view.FindViewById<TextView>(global::Pray_Ad_Free.Resource.Id.alarmClockText);
        _offsetText = view.FindViewById<TextView>(global::Pray_Ad_Free.Resource.Id.alarmOffsetText);
        _prayerNameText = view.FindViewById<TextView>(global::Pray_Ad_Free.Resource.Id.alarmPrayerNameText);
        _reminderText = view.FindViewById<TextView>(global::Pray_Ad_Free.Resource.Id.alarmReminderText);
        _snoozePicker = view.FindViewById<NumberPicker>(global::Pray_Ad_Free.Resource.Id.alarmSnoozePicker);
        _decreaseButton = view.FindViewById<global::Android.Widget.Button>(global::Pray_Ad_Free.Resource.Id.alarmDecreaseButton);
        _increaseButton = view.FindViewById<global::Android.Widget.Button>(global::Pray_Ad_Free.Resource.Id.alarmIncreaseButton);
        _snoozeButton = view.FindViewById<global::Android.Widget.Button>(global::Pray_Ad_Free.Resource.Id.alarmSnoozeButton);
        _stopButton = view.FindViewById<global::Android.Widget.Button>(global::Pray_Ad_Free.Resource.Id.alarmStopButton);

        if (_snoozePicker != null) {
            _snoozePicker.WrapSelectorWheel = false;
            _snoozePicker.MinValue = DefaultSnoozeDelayMinutes;
            _snoozePicker.MaxValue = DefaultMaxSnoozeDelayMinutes;
            _snoozePicker.Value = DefaultSnoozeDelayMinutes;
        }

        if (_decreaseButton != null) {
            _decreaseButton.Click += (_, _) => AdjustPicker(-1);
        }

        if (_increaseButton != null) {
            _increaseButton.Click += (_, _) => AdjustPicker(1);
        }

        if (_snoozeButton != null) {
            _snoozeButton.Text = LocalizationManager.Translate("AlarmSnoozeButton");
            _snoozeButton.Click += async (_, _) => await SnoozeAsync();
        }

        if (_stopButton != null) {
            _stopButton.Text = LocalizationManager.Translate("AlarmStopButton");
            _stopButton.Click += async (_, _) => await StopAsync();
        }
    }

    private async Task InitializeOverlayAsync() {
        if (string.IsNullOrWhiteSpace(_payloadText)) {
            return;
        }

        AndroidAlarmLaunchCoordinator.Enqueue(_payloadText);
        AndroidAlarmLaunchCoordinator.TryDispatchPending("AlarmOverlayService");

        var playbackService = await WaitForPlaybackServiceAsync(TimeSpan.FromSeconds(12)).ConfigureAwait(false);
        if (playbackService == null) {
            Log.Warn(LogTag, "OverlayService could not resolve playback service");
            return;
        }

        var model = await playbackService.BuildAlarmPresentationModelAsync(_payload).ConfigureAwait(false);
        MainThread.BeginInvokeOnMainThread(() => ApplyPresentation(model));
        Log.Info(LogTag, "OverlayService applied alarm presentation model");
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
            _reminderText.Text = LocalizationManager.Translate("AdhanPlaybackStopHint");
        }
    }

    private void ApplyPresentation(AlarmPresentationModel model) {
        _canSnooze = model.CanSnooze;
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
                ? LocalizationManager.Translate("AdhanPlaybackStopHint")
                : model.ReminderText;
        }

        if (_snoozePicker != null) {
            _snoozePicker.MinValue = model.MinDelayMinutes;
            _snoozePicker.MaxValue = model.MaxDelayMinutes;
            _snoozePicker.Value = Math.Clamp(model.InitialDelayMinutes, model.MinDelayMinutes, model.MaxDelayMinutes);
            _snoozePicker.Enabled = model.CanSnooze;
        }

        SetButtonsEnabled(!_isBusy);
    }

    private async Task StopAsync() {
        if (_isBusy) {
            return;
        }

        await ExecuteBusyAsync(async playbackService => {
            if (playbackService != null) {
                await playbackService.StopAsync().ConfigureAwait(false);
            }

            StopSelf();
        }).ConfigureAwait(false);
    }

    private async Task SnoozeAsync() {
        if (_isBusy || !_canSnooze) {
            return;
        }

        var delayMinutes = _snoozePicker?.Value ?? 10;
        await ExecuteBusyAsync(async playbackService => {
            if (playbackService == null) {
                return;
            }

            var scheduled = await playbackService.SnoozeAlarmAsync(_payload, delayMinutes).ConfigureAwait(false);
            if (scheduled) {
                StopSelf();
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
            MainThread.BeginInvokeOnMainThread(() => SetButtonsEnabled(true));
        }
    }

    private void SetButtonsEnabled(bool enabled) {
        if (_decreaseButton != null) {
            _decreaseButton.Enabled = enabled && _canSnooze;
        }

        if (_increaseButton != null) {
            _increaseButton.Enabled = enabled && _canSnooze;
        }

        if (_snoozePicker != null) {
            _snoozePicker.Enabled = enabled && _canSnooze;
        }

        if (_snoozeButton != null) {
            _snoozeButton.Enabled = enabled && _canSnooze;
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
        return LocalizationManager.TranslatePrayer(prayer);
    }

    private static string ResolveOverlayTitle() {
        return LocalizationManager.Translate("AlarmScreenTitle");
    }

    private static string ResolveOverlayBody() {
        return LocalizationManager.Translate("AdhanPlaybackStopHint");
    }
}
#endif
