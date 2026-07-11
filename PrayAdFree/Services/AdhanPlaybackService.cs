using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;
using Plugin.LocalNotification.EventArgs;
using Plugin.LocalNotification.WindowsOption;
using Microsoft.Extensions.DependencyInjection;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using Pray_Ad_Free.Platforms.Android;
#endif
#if WINDOWS
using Windows.Media.Core;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
#endif
#if IOS || MACCATALYST
using AVFoundation;
using Foundation;
#endif

namespace Pray_Ad_Free.Services;

public sealed class AdhanPlaybackService : IAdhanPlaybackService, IDisposable {
    public const int StopActionId = 54001;
    public const int ControlNotificationId = 54002;
    public const int Snooze10ActionId = 54003;
    public const int OpenCustomSnoozeActionId = 54004;
    public const int DeferredAdhanNotificationId = 54005;
    public const int AndroidDismissControlActionId = 54006;
    public const int PreviewNotificationId = 54007;
    public const string ControlReturningData = "adhan_control";
    public const string AndroidControlAction = "com.rynex.prayer.ADHAN_CONTROL";
    public const string AndroidControlActionIdExtra = "adhan_control_action_id";
    public const string AndroidAlarmAction = "com.rynex.prayer.ADHAN_ALARM_FULLSCREEN";
    private const int MinSnoozeMinutes = 4;
    private const int BufferBeforeNextPrayerMinutes = 30;
    public const string WindowsStopActionToken = WindowsNotificationActionParser.StopActionToken;
    public const string WindowsControlNotificationSourceToken = WindowsNotificationActionParser.ControlSourceToken;
    public const string WindowsControlNotificationTag = WindowsNotificationActionParser.ControlNotificationTag;

    private readonly SettingsService _settingsService;
    private readonly PrayerTimesService _prayerTimesService;
    private readonly IServiceProvider _serviceProvider;
    private readonly AlarmReminderCatalogService _alarmReminderCatalogService;
    private readonly IAppLogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _alarmScreenGate = new(1, 1);
    private ILocalNotificationScheduler? _localNotificationScheduler;
    private AdhanNotificationPayload? _activeScheduledPayload;
    private AdhanAlarmPayload? _activeAlarmPayload;
    private AlarmPresentationModel? _activeAlarmPresentation;
    private bool _initialized;
    private bool _disposed;
    private readonly object _alarmScreenStateLock = new();
    private AdhanAlarmPayload? _pendingAlarmScreenPayload;

#if ANDROID
    private Android.Media.MediaPlayer? _androidPlayer;
    private Android.Media.AudioManager? _androidAudioManager;
    private Android.Media.AudioFocusRequestClass? _androidAudioFocusRequest;
    private Android.Media.AudioManager.IOnAudioFocusChangeListener? _androidAudioFocusChangeListener;
    private bool _androidPausedForTransientLoss;
#endif
#if WINDOWS
    private Windows.Media.Playback.MediaPlayer? _windowsPlayer;
    private CancellationTokenSource? _windowsNotificationMonitorCts;
#endif
#if IOS || MACCATALYST
    private AVAudioPlayer? _applePlayer;
#endif

    public AdhanPlaybackService(
        SettingsService settingsService,
        PrayerTimesService prayerTimesService,
        AlarmReminderCatalogService alarmReminderCatalogService,
        IServiceProvider serviceProvider,
        IAppLogger logger) {
        _settingsService = settingsService;
        _prayerTimesService = prayerTimesService;
        _alarmReminderCatalogService = alarmReminderCatalogService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void Initialize() {
        if (_initialized) {
            return;
        }

        LocalNotificationCenter.Current.NotificationReceived += OnNotificationReceived;
        LocalNotificationCenter.Current.NotificationActionTapped += OnNotificationActionTapped;
        ClearExpiredPendingDeferredReminder();
        _initialized = true;
    }

    public async Task<bool> PlayPreviewAsync(string? soundKey) {
        var settings = _settingsService.Load();
        var effectiveSoundKey = AdhanSoundLibrary.ResolveEffectiveSoundKey(soundKey ?? settings.Notifications.SoundKey);
        if (AdhanSoundLibrary.IsSilent(effectiveSoundKey)) {
            await StopAsync().ConfigureAwait(false);
            return false;
        }

        var source = AdhanSoundLibrary.ResolvePlaybackSource(settings.Notifications, effectiveSoundKey);
        if (source == null) {
            return false;
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try {
            StopCore();
            _activeScheduledPayload = null;
            _activeAlarmPayload = null;
            StartCore(source, settings.Notifications.AdhanVolume);
            await ShowPreviewNotificationAsync().ConfigureAwait(false);
            return true;
        } catch (Exception ex) {
            _logger.LogException(ex, "AdhanPlaybackService.PlayPreviewAsync");
            return false;
        } finally {
            _gate.Release();
        }
    }

    public async Task<bool> PlayScheduledAsync(AdhanNotificationPayload payload) {
        var settings = _settingsService.Load();
        if (!settings.Notifications.EnableAdhan) {
            return false;
        }

        var source = AdhanSoundLibrary.ResolvePlaybackSource(settings.Notifications, payload.SoundKey);
        if (source == null) {
            return false;
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try {
            StopCore();
            _activeScheduledPayload = payload;
            _activeAlarmPayload = null;
            StartCore(source, settings.Notifications.AdhanVolume);
            var prayerName = LocalizationManager.TranslatePrayer(payload.Prayer);
            await ShowControlNotificationAsync(prayerName, includeSnoozeActions: true).ConfigureAwait(false);
            return true;
        } catch (Exception ex) {
            _logger.LogException(ex, "AdhanPlaybackService.PlayScheduledAsync");
            return false;
        } finally {
            _gate.Release();
        }
    }

    public async Task<bool> ScheduleTestAlarmAsync(string? soundKey, TimeSpan delay) {
        try {
            var settings = _settingsService.Load();
            var effectiveSoundKey = AdhanSoundLibrary.ResolveEffectiveSoundKey(soundKey ?? settings.Notifications.SoundKey);
            if (AdhanSoundLibrary.IsSilent(effectiveSoundKey)) {
                return false;
            }

            var triggerTime = DateTime.Now.Add(delay <= TimeSpan.Zero ? TimeSpan.FromSeconds(12) : delay);
            var payload = AdhanAlarmPayload.Build(PrayerId.Fajr, effectiveSoundKey, triggerTime, triggerTime);

#if ANDROID
            AndroidAdhanAlarmScheduler.UpsertAlarm(new AndroidAdhanAlarmScheduler.ScheduledAlarm(
                54008,
                triggerTime,
                payload));
            await Task.CompletedTask;
            return true;
#else
            var request = new NotificationRequest {
                NotificationId = 54008,
                CategoryType = NotificationCategoryType.Alarm,
                Title = LocalizationManager.Translate("TestAlarm"),
                Description = LocalizationManager.Translate("TestNotification"),
                ReturningData = payload,
                Schedule = new NotificationRequestSchedule {
                    NotifyTime = triggerTime,
                    NotifyRepeatInterval = null
                }
            };
            await LocalNotificationCenter.Current.Show(request).ConfigureAwait(false);
            return true;
#endif
        } catch (Exception ex) {
            _logger.LogException(ex, "AdhanPlaybackService.ScheduleTestAlarmAsync");
            return false;
        }
    }

    public async Task StopAsync() {
        await _gate.WaitAsync().ConfigureAwait(false);
        try {
            StopCore();
#if ANDROID
            AlarmOverlayService.StopOverlay(Android.App.Application.Context);
            CancelAndroidControlNotification();
#endif
#if WINDOWS
            await AppNotificationManager.Default.RemoveByTagAsync(WindowsControlNotificationTag);
#else
            LocalNotificationCenter.Current.Cancel(ControlNotificationId);
#endif
            LocalNotificationCenter.Current.Cancel(PreviewNotificationId);
        } catch (Exception ex) {
            _logger.LogException(ex, "AdhanPlaybackService.StopAsync");
        } finally {
            _gate.Release();
        }
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        LocalNotificationCenter.Current.NotificationReceived -= OnNotificationReceived;
        LocalNotificationCenter.Current.NotificationActionTapped -= OnNotificationActionTapped;
        StopCore();
        _gate.Dispose();
        _alarmScreenGate.Dispose();
    }

    private void OnNotificationReceived(NotificationEventArgs e) {
        _ = HandleNotificationReceivedAsync(e);
    }

    private void OnNotificationActionTapped(NotificationActionEventArgs e) {
        _ = HandleNotificationActionTappedAsync(e);
    }

    private async Task HandleNotificationReceivedAsync(NotificationEventArgs e) {
        try {
            var request = e?.Request;
            if (request == null) {
                return;
            }

            if (AdhanAlarmPayload.TryParse(request.ReturningData, out var alarmPayload)) {
                await HandleAlarmNotificationAsync(request, alarmPayload, triggeredByTap: false).ConfigureAwait(false);
                return;
            }

            if (!AdhanNotificationPayload.TryParse(request.ReturningData, out var payload)) {
                return;
            }

            if (request.NotificationId == DeferredAdhanNotificationId) {
                ClearPendingDeferredReminder();
            }

            var settings = _settingsService.Load();
            if (!settings.Notifications.EnableAdhan) {
                return;
            }

            var source = AdhanSoundLibrary.ResolvePlaybackSource(settings.Notifications, payload.SoundKey);
            if (source == null) {
                return;
            }

            if (request.NotificationId > 0 && request.NotificationId != ControlNotificationId) {
                LocalNotificationCenter.Current.Cancel(request.NotificationId);
            }

            await _gate.WaitAsync().ConfigureAwait(false);
            try {
                StopCore();
                _activeScheduledPayload = payload;
                _activeAlarmPayload = null;
                StartCore(source, settings.Notifications.AdhanVolume);
                var prayerName = LocalizationManager.TranslatePrayer(payload.Prayer);
                await ShowControlNotificationAsync(prayerName, includeSnoozeActions: true).ConfigureAwait(false);
            } finally {
                _gate.Release();
            }
        } catch (Exception ex) {
            _logger.LogException(ex, "AdhanPlaybackService.HandleNotificationReceivedAsync");
        }
    }

    private async Task HandleAlarmNotificationAsync(
        NotificationRequest request,
        AdhanAlarmPayload payload,
        bool triggeredByTap) {
        if (!triggeredByTap && !OperatingSystem.IsAndroid()) {
            return;
        }

        if (request.NotificationId == DeferredAdhanNotificationId) {
            ClearPendingDeferredReminder();
        }

        var settings = _settingsService.Load();
        if (!settings.Notifications.EnableAdhan) {
            return;
        }

        if (!OperatingSystem.IsAndroid() &&
            request.NotificationId > 0 &&
            request.NotificationId != ControlNotificationId) {
            LocalNotificationCenter.Current.Cancel(request.NotificationId);
        }

#if ANDROID
        TryLaunchAndroidAlarmActivity(payload);
#endif
        await ActivateAlarmAsync(payload, settings, showAlarmScreen: !OperatingSystem.IsAndroid()).ConfigureAwait(false);
    }

#if ANDROID
    public async Task HandleAndroidAlarmLaunchAsync(
        AdhanAlarmPayload payload,
        string source = "Android",
        AlarmPresentationMode presentationMode = AlarmPresentationMode.FullscreenActivity) {
        try {
            var settings = _settingsService.Load();
            if (!settings.Notifications.EnableAdhan) {
                return;
            }

            if (IsMatchingPendingDeferredReminder(settings, payload)) {
                ClearPendingDeferredReminder();
                settings = _settingsService.Load();
            }

            _logger.LogEvent(
                "AdhanAlarmLaunch.Android",
                $"source={source};presentationMode={presentationMode};payload={BuildAlarmPayloadKey(payload)}");
            await ActivateAlarmAsync(
                payload,
                settings,
                showAlarmScreen: presentationMode == AlarmPresentationMode.FullscreenActivity).ConfigureAwait(false);
            if (presentationMode == AlarmPresentationMode.ControlNotification) {
                var prayerName = LocalizationManager.TranslatePrayer(payload.Prayer);
                await ShowControlNotificationAsync(prayerName, includeSnoozeActions: true).ConfigureAwait(false);
            }
        } catch (Exception ex) {
            _logger.LogException(ex, "AdhanPlaybackService.HandleAndroidAlarmLaunchAsync");
        }
    }
#endif

    private async Task HandleNotificationActionTappedAsync(NotificationActionEventArgs e) {
        try {
            if (e == null) {
                return;
            }

            var isControlNotification = IsControlNotificationRequest(e.Request);
            var isAdhanNotification = IsAdhanNotificationRequest(e.Request);
            _logger.LogEvent(
                "AdhanNotificationAction",
                $"actionId={e.ActionId};isTapped={e.IsTapped};isDismissed={e.IsDismissed};notifId={e.Request?.NotificationId};returningData={e.Request?.ReturningData ?? "null"};isAdhan={isAdhanNotification};isControl={isControlNotification}");

            if (e.Request != null && AdhanAlarmPayload.TryParse(e.Request.ReturningData, out var alarmPayload)) {
                if (!OperatingSystem.IsAndroid() &&
                    e.ActionId == NotificationActionEventArgs.TapActionId) {
                    await HandleAlarmNotificationAsync(e.Request, alarmPayload, triggeredByTap: true).ConfigureAwait(false);
                }
                return;
            }

            if (e.ActionId == Snooze10ActionId ||
                e.ActionId == OpenCustomSnoozeActionId ||
                e.ActionId == StopActionId) {
                if (!isControlNotification) {
                    return;
                }

                await HandleControlActionAsync(e.ActionId).ConfigureAwait(false);
                return;
            }

            if (!isControlNotification) {
                return;
            }

            if (e.ActionId == NotificationActionEventArgs.TapActionId ||
                e.ActionId == NotificationActionEventArgs.DismissedActionId) {
                await StopAsync().ConfigureAwait(false);
            }
        } catch (Exception ex) {
            _logger.LogException(ex, "AdhanPlaybackService.HandleNotificationActionTappedAsync");
        }
    }

    public async Task HandleControlActionAsync(int actionId) {
        switch (actionId) {
            case Snooze10ActionId:
                await HandleSnooze10ActionAsync().ConfigureAwait(false);
                return;
            case OpenCustomSnoozeActionId:
                await HandleOpenCustomSnoozeActionAsync().ConfigureAwait(false);
                return;
            case StopActionId:
            case AndroidDismissControlActionId:
                await StopAsync().ConfigureAwait(false);
                return;
        }
    }

    private async Task HandleSnooze10ActionAsync() {
        if (!_activeScheduledPayload.HasValue) {
            return;
        }

        var payload = _activeScheduledPayload.Value;
        await StopAsync().ConfigureAwait(false);
        await ScheduleDeferredReminderAsync(payload, 10, basePrayerTime: null, openAlarmScreen: false).ConfigureAwait(false);
    }

    private async Task HandleOpenCustomSnoozeActionAsync() {
        if (!_activeScheduledPayload.HasValue) {
            return;
        }

        var payload = _activeScheduledPayload.Value;
        await StopAsync().ConfigureAwait(false);
        var settings = _settingsService.Load();
        var alarmPayload = new AdhanAlarmPayload(payload.Prayer, payload.SoundKey, DateTime.Now, DateTime.Now);
        await ShowAlarmPageAsync(alarmPayload, settings).ConfigureAwait(false);
    }

    public async Task<AlarmPresentationModel> BuildAlarmPresentationModelAsync(AdhanAlarmPayload payload) {
        var settings = _settingsService.Load();
        return await BuildAlarmPresentationModelAsync(payload, settings).ConfigureAwait(false);
    }

    public async Task<AlarmPresentationModel?> GetActiveAlarmPresentationModelAsync() {
        AdhanAlarmPayload? payload;
        AlarmPresentationModel? presentation;
        await _gate.WaitAsync().ConfigureAwait(false);
        try {
            payload = _activeAlarmPayload;
            presentation = _activeAlarmPresentation;
        } finally {
            _gate.Release();
        }

        if (!payload.HasValue) {
            return null;
        }

        return presentation ?? await BuildAlarmPresentationModelAsync(payload.Value).ConfigureAwait(false);
    }

    public async Task<bool> SnoozeActiveAlarmAsync(int delayMinutes) {
        AdhanAlarmPayload? payload;
        await _gate.WaitAsync().ConfigureAwait(false);
        try {
            payload = _activeAlarmPayload;
        } finally {
            _gate.Release();
        }

        return payload.HasValue &&
            await SnoozeAlarmAsync(payload.Value, delayMinutes).ConfigureAwait(false);
    }

    public async Task<bool> SnoozeAlarmAsync(AdhanAlarmPayload payload, int delayMinutes) {
        await StopAsync().ConfigureAwait(false);
        return await ScheduleDeferredReminderAsync(
            new AdhanNotificationPayload(payload.Prayer, payload.SoundKey),
            delayMinutes,
            payload.BasePrayerTime,
            openAlarmScreen: true).ConfigureAwait(false);
    }

    public async Task TryPresentPendingAlarmScreenAsync(string reason) {
        AdhanAlarmPayload payload;
        lock (_alarmScreenStateLock) {
            if (!_pendingAlarmScreenPayload.HasValue) {
                return;
            }

            payload = _pendingAlarmScreenPayload.Value;
        }

        var settings = _settingsService.Load();
        if (!settings.Notifications.EnableAdhan) {
            ClearPendingAlarmScreen(payload);
            return;
        }

        _logger.LogEvent("AdhanAlarmScreenRetry", $"reason={reason};payload={BuildAlarmPayloadKey(payload)}");
        await ShowAlarmPageAsync(payload, settings, queueOnFailure: true, navigationWait: TimeSpan.FromSeconds(3)).ConfigureAwait(false);
    }

    private async Task<AlarmPresentationModel> BuildAlarmPresentationModelAsync(AdhanAlarmPayload payload, AppSettings settings) {
        var window = await TryBuildSnoozeWindowAsync(DateTime.Now).ConfigureAwait(false);
        var canSnooze = window != null && window.Value.MaxDelayMinutes >= MinSnoozeMinutes;
        var maxDelayMinutes = canSnooze ? window!.Value.MaxDelayMinutes : MinSnoozeMinutes;
        var initialDelay = canSnooze
            ? Math.Clamp(10, MinSnoozeMinutes, maxDelayMinutes)
            : MinSnoozeMinutes;
        var delayLabel = FormatDelayOffset(payload.NotifyTime - payload.BasePrayerTime);
        var reminderText = ResolveRandomAlarmReminderText(settings);

        return new AlarmPresentationModel(
            PrayerClock: TimeFormatHelper.FormatTime(payload.BasePrayerTime, settings.ClockFormat),
            DelayFromBase: delayLabel,
            PrayerName: LocalizationManager.TranslatePrayer(payload.Prayer),
            ReminderText: reminderText,
            CanSnooze: canSnooze,
            MinDelayMinutes: MinSnoozeMinutes,
            MaxDelayMinutes: maxDelayMinutes,
            InitialDelayMinutes: initialDelay);
    }

    private async Task<bool> ShowAlarmPageAsync(
        AdhanAlarmPayload payload,
        AppSettings settings,
        bool queueOnFailure = true,
        TimeSpan? navigationWait = null) {
        await _alarmScreenGate.WaitAsync().ConfigureAwait(false);
        try {
            var navigation = await WaitForNavigationAsync(navigationWait ?? TimeSpan.FromSeconds(12)).ConfigureAwait(false);
            if (navigation == null) {
                if (queueOnFailure) {
                    QueuePendingAlarmScreen(payload);
                }
                _logger.LogEvent("AdhanAlarmScreen", "navigation_unavailable");
                return false;
            }

            var shown = false;
            await MainThread.InvokeOnMainThreadAsync(async () => {
                try {
                    if (navigation.ModalStack.Count > 0 && navigation.ModalStack[^1] is Pages.AdhanSnoozePage) {
                        ClearPendingAlarmScreen(payload);
                        shown = true;
                        return;
                    }

                    var page = _serviceProvider.GetRequiredService<Pages.AdhanSnoozePage>();

                    await navigation.PushModalAsync(page);
                    ClearPendingAlarmScreen(payload);
                    shown = true;
                } catch (Exception ex) {
                    if (queueOnFailure) {
                        QueuePendingAlarmScreen(payload);
                    }
                    _logger.LogException(ex, "AdhanPlaybackService.ShowAlarmPageAsync");
                }
            });
            return shown;
        } finally {
            _alarmScreenGate.Release();
        }
    }

    private async Task ActivateAlarmAsync(AdhanAlarmPayload payload, AppSettings settings, bool showAlarmScreen = true) {
        var source = AdhanSoundLibrary.ResolvePlaybackSource(settings.Notifications, payload.SoundKey);
        if (source == null) {
            return;
        }

        var presentation = await BuildAlarmPresentationModelAsync(payload, settings).ConfigureAwait(false);

        await _gate.WaitAsync().ConfigureAwait(false);
        try {
            StopCore();
            _activeScheduledPayload = new AdhanNotificationPayload(payload.Prayer, payload.SoundKey);
            _activeAlarmPayload = payload;
            _activeAlarmPresentation = presentation;
            StartCore(source, settings.Notifications.AdhanVolume);
        } finally {
            _gate.Release();
        }

        if (showAlarmScreen) {
            await ShowAlarmPageAsync(payload, settings).ConfigureAwait(false);
        }
    }

    private static bool IsMatchingPendingDeferredReminder(AppSettings settings, AdhanAlarmPayload payload) {
        var pending = settings.Notifications.PendingDeferredReminder;
        if (pending == null || !pending.OpenAlarmScreen) {
            return false;
        }

        return pending.Prayer == payload.Prayer &&
            string.Equals(
                AdhanSoundLibrary.ResolveEffectiveSoundKey(pending.SoundKey),
                AdhanSoundLibrary.ResolveEffectiveSoundKey(payload.SoundKey),
                StringComparison.OrdinalIgnoreCase) &&
            Math.Abs((pending.NotifyTime - payload.NotifyTime).TotalSeconds) <= 2;
    }

    private static async Task<INavigation?> WaitForNavigationAsync(TimeSpan timeout) {
        var deadlineUtc = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadlineUtc) {
            var navigation = Shell.Current?.Navigation
                ?? Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
            if (navigation != null) {
                return navigation;
            }

            await Task.Delay(120).ConfigureAwait(false);
        }

        return null;
    }

    private void QueuePendingAlarmScreen(AdhanAlarmPayload payload) {
        lock (_alarmScreenStateLock) {
            _pendingAlarmScreenPayload = payload;
        }
    }

    private void ClearPendingAlarmScreen(AdhanAlarmPayload payload) {
        lock (_alarmScreenStateLock) {
            if (_pendingAlarmScreenPayload.HasValue &&
                string.Equals(
                    BuildAlarmPayloadKey(_pendingAlarmScreenPayload.Value),
                    BuildAlarmPayloadKey(payload),
                    StringComparison.Ordinal)) {
                _pendingAlarmScreenPayload = null;
            }
        }
    }

    private static string BuildAlarmPayloadKey(AdhanAlarmPayload payload) {
        return AdhanAlarmPayload.Build(payload.Prayer, payload.SoundKey, payload.BasePrayerTime, payload.NotifyTime);
    }

    private string ResolveRandomAlarmReminderText(AppSettings settings) {
        try {
            var items = new List<string>();
            var alarmSettings = settings.AlarmReminders ?? new AlarmRemindersSettings();
            var disabled = new HashSet<string>(alarmSettings.DisabledBuiltInIds ?? [], StringComparer.OrdinalIgnoreCase);
            var builtIn = _alarmReminderCatalogService.LoadForCurrentLanguage();
            foreach (var item in builtIn) {
                if (string.IsNullOrWhiteSpace(item.Text) || disabled.Contains(item.Id)) {
                    continue;
                }

                items.Add(item.Text.Trim());
            }

            foreach (var item in alarmSettings.UserItems ?? []) {
                if (!item.IsEnabled || string.IsNullOrWhiteSpace(item.Text)) {
                    continue;
                }

                items.Add(item.Text.Trim());
            }

            if (items.Count == 0) {
                return string.Empty;
            }

            return items[Random.Shared.Next(items.Count)];
        } catch (Exception ex) {
            _logger.LogException(ex, "AdhanPlaybackService.ResolveRandomAlarmReminderText");
            return string.Empty;
        }
    }

    private static bool IsAdhanNotificationRequest(NotificationRequest? request) {
        if (request == null) {
            return false;
        }

        if (request.NotificationId == ControlNotificationId) {
            return true;
        }

        if (string.IsNullOrWhiteSpace(request.ReturningData)) {
            return false;
        }

        if (string.Equals(request.ReturningData, ControlReturningData, StringComparison.Ordinal)) {
            return true;
        }

        return AdhanNotificationPayload.TryParse(request.ReturningData, out _)
            || AdhanAlarmPayload.TryParse(request.ReturningData, out _);
    }

    private static bool IsControlNotificationRequest(NotificationRequest? request) {
        if (request == null) {
            return false;
        }

        if (request.NotificationId == ControlNotificationId) {
            return true;
        }

        return string.Equals(request.ReturningData, ControlReturningData, StringComparison.Ordinal);
    }

    private async Task ShowControlNotificationAsync(string prayerName, bool includeSnoozeActions) {
#if ANDROID
        ShowAndroidControlNotification(prayerName, includeSnoozeActions);
        await Task.CompletedTask;
#elif WINDOWS
        var notification = new AppNotificationBuilder()
            .AddArgument("source", WindowsControlNotificationSourceToken)
            .AddText(prayerName)
            .AddText(LocalizationManager.Translate("AdhanPlaybackStopHint"))
            .AddButton(new AppNotificationButton(LocalizationManager.Translate("StopAdhan"))
                .AddArgument("action", WindowsStopActionToken)
                .AddArgument("source", WindowsControlNotificationSourceToken))
            .BuildNotification();
        notification.Tag = WindowsControlNotificationTag;
        AppNotificationManager.Default.Show(notification);
        StartWindowsNotificationMonitor();
        await Task.CompletedTask;
#else
        var categoryType = NotificationCategoryType.Status;
        var buttonsMode = "stop_only";
        int? maxDelayMinutes = null;
        if (includeSnoozeActions) {
            var window = await TryBuildSnoozeWindowAsync(DateTime.Now).ConfigureAwait(false);
            if (window == null) {
                categoryType = NotificationCategoryType.Recommendation;
                buttonsMode = "stop_10_custom";
            } else if (window.Value.MaxDelayMinutes >= 10) {
                categoryType = NotificationCategoryType.Recommendation;
                buttonsMode = "stop_10_custom";
                maxDelayMinutes = window.Value.MaxDelayMinutes;
            } else if (window.Value.MaxDelayMinutes >= MinSnoozeMinutes) {
                categoryType = NotificationCategoryType.Event;
                buttonsMode = "stop_custom";
                maxDelayMinutes = window.Value.MaxDelayMinutes;
            } else {
                maxDelayMinutes = window.Value.MaxDelayMinutes;
            }
        }

        _logger.LogEvent(
            "AdhanControlNotification",
            $"category={categoryType};buttons={buttonsMode};maxDelay={(maxDelayMinutes.HasValue ? maxDelayMinutes.Value.ToString() : "null")}");

        var request = new NotificationRequest {
            NotificationId = ControlNotificationId,
            CategoryType = categoryType,
            Title = prayerName,
            Description = LocalizationManager.Translate("AdhanPlaybackStopHint"),
            Silent = false,
            ReturningData = ControlReturningData,
            Android = new AndroidOptions {
                ChannelId = "adhan_playback_control",
                Priority = AndroidPriority.Max,
                Ongoing = true,
                AutoCancel = false,
                VisibilityType = AndroidVisibilityType.Public,
                LaunchAppWhenTapped = false
            },
            Windows = new WindowsOptions {
                LaunchAppWhenTapped = false
            }
        };

        await LocalNotificationCenter.Current.Show(request).ConfigureAwait(false);
#endif
    }

    private async Task ShowPreviewNotificationAsync() {
        var request = new NotificationRequest {
            NotificationId = PreviewNotificationId,
            CategoryType = NotificationCategoryType.None,
            Title = LocalizationManager.Translate("TestNotification"),
            Description = LocalizationManager.Translate("AdhanPreviewTitle"),
            Silent = false
        };

#if ANDROID
        var context = Android.App.Application.Context;
        if (context != null) {
            EnsureAndroidPrayerNotificationChannel(context);
        }

        request.Android = new AndroidOptions {
            Priority = AndroidPriority.High,
            ChannelId = LocalNotificationScheduler.PrayerNotificationChannelId,
            Ongoing = false,
            AutoCancel = true,
            LaunchAppWhenTapped = true,
            VisibilityType = AndroidVisibilityType.Public
        };
#endif

#if WINDOWS
        request.Windows = new WindowsOptions {
            LaunchAppWhenTapped = true
        };
#endif

        await LocalNotificationCenter.Current.Show(request).ConfigureAwait(false);
    }

#if ANDROID
    private void ShowAndroidControlNotification(string prayerName, bool includeSnoozeActions) {
        var context = Android.App.Application.Context;
        if (context == null) {
            return;
        }

        EnsureAndroidControlChannel(context);

        var title = BuildPrayerNotificationTitle(prayerName);
        var body = BuildPrayerNotificationBody(prayerName);
        var maxDelayMinutes = includeSnoozeActions
            ? TryBuildSnoozeWindowAsync(DateTime.Now).GetAwaiter().GetResult()?.MaxDelayMinutes ?? int.MaxValue
            : int.MinValue;
        var actionsSummary = BuildAndroidControlSummary(includeSnoozeActions, maxDelayMinutes);
        var actionsDetails = BuildAndroidControlDetailText(includeSnoozeActions, maxDelayMinutes);
        var details = $"{LocalizationManager.Translate("AdhanPlaybackStopHint")}{System.Environment.NewLine}{actionsDetails}";

        Notification.Builder builder;
        if (OperatingSystem.IsAndroidVersionAtLeast(26)) {
            builder = new Notification.Builder(context, "adhan_playback_control");
        } else {
            builder = new Notification.Builder(context);
        }

        builder
            .SetSmallIcon(context.ApplicationInfo?.Icon ?? Android.Resource.Drawable.IcDialogInfo)
            .SetContentTitle(title)
            .SetContentText(body)
            .SetVisibility(NotificationVisibility.Public)
            .SetCategory(Notification.CategoryAlarm)
            .SetOngoing(true)
            .SetAutoCancel(false)
            .SetOnlyAlertOnce(false)
            .SetShowWhen(true)
            .SetContentIntent(BuildAndroidNoOpPendingIntent(context))
            .SetDeleteIntent(BuildAndroidControlActionPendingIntent(context, AndroidDismissControlActionId));
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) {
#pragma warning disable CA1422
            builder!.SetPriority((int)NotificationPriority.Max);
            builder.SetVibrate(new long[] { 0, 80, 70, 80 });
#pragma warning restore CA1422
        }

        var compactActionIndexes = new List<int>();
        if (includeSnoozeActions) {
            if (maxDelayMinutes >= 10) {
                builder.AddAction(BuildAndroidNativeAction(Android.Resource.Drawable.IcLockIdleAlarm, ResolveSnooze10Title(), BuildAndroidControlActionPendingIntent(context, Snooze10ActionId)));
                compactActionIndexes.Add(compactActionIndexes.Count);
                builder.AddAction(BuildAndroidNativeAction(Android.Resource.Drawable.IcMenuEdit, ResolveCustomSnoozeTitle(), BuildAndroidControlLaunchPendingIntent(context, OpenCustomSnoozeActionId)));
                compactActionIndexes.Add(compactActionIndexes.Count);
            } else if (maxDelayMinutes >= MinSnoozeMinutes) {
                builder.AddAction(BuildAndroidNativeAction(Android.Resource.Drawable.IcMenuEdit, ResolveCustomSnoozeTitle(), BuildAndroidControlLaunchPendingIntent(context, OpenCustomSnoozeActionId)));
                compactActionIndexes.Add(compactActionIndexes.Count);
            }
        }

        builder.AddAction(BuildAndroidNativeAction(Android.Resource.Drawable.IcMenuCloseClearCancel, ResolveStopTitle(), BuildAndroidControlActionPendingIntent(context, StopActionId)));
        compactActionIndexes.Add(compactActionIndexes.Count);

        builder.SetStyle(new Notification.BigTextStyle().BigText(details));

        _logger.LogEvent(
            "AdhanControlNotification.Android",
            $"includeSnooze={includeSnoozeActions};maxDelay={maxDelayMinutes};title={title};body={body};actions={actionsSummary}");

        var manager = context.GetSystemService(Context.NotificationService) as NotificationManager;
        manager?.Notify(ControlNotificationId, builder.Build());
    }

    private static string BuildAndroidControlSummary(bool includeSnoozeActions, int maxDelayMinutes) {
        var labels = new List<string>();
        if (includeSnoozeActions) {
            if (maxDelayMinutes >= 10) {
                labels.Add(ResolveSnooze10Title());
            }

            if (maxDelayMinutes >= MinSnoozeMinutes) {
                labels.Add(ResolveCustomSnoozeTitle());
            }
        }

        labels.Add(ResolveStopTitle());
        return string.Join(" | ", labels);
    }

    private static string BuildAndroidControlDetailText(bool includeSnoozeActions, int maxDelayMinutes) {
        var lines = new List<string>();
        if (includeSnoozeActions && maxDelayMinutes >= 10) {
            lines.Add($"- {ResolveSnooze10Title()}");
        }

        if (includeSnoozeActions && maxDelayMinutes >= MinSnoozeMinutes) {
            lines.Add($"- {ResolveCustomSnoozeTitle()}");
        }

        lines.Add($"- {ResolveStopTitle()}");
        return string.Join(System.Environment.NewLine, lines);
    }

    private static Notification.Action BuildAndroidNativeAction(int iconId, string title, PendingIntent pendingIntent) {
#pragma warning disable CA1422
        return new Notification.Action.Builder(iconId, title, pendingIntent).Build();
#pragma warning restore CA1422
    }

    private static PendingIntent BuildAndroidControlActionPendingIntent(Context context, int actionId) {
        var intent = new Intent(context, typeof(Pray_Ad_Free.Platforms.Android.AdhanControlActionReceiver));
        intent.SetAction(AndroidControlAction);
        intent.PutExtra(AndroidControlActionIdExtra, actionId);

        var flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23)) {
            flags |= PendingIntentFlags.Immutable;
        }

        return PendingIntent.GetBroadcast(context, actionId, intent, flags)!;
    }

    private static PendingIntent BuildAndroidControlLaunchPendingIntent(Context context, int actionId) {
        var launchIntent = BuildAndroidMainLaunchIntent(context);
        launchIntent.SetAction(AndroidControlAction);
        launchIntent.PutExtra(AndroidControlActionIdExtra, actionId);
        launchIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);

        var flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23)) {
            flags |= PendingIntentFlags.Immutable;
        }

        return PendingIntent.GetActivity(context, actionId, launchIntent, flags)!;
    }

    private static PendingIntent BuildAndroidContentPendingIntent(Context context) {
        var launchIntent = BuildAndroidMainLaunchIntent(context);
        launchIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);

        var flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23)) {
            flags |= PendingIntentFlags.Immutable;
        }

        return PendingIntent.GetActivity(context, 0, launchIntent, flags)!;
    }

    private static PendingIntent BuildAndroidNoOpPendingIntent(Context context) {
        var intent = new Intent(context, typeof(Pray_Ad_Free.Platforms.Android.AdhanControlActionReceiver));
        intent.SetAction("com.rynex.prayer.ADHAN_NOOP");

        var flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23)) {
            flags |= PendingIntentFlags.Immutable;
        }

        return PendingIntent.GetBroadcast(context, 0, intent, flags)!;
    }

    private static Intent BuildAndroidMainLaunchIntent(Context context) {
        var packageName = context.PackageName;
        var launchIntent = !string.IsNullOrWhiteSpace(packageName)
            ? context.PackageManager?.GetLaunchIntentForPackage(packageName)
            : null;
        return launchIntent ?? new Intent(context, typeof(MainActivity));
    }

    private static void TryLaunchAndroidAlarmActivity(AdhanAlarmPayload payload) {
        try {
            var context = Android.App.Application.Context;
            if (context == null) {
                return;
            }

            var launchIntent = new Intent(context, typeof(global::Pray_Ad_Free.Platforms.Android.AlarmActivity));
            launchIntent.SetAction(AndroidAlarmAction);
            launchIntent.PutExtra(
                global::Pray_Ad_Free.Platforms.Android.AndroidAdhanAlarmScheduler.AlarmPayloadExtra,
                BuildAlarmPayloadKey(payload));
            launchIntent.AddFlags(
                ActivityFlags.NewTask |
                ActivityFlags.SingleTop |
                ActivityFlags.ClearTop);
            context.StartActivity(launchIntent);
        } catch {
        }
    }

    private static void EnsureAndroidControlChannel(Context context) {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) {
            return;
        }

        var manager = context.GetSystemService(Context.NotificationService) as NotificationManager;
        if (manager == null) {
            return;
        }

        var existing = manager.GetNotificationChannel("adhan_playback_control");
        if (existing != null) {
            return;
        }

        var channel = new NotificationChannel(
            "adhan_playback_control",
            LocalizationManager.Translate("AdhanReminder"),
            NotificationImportance.High) {
            Description = LocalizationManager.Translate("AdhanPlaybackStopHint")
        };
        channel.SetSound(null, null);
        channel.EnableVibration(true);
        channel.SetVibrationPattern(new long[] { 0, 120, 90, 120 });
        channel.LockscreenVisibility = NotificationVisibility.Public;
        manager.CreateNotificationChannel(channel);
    }

    private static void EnsureAndroidPrayerNotificationChannel(Context context) {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) {
            return;
        }

        var manager = context.GetSystemService(Context.NotificationService) as NotificationManager;
        if (manager == null) {
            return;
        }

        var existing = manager.GetNotificationChannel(LocalNotificationScheduler.PrayerNotificationChannelId);
        if (existing != null) {
            return;
        }

        var audioAttributesBuilder = new Android.Media.AudioAttributes.Builder();
        audioAttributesBuilder.SetUsage(Android.Media.AudioUsageKind.Notification);
        audioAttributesBuilder.SetContentType(Android.Media.AudioContentType.Sonification);
        var audioAttributes = audioAttributesBuilder.Build();
        var defaultSound = Android.Media.RingtoneManager.GetDefaultUri(Android.Media.RingtoneType.Notification);

        var channel = new NotificationChannel(
            LocalNotificationScheduler.PrayerNotificationChannelId,
            LocalizationManager.Translate("AdhanReminders"),
            NotificationImportance.High) {
            Description = LocalizationManager.Translate("ReminderType_Notification")
        };
        channel.EnableVibration(true);
        channel.SetVibrationPattern(new long[] { 0, 120, 80, 120 });
        channel.LockscreenVisibility = NotificationVisibility.Public;
        channel.SetSound(defaultSound, audioAttributes);
        manager.CreateNotificationChannel(channel);
    }

    private static void CancelAndroidControlNotification() {
        var context = Android.App.Application.Context;
        if (context == null) {
            return;
        }

        var manager = context.GetSystemService(Context.NotificationService) as NotificationManager;
        manager?.Cancel(ControlNotificationId);
    }

    private static string ResolveStopTitle() {
        return LocalizationManager.Translate("Stop");
    }

    private static string ResolveSnooze10Title() {
        return string.Format(LocalizationManager.Translate("SnoozeDelayLabel"), 10);
    }

    private static string ResolveCustomSnoozeTitle() {
        return LocalizationManager.Translate("SnoozePageTitle");
    }

    private static string BuildPrayerNotificationTitle(string prayerName) {
        var template = LocalizationManager.Translate("Notification_PrayerTitle");
        return FormatTemplate(template, prayerName);
    }

    private static string BuildPrayerNotificationBody(string prayerName) {
        var template = LocalizationManager.Translate("Notification_PrayerBody");
        return FormatTemplate(template, prayerName);
    }

    private static string FormatTemplate(string template, string prayerName) {
        try {
            if (template.Contains("{0}", StringComparison.Ordinal)) {
                return string.Format(template, prayerName);
            }

            return $"{template} {prayerName}".Trim();
        } catch {
            return $"{template} {prayerName}".Trim();
        }
    }
#endif

    private async Task<SnoozeWindow?> TryBuildSnoozeWindowAsync(DateTime now) {
        try {
            var settings = _settingsService.Load();
            var today = DateOnly.FromDateTime(now);
            var month = await _prayerTimesService
                .GetMonthAsync(settings, now.Year, now.Month, CancellationToken.None)
                .ConfigureAwait(false);

            var day = month.Days.FirstOrDefault(item => item.Date == today);
            if (day == null) {
                return null;
            }

            var (nextPrayerId, nextPrayerTime) = NextPrayerCalculator.GetNext(day, now);
            var maxDelayMinutes = (int)Math.Floor((nextPrayerTime - now - TimeSpan.FromMinutes(BufferBeforeNextPrayerMinutes)).TotalMinutes);
            return new SnoozeWindow(maxDelayMinutes, nextPrayerId, nextPrayerTime);
        } catch (Exception ex) {
            _logger.LogException(ex, "AdhanPlaybackService.TryBuildSnoozeWindowAsync");
            return null;
        }
    }

    private async Task<bool> ScheduleDeferredReminderAsync(
        AdhanNotificationPayload payload,
        int delayMinutes,
        DateTime? basePrayerTime,
        bool openAlarmScreen) {
        try {
            var now = DateTime.Now;
            var window = await TryBuildSnoozeWindowAsync(now).ConfigureAwait(false);
            if (window == null || window.Value.MaxDelayMinutes < MinSnoozeMinutes) {
                return false;
            }

            if (delayMinutes < MinSnoozeMinutes || delayMinutes > window.Value.MaxDelayMinutes) {
                return false;
            }

            var effectiveSoundKey = AdhanSoundLibrary.ResolveEffectiveSoundKey(payload.SoundKey);
            if (AdhanSoundLibrary.IsSilent(effectiveSoundKey)) {
                return false;
            }

            var settings = _settingsService.Load();
            var pendingReminder = new DeferredAdhanReminder {
                NotifyTime = now.AddMinutes(delayMinutes),
                BasePrayerTime = basePrayerTime ?? now,
                Prayer = payload.Prayer,
                SoundKey = effectiveSoundKey,
                OpenAlarmScreen = openAlarmScreen
            };

            var updated = CloneSettingsWithPendingReminder(settings, pendingReminder);
            _settingsService.Save(updated);
            await ScheduleDeferredNotificationNowAsync(updated, pendingReminder).ConfigureAwait(false);
            return true;
        } catch (Exception ex) {
            _logger.LogException(ex, "AdhanPlaybackService.ScheduleDeferredReminderAsync");
            return false;
        }
    }

    private static async Task ScheduleDeferredNotificationNowAsync(AppSettings settings, DeferredAdhanReminder pendingReminder) {
#if ANDROID
        if (pendingReminder.OpenAlarmScreen) {
            AndroidAdhanAlarmScheduler.UpsertAlarm(new AndroidAdhanAlarmScheduler.ScheduledAlarm(
                DeferredAdhanNotificationId,
                pendingReminder.NotifyTime,
                AdhanAlarmPayload.Build(
                    pendingReminder.Prayer,
                    pendingReminder.SoundKey,
                    pendingReminder.BasePrayerTime == default ? pendingReminder.NotifyTime : pendingReminder.BasePrayerTime,
                    pendingReminder.NotifyTime)));
            await Task.CompletedTask;
            return;
        }
#endif
        var request = new NotificationRequest {
            NotificationId = DeferredAdhanNotificationId,
            CategoryType = pendingReminder.OpenAlarmScreen ? NotificationCategoryType.Alarm : NotificationCategoryType.None,
            Title = LocalizationManager.Translate("AdhanReminder"),
            Description = LocalizationManager.Translate("SnoozeReminderBody"),
            Silent = ResolveDeferredNotificationSilent(),
            Sound = string.Empty,
            ReturningData = pendingReminder.OpenAlarmScreen
                ? AdhanAlarmPayload.Build(
                    pendingReminder.Prayer,
                    pendingReminder.SoundKey,
                    pendingReminder.BasePrayerTime == default ? pendingReminder.NotifyTime : pendingReminder.BasePrayerTime,
                    pendingReminder.NotifyTime)
                : AdhanNotificationPayload.BuildPlay(pendingReminder.Prayer, pendingReminder.SoundKey),
            Schedule = new NotificationRequestSchedule {
                NotifyTime = pendingReminder.NotifyTime,
                NotifyRepeatInterval = null
#if ANDROID
                ,
                Android = new AndroidScheduleOptions {
                    AlarmType = AndroidAlarmType.RtcWakeup
                }
#endif
            }
        };

#if ANDROID
        request.Android = new AndroidOptions {
            Priority = pendingReminder.OpenAlarmScreen ? AndroidPriority.Max : AndroidPriority.Default,
            ChannelId = LocalNotificationScheduler.PrayerRuntimeMediaChannelId,
            VibrationPattern = Array.Empty<long>(),
            VisibilityType = pendingReminder.OpenAlarmScreen ? AndroidVisibilityType.Public : AndroidVisibilityType.Private,
            LaunchApp = pendingReminder.OpenAlarmScreen ? new AndroidLaunch { InHighPriority = true } : null,
            LaunchAppWhenTapped = pendingReminder.OpenAlarmScreen
        };
#endif

        await LocalNotificationCenter.Current.Show(request).ConfigureAwait(false);
    }

    private static bool ResolveDeferredNotificationSilent() {
#if ANDROID
        return false;
#else
        return true;
#endif
    }

    private void ClearPendingDeferredReminder() {
        try {
            var settings = _settingsService.Load();
            if (settings.Notifications.PendingDeferredReminder == null) {
                return;
            }

            var updated = CloneSettingsWithPendingReminder(settings, null);
            _settingsService.Save(updated);
#if ANDROID
            AndroidAdhanAlarmScheduler.Cancel(DeferredAdhanNotificationId);
#endif
        } catch (Exception ex) {
            _logger.LogException(ex, "AdhanPlaybackService.ClearPendingDeferredReminder");
        }
    }

    private void ClearExpiredPendingDeferredReminder() {
        try {
            var settings = _settingsService.Load();
            if (settings.Notifications.PendingDeferredReminder?.NotifyTime > DateTime.Now) {
                return;
            }

            if (settings.Notifications.PendingDeferredReminder == null) {
                return;
            }

            var updated = CloneSettingsWithPendingReminder(settings, null);
            _settingsService.Save(updated);
#if ANDROID
            AndroidAdhanAlarmScheduler.Cancel(DeferredAdhanNotificationId);
#endif
        } catch (Exception ex) {
            _logger.LogException(ex, "AdhanPlaybackService.ClearExpiredPendingDeferredReminder");
        }
    }

    private async Task RescheduleNotificationsAsync(AppSettings settings) {
        var normalizedSettings = settings;
        if (normalizedSettings.Notifications.PendingDeferredReminder?.NotifyTime <= DateTime.Now) {
            normalizedSettings = CloneSettingsWithPendingReminder(normalizedSettings, null);
            _settingsService.Save(normalizedSettings);
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var currentDate = DateTime.Today;
        var currentMonth = await _prayerTimesService
            .GetMonthAsync(normalizedSettings, currentDate.Year, currentDate.Month, CancellationToken.None)
            .ConfigureAwait(false);

        var daysToSchedule = currentMonth.Days
            .Where(item => item.Date >= today)
            .OrderBy(item => item.Date)
            .ToList();

        if (daysToSchedule.Count < 30) {
            var nextMonthDate = currentDate.AddMonths(1);
            var nextMonth = await _prayerTimesService
                .GetMonthAsync(normalizedSettings, nextMonthDate.Year, nextMonthDate.Month, CancellationToken.None)
                .ConfigureAwait(false);

            foreach (var day in nextMonth.Days.Where(item => item.Date >= today).OrderBy(item => item.Date)) {
                daysToSchedule.Add(day);
            }
        }

        var finalDays = daysToSchedule
            .GroupBy(item => item.Date)
            .Select(group => group.First())
            .OrderBy(item => item.Date)
            .Take(45)
            .ToList();

        if (finalDays.Count == 0) {
            return;
        }

        await ResolveLocalNotificationScheduler()
            .ScheduleAsync(finalDays, normalizedSettings, CancellationToken.None, requestPermissions: false)
            .ConfigureAwait(false);
    }

    private ILocalNotificationScheduler ResolveLocalNotificationScheduler() {
        return _localNotificationScheduler ??= _serviceProvider.GetRequiredService<ILocalNotificationScheduler>();
    }

    private static AppSettings CloneSettingsWithPendingReminder(AppSettings settings, DeferredAdhanReminder? pendingReminder) {
        return new AppSettings {
            Location = settings.Location,
            Method = settings.Method,
            Madhhab = settings.Madhhab,
            HighLatitudeRule = settings.HighLatitudeRule,
            SunAngles = settings.SunAngles,
            Offsets = settings.Offsets,
            FastingOffsets = settings.FastingOffsets,
            FastingReminders = settings.FastingReminders,
            Notifications = new NotificationSettings {
                EnableAdhan = settings.Notifications.EnableAdhan,
                MobilePrimaryAdhanType = settings.Notifications.MobilePrimaryAdhanType,
                EnableVibration = settings.Notifications.EnableVibration,
                HideOnCloseOnWindows = settings.Notifications.HideOnCloseOnWindows,
                RunBackgroundServiceOnWindows = settings.Notifications.RunBackgroundServiceOnWindows,
                MinutesBefore = settings.Notifications.MinutesBefore,
                AdhanVolume = settings.Notifications.AdhanVolume,
                SoundKey = settings.Notifications.SoundKey,
                CustomSounds = settings.Notifications.CustomSounds?.ToList() ?? new List<CustomAdhanSound>(),
                PrayerOverrides = settings.Notifications.PrayerOverrides?.ToList() ?? new List<AdhanPrayerOverride>(),
                VibrationStrength = settings.Notifications.VibrationStrength,
                VibrationPattern = settings.Notifications.VibrationPattern,
                ReminderScope = settings.Notifications.ReminderScope,
                ReminderPrayer = settings.Notifications.ReminderPrayer,
                ReminderItems = settings.Notifications.ReminderItems?.ToList() ?? new List<AdhanReminderItem>(),
                ReminderOffsetsMinutes = settings.Notifications.ReminderOffsetsMinutes?.ToList() ?? new List<int>(),
                PendingDeferredReminder = pendingReminder
            },
            AlarmReminders = settings.AlarmReminders,
            Qibla = settings.Qibla,
            ClockFormat = settings.ClockFormat,
            TextScale = settings.TextScale,
            Tasbih = settings.Tasbih,
            Language = settings.Language,
            LanguageSelected = settings.LanguageSelected,
            ThemeMode = settings.ThemeMode,
            AccentIndex = settings.AccentIndex,
            OnboardingCompleted = settings.OnboardingCompleted
        };
    }

    private static string FormatRemaining(TimeSpan remaining) {
        if (remaining < TimeSpan.Zero) {
            remaining = TimeSpan.Zero;
        }

        var totalHours = (int)Math.Floor(remaining.TotalHours);
        return $"{totalHours:00}:{remaining.Minutes:00}";
    }

    private static string FormatDelayOffset(TimeSpan offset) {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var absolute = offset < TimeSpan.Zero ? offset.Negate() : offset;
        var totalHours = (int)Math.Floor(absolute.TotalHours);
        return $"{sign}{totalHours}:{absolute.Minutes:00}";
    }

    private readonly record struct SnoozeWindow(int MaxDelayMinutes, PrayerId NextPrayerId, DateTime NextPrayerTime);

    private void StartCore(AdhanPlaybackSource source, double volume) {
        var normalizedVolume = NormalizeVolume(volume);
#if ANDROID
        var hasAudioFocus = TryAcquireAndroidAudioFocus();
        if (!hasAudioFocus) {
            _logger.LogEvent("AdhanAudioFocus", "initial=denied;action=play_without_focus");
        }

        var player = new Android.Media.MediaPlayer();
        try {
            var attributeBuilder = new Android.Media.AudioAttributes.Builder();
            attributeBuilder.SetUsage(Android.Media.AudioUsageKind.Alarm);
            attributeBuilder.SetContentType(Android.Media.AudioContentType.Music);
            var attributes = attributeBuilder.Build();
            if (attributes != null) {
                player.SetAudioAttributes(attributes);
            }

            if (source.IsPackageAsset) {
                var context = Android.App.Application.Context;
                if (context == null) {
                    ReleaseAndroidAudioFocus();
                    return;
                }

                var assets = context.Assets;
                if (assets == null) {
                    ReleaseAndroidAudioFocus();
                    return;
                }

                using var asset = assets.OpenFd(NormalizeAssetPath(source.Path));
                player.SetDataSource(asset.FileDescriptor!, asset.StartOffset, asset.Length);
            } else {
                player.SetDataSource(source.Path);
            }

            player.Completion += OnAndroidCompletion;
            player.SetVolume(normalizedVolume, normalizedVolume);
            player.Prepare();
            player.Start();
            _androidPlayer = player;
            _androidPausedForTransientLoss = false;
        } catch {
            player.Completion -= OnAndroidCompletion;
            player.Release();
            player.Dispose();
            ReleaseAndroidAudioFocus();
            throw;
        }
#elif WINDOWS
        var player = new Windows.Media.Playback.MediaPlayer();
        player.MediaEnded += OnWindowsMediaEnded;
        player.MediaFailed += OnWindowsMediaFailed;
        player.Volume = normalizedVolume;
        player.Source = MediaSource.CreateFromUri(BuildWindowsUri(source));
        player.Play();
        _windowsPlayer = player;
#elif IOS || MACCATALYST
        if (!TryActivateAppleAudioSession()) {
            return;
        }

        var filePath = ResolveApplePath(source);
        if (string.IsNullOrWhiteSpace(filePath)) {
            DeactivateAppleAudioSession();
            return;
        }

        var player = AVAudioPlayer.FromUrl(NSUrl.FromFilename(filePath));
        if (player == null) {
            DeactivateAppleAudioSession();
            return;
        }

        player.FinishedPlaying += OnAppleFinishedPlaying;
        player.Volume = normalizedVolume;
        player.PrepareToPlay();
        if (!player.Play()) {
            player.FinishedPlaying -= OnAppleFinishedPlaying;
            player.Dispose();
            DeactivateAppleAudioSession();
            return;
        }
        _applePlayer = player;
#endif
    }

    private void StopCore() {
#if ANDROID
        if (_androidPlayer != null) {
            try {
                if (_androidPlayer.IsPlaying) {
                    _androidPlayer.Stop();
                }
            } catch {
            }
            _androidPlayer.Completion -= OnAndroidCompletion;
            _androidPlayer.Release();
            _androidPlayer.Dispose();
            _androidPlayer = null;
        }

        ReleaseAndroidAudioFocus();
#endif

#if WINDOWS
        if (_windowsPlayer != null) {
            _windowsPlayer.MediaEnded -= OnWindowsMediaEnded;
            _windowsPlayer.MediaFailed -= OnWindowsMediaFailed;
            _windowsPlayer.Pause();
            _windowsPlayer.Source = null;
            _windowsPlayer.Dispose();
            _windowsPlayer = null;
        }

        _windowsNotificationMonitorCts?.Cancel();
        _windowsNotificationMonitorCts?.Dispose();
        _windowsNotificationMonitorCts = null;
#endif

#if IOS || MACCATALYST
        if (_applePlayer != null) {
            _applePlayer.FinishedPlaying -= OnAppleFinishedPlaying;
            _applePlayer.Stop();
            _applePlayer.Dispose();
            _applePlayer = null;
        }

        DeactivateAppleAudioSession();
#endif

        _activeScheduledPayload = null;
        _activeAlarmPayload = null;
        _activeAlarmPresentation = null;
    }

#if ANDROID
    private static string NormalizeAssetPath(string path) => path.Replace('\\', '/');

    private bool TryAcquireAndroidAudioFocus() {
        var context = Android.App.Application.Context;
        if (context == null) {
            return false;
        }

        if (context.GetSystemService(Android.Content.Context.AudioService) is not Android.Media.AudioManager audioManager) {
            return false;
        }

        _androidAudioManager = audioManager;
        _androidAudioFocusChangeListener ??= new AndroidAudioFocusChangeListener(OnAndroidAudioFocusChanged);

        if (OperatingSystem.IsAndroidVersionAtLeast(26)) {
            var attributeBuilder = new Android.Media.AudioAttributes.Builder();
            attributeBuilder.SetUsage(Android.Media.AudioUsageKind.Alarm);
            attributeBuilder.SetContentType(Android.Media.AudioContentType.Music);
            var attributes = attributeBuilder.Build();

            var focusRequestBuilder = new Android.Media.AudioFocusRequestClass.Builder(Android.Media.AudioFocus.Gain);
            if (attributes != null) {
                focusRequestBuilder.SetAudioAttributes(attributes);
            }
            focusRequestBuilder.SetWillPauseWhenDucked(false);
            focusRequestBuilder.SetOnAudioFocusChangeListener(_androidAudioFocusChangeListener);
            _androidAudioFocusRequest = focusRequestBuilder.Build();
            if (_androidAudioFocusRequest == null) {
                _androidAudioManager = null;
                return false;
            }

            var focusResult = audioManager.RequestAudioFocus(_androidAudioFocusRequest);
            if (focusResult != Android.Media.AudioFocusRequest.Granted) {
                _androidAudioFocusRequest = null;
                _androidAudioManager = null;
                return false;
            }

            return true;
        }

#pragma warning disable CS0618
        var legacyResult = audioManager.RequestAudioFocus(_androidAudioFocusChangeListener, Android.Media.Stream.Alarm, Android.Media.AudioFocus.Gain);
#pragma warning restore CS0618
        if (legacyResult != Android.Media.AudioFocusRequest.Granted) {
            _androidAudioManager = null;
            return false;
        }

        return true;
    }

    private void ReleaseAndroidAudioFocus() {
        if (_androidAudioManager == null) {
            _androidPausedForTransientLoss = false;
            _androidAudioFocusRequest = null;
            return;
        }

        try {
            if (OperatingSystem.IsAndroidVersionAtLeast(26)) {
                if (_androidAudioFocusRequest != null) {
                    _androidAudioManager.AbandonAudioFocusRequest(_androidAudioFocusRequest);
                }
            } else if (_androidAudioFocusChangeListener != null) {
#pragma warning disable CS0618
                _androidAudioManager.AbandonAudioFocus(_androidAudioFocusChangeListener);
#pragma warning restore CS0618
            }
        } catch {
        }

        _androidPausedForTransientLoss = false;
        _androidAudioFocusRequest = null;
        _androidAudioManager = null;
    }

    private void OnAndroidAudioFocusChanged(Android.Media.AudioFocus focusChange) {
        switch (focusChange) {
            case Android.Media.AudioFocus.Loss:
                _logger.LogEvent("AdhanAudioFocus", "change=Loss;action=recover_and_continue");
                _ = RecoverAfterAndroidFocusLossAsync("Loss");
                break;
            case Android.Media.AudioFocus.LossTransient:
                _logger.LogEvent("AdhanAudioFocus", "change=LossTransient;action=pause");
                _ = PauseForAndroidTransientFocusLossAsync();
                break;
            case Android.Media.AudioFocus.Gain:
                _logger.LogEvent("AdhanAudioFocus", "change=Gain;action=resume");
                _ = ResumeAfterAndroidFocusGainAsync();
                break;
            case Android.Media.AudioFocus.LossTransientCanDuck:
                _logger.LogEvent("AdhanAudioFocus", "change=LossTransientCanDuck;action=ignore");
                break;
            default:
                _logger.LogEvent("AdhanAudioFocus", $"change={focusChange};action=ignore");
                break;
        }
    }

    private async Task RecoverAfterAndroidFocusLossAsync(string reason) {
        TryReacquireAndroidAudioFocus();
        await _gate.WaitAsync().ConfigureAwait(false);
        try {
            if (_androidPlayer == null) {
                return;
            }

            try {
                if (!_androidPlayer.IsPlaying) {
                    _androidPlayer.Start();
                }
                _androidPausedForTransientLoss = false;
            } catch (Exception ex) {
                _logger.LogException(ex, $"AdhanPlaybackService.RecoverAfterAndroidFocusLossAsync:{reason}");
            }
        } finally {
            _gate.Release();
        }
    }

    private void TryReacquireAndroidAudioFocus() {
        if (_androidAudioManager == null || _androidAudioFocusChangeListener == null) {
            return;
        }

        try {
            if (OperatingSystem.IsAndroidVersionAtLeast(26)) {
                if (_androidAudioFocusRequest != null) {
                    _androidAudioManager.RequestAudioFocus(_androidAudioFocusRequest);
                }
                return;
            }

#pragma warning disable CS0618
            _androidAudioManager.RequestAudioFocus(_androidAudioFocusChangeListener, Android.Media.Stream.Alarm, Android.Media.AudioFocus.Gain);
#pragma warning restore CS0618
        } catch (Exception ex) {
            _logger.LogException(ex, "AdhanPlaybackService.TryReacquireAndroidAudioFocus");
        }
    }

    private async Task PauseForAndroidTransientFocusLossAsync() {
        await _gate.WaitAsync().ConfigureAwait(false);
        try {
            if (_androidPlayer == null) {
                return;
            }

            try {
                if (_androidPlayer.IsPlaying) {
                    _androidPlayer.Pause();
                    _androidPausedForTransientLoss = true;
                }
            } catch {
            }
        } finally {
            _gate.Release();
        }
    }

    private async Task ResumeAfterAndroidFocusGainAsync() {
        TryReacquireAndroidAudioFocus();
        await _gate.WaitAsync().ConfigureAwait(false);
        try {
            if (_androidPlayer == null || !_androidPausedForTransientLoss) {
                return;
            }

            try {
                _androidPlayer.Start();
                _androidPausedForTransientLoss = false;
            } catch {
            }
        } finally {
            _gate.Release();
        }
    }

    private void OnAndroidCompletion(object? sender, EventArgs e) {
        _ = StopAsync();
    }

    private sealed class AndroidAudioFocusChangeListener : Java.Lang.Object, Android.Media.AudioManager.IOnAudioFocusChangeListener {
        private readonly Action<Android.Media.AudioFocus> _onAudioFocusChange;

        public AndroidAudioFocusChangeListener(Action<Android.Media.AudioFocus> onAudioFocusChange) {
            _onAudioFocusChange = onAudioFocusChange;
        }

        public void OnAudioFocusChange(Android.Media.AudioFocus focusChange) {
            _onAudioFocusChange(focusChange);
        }
    }
#endif

#if WINDOWS
    private static Uri BuildWindowsUri(AdhanPlaybackSource source) {
        if (source.IsPackageAsset) {
            var relative = source.Path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(AppContext.BaseDirectory, relative);
            return new Uri(fullPath, UriKind.Absolute);
        }

        return new Uri(source.Path, UriKind.Absolute);
    }

    private void OnWindowsMediaEnded(Windows.Media.Playback.MediaPlayer sender, object args) {
        _ = StopAsync();
    }

    private void OnWindowsMediaFailed(Windows.Media.Playback.MediaPlayer sender, Windows.Media.Playback.MediaPlayerFailedEventArgs args) {
        _logger.LogException(new InvalidOperationException($"Windows media failed: {args.ErrorMessage}"), "AdhanPlaybackService.OnWindowsMediaFailed");
        _ = StopAsync();
    }

    private void StartWindowsNotificationMonitor() {
        _windowsNotificationMonitorCts?.Cancel();
        _windowsNotificationMonitorCts?.Dispose();
        _windowsNotificationMonitorCts = new CancellationTokenSource();
        var token = _windowsNotificationMonitorCts.Token;

        _ = Task.Run(async () => {
            try {
                await Task.Delay(1000, token).ConfigureAwait(false);
                while (!token.IsCancellationRequested) {
                    var notifications = await AppNotificationManager.Default.GetAllAsync();
                    var exists = notifications.Any(item =>
                        string.Equals(item.Tag, WindowsControlNotificationTag, StringComparison.Ordinal));
                    if (!exists) {
                        await StopAsync().ConfigureAwait(false);
                        return;
                    }

                    await Task.Delay(750, token).ConfigureAwait(false);
                }
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                _logger.LogException(ex, "AdhanPlaybackService.StartWindowsNotificationMonitor");
            }
        }, token);
    }
#endif

#if IOS || MACCATALYST
    private bool TryActivateAppleAudioSession() {
        try {
            var session = AVAudioSession.SharedInstance();
            session.SetCategory(AVAudioSessionCategory.Playback);
            session.SetActive(true);
            return true;
        } catch (Exception ex) {
            _logger.LogException(ex, "AdhanPlaybackService.TryActivateAppleAudioSession");
            return false;
        }
    }

    private void DeactivateAppleAudioSession() {
        try {
            var session = AVAudioSession.SharedInstance();
            try {
                session.SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation);
            } catch {
                session.SetActive(false);
            }
        } catch (Exception ex) {
            _logger.LogException(ex, "AdhanPlaybackService.DeactivateAppleAudioSession");
        }
    }

    private static string? ResolveApplePath(AdhanPlaybackSource source) {
        if (!source.IsPackageAsset) {
            return source.Path;
        }

        var normalized = source.Path.Replace('\\', '/');
        var folder = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
        var file = Path.GetFileNameWithoutExtension(normalized);
        var extension = Path.GetExtension(normalized).TrimStart('.');
        return NSBundle.MainBundle.PathForResource(file, extension, folder);
    }

    private void OnAppleFinishedPlaying(object? sender, AVStatusEventArgs e) {
        _ = StopAsync();
    }
#endif

    private static float NormalizeVolume(double volume) {
        if (double.IsNaN(volume) || double.IsInfinity(volume)) {
            return 1f;
        }

        return (float)Math.Clamp(volume, 0d, 1d);
    }
}

