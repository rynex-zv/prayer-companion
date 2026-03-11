using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;
using Plugin.LocalNotification.EventArgs;
using Plugin.LocalNotification.WindowsOption;
using Microsoft.Extensions.DependencyInjection;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
#if ANDROID
using Android.App;
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
    public const string ControlReturningData = "adhan_control";
    private const int MinSnoozeMinutes = 4;
    private const int BufferBeforeNextPrayerMinutes = 30;
    public const string WindowsStopActionToken = WindowsNotificationActionParser.StopActionToken;
    public const string WindowsControlNotificationSourceToken = WindowsNotificationActionParser.ControlSourceToken;
    public const string WindowsControlNotificationTag = WindowsNotificationActionParser.ControlNotificationTag;

    private readonly SettingsService _settingsService;
    private readonly PrayerTimesService _prayerTimesService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAppLogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ILocalNotificationScheduler? _localNotificationScheduler;
    private AdhanNotificationPayload? _activeScheduledPayload;
    private bool _initialized;
    private bool _disposed;

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
        IServiceProvider serviceProvider,
        IAppLogger logger) {
        _settingsService = settingsService;
        _prayerTimesService = prayerTimesService;
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
            StartCore(source, settings.Notifications.AdhanVolume);
            await ShowControlNotificationAsync(LocalizationManager.Translate("AdhanPreviewTitle"), includeSnoozeActions: false).ConfigureAwait(false);
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

    public async Task StopAsync() {
        await _gate.WaitAsync().ConfigureAwait(false);
        try {
            StopCore();
#if WINDOWS
            await AppNotificationManager.Default.RemoveByTagAsync(WindowsControlNotificationTag);
#else
            LocalNotificationCenter.Current.Cancel(ControlNotificationId);
#endif
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
            if (request == null || !AdhanNotificationPayload.TryParse(request.ReturningData, out var payload)) {
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

            await _gate.WaitAsync().ConfigureAwait(false);
            try {
                StopCore();
                _activeScheduledPayload = payload;
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

    private async Task HandleNotificationActionTappedAsync(NotificationActionEventArgs e) {
        try {
            if (e == null) {
                return;
            }

            var isAdhanNotification = IsAdhanNotificationRequest(e.Request);
            if (e.ActionId == Snooze10ActionId) {
                if (!isAdhanNotification) {
                    return;
                }

                await HandleSnooze10ActionAsync().ConfigureAwait(false);
                return;
            }

            if (e.ActionId == OpenCustomSnoozeActionId) {
                if (!isAdhanNotification) {
                    return;
                }

                await HandleOpenCustomSnoozeActionAsync().ConfigureAwait(false);
                return;
            }

            var isStopAction = e.ActionId == StopActionId;
            var isTapAction = e.ActionId == NotificationActionEventArgs.TapActionId || e.IsTapped;
            var isDismissAction = e.ActionId == NotificationActionEventArgs.DismissedActionId || e.IsDismissed;

            if (!isStopAction && !isTapAction && !isDismissAction) {
                return;
            }

            if (!isStopAction && !isAdhanNotification) {
                return;
            }

            await StopAsync().ConfigureAwait(false);
        } catch (Exception ex) {
            _logger.LogException(ex, "AdhanPlaybackService.HandleNotificationActionTappedAsync");
        }
    }

    private async Task HandleSnooze10ActionAsync() {
        if (!_activeScheduledPayload.HasValue) {
            return;
        }

        var payload = _activeScheduledPayload.Value;
        await StopAsync().ConfigureAwait(false);
        await ScheduleDeferredReminderAsync(payload, 10).ConfigureAwait(false);
    }

    private async Task HandleOpenCustomSnoozeActionAsync() {
        if (!_activeScheduledPayload.HasValue) {
            return;
        }

        var payload = _activeScheduledPayload.Value;
        await StopAsync().ConfigureAwait(false);

        var window = await TryBuildSnoozeWindowAsync(DateTime.Now).ConfigureAwait(false);
        if (window == null || window.Value.MaxDelayMinutes < MinSnoozeMinutes) {
            return;
        }

        var model = new Pages.AdhanSnoozePageModel(
            LocalizationManager.TranslatePrayer(payload.Prayer),
            LocalizationManager.TranslatePrayer(window.Value.NextPrayerId),
            FormatRemaining(window.Value.NextPrayerTime - DateTime.Now),
            MinSnoozeMinutes,
            window.Value.MaxDelayMinutes,
            Math.Clamp(10, MinSnoozeMinutes, window.Value.MaxDelayMinutes));

        await MainThread.InvokeOnMainThreadAsync(async () => {
            try {
                var page = new Pages.AdhanSnoozePage(model, async minutes => await ScheduleDeferredReminderAsync(payload, minutes).ConfigureAwait(false));
                var navigation = Shell.Current?.Navigation ?? Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
                if (navigation != null) {
                    await navigation.PushModalAsync(page);
                }
            } catch (Exception ex) {
                _logger.LogException(ex, "AdhanPlaybackService.OpenCustomSnoozePage");
            }
        });
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

        return AdhanNotificationPayload.TryParse(request.ReturningData, out _);
    }

    private async Task ShowControlNotificationAsync(string prayerName, bool includeSnoozeActions) {
#if WINDOWS
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
        var categoryType = NotificationCategoryType.Service;
        if (includeSnoozeActions) {
            var window = await TryBuildSnoozeWindowAsync(DateTime.Now).ConfigureAwait(false);
            if (window?.MaxDelayMinutes >= 10) {
                categoryType = NotificationCategoryType.Reminder;
            } else if (window?.MaxDelayMinutes >= MinSnoozeMinutes) {
                categoryType = NotificationCategoryType.Alarm;
            }
        }

        var request = new NotificationRequest {
            NotificationId = ControlNotificationId,
            CategoryType = categoryType,
            Title = prayerName,
            Description = LocalizationManager.Translate("AdhanPlaybackStopHint"),
            Silent = true,
            ReturningData = ControlReturningData,
            Android = new AndroidOptions {
                ChannelId = "adhan_playback_control",
                Priority = AndroidPriority.High,
                Ongoing = true,
                AutoCancel = false,
                LaunchAppWhenTapped = false
            },
            Windows = new WindowsOptions {
                LaunchAppWhenTapped = false
            }
        };

        await LocalNotificationCenter.Current.Show(request).ConfigureAwait(false);
#endif
    }

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

    private async Task<bool> ScheduleDeferredReminderAsync(AdhanNotificationPayload payload, int delayMinutes) {
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
                Prayer = payload.Prayer,
                SoundKey = effectiveSoundKey
            };

            var updated = CloneSettingsWithPendingReminder(settings, pendingReminder);
            _settingsService.Save(updated);
            await RescheduleNotificationsAsync(updated).ConfigureAwait(false);
            return true;
        } catch (Exception ex) {
            _logger.LogException(ex, "AdhanPlaybackService.ScheduleDeferredReminderAsync");
            return false;
        }
    }

    private void ClearPendingDeferredReminder() {
        try {
            var settings = _settingsService.Load();
            if (settings.Notifications.PendingDeferredReminder == null) {
                return;
            }

            var updated = CloneSettingsWithPendingReminder(settings, null);
            _settingsService.Save(updated);
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
            Offsets = settings.Offsets,
            FastingOffsets = settings.FastingOffsets,
            FastingReminders = settings.FastingReminders,
            Notifications = new NotificationSettings {
                EnableAdhan = settings.Notifications.EnableAdhan,
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
            Qibla = settings.Qibla,
            ClockFormat = settings.ClockFormat,
            TextScale = settings.TextScale,
            Tasbih = settings.Tasbih,
            Language = settings.Language,
            LanguageSelected = settings.LanguageSelected,
            ThemeMode = settings.ThemeMode,
            ThemeVariant = settings.ThemeVariant,
            AccentIndex = settings.AccentIndex
        };
    }

    private static string FormatRemaining(TimeSpan remaining) {
        if (remaining < TimeSpan.Zero) {
            remaining = TimeSpan.Zero;
        }

        var totalHours = (int)Math.Floor(remaining.TotalHours);
        return $"{totalHours:00}:{remaining.Minutes:00}";
    }

    private readonly record struct SnoozeWindow(int MaxDelayMinutes, PrayerId NextPrayerId, DateTime NextPrayerTime);

    private void StartCore(AdhanPlaybackSource source, double volume) {
        var normalizedVolume = NormalizeVolume(volume);
#if ANDROID
        if (!TryAcquireAndroidAudioFocus()) {
            return;
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

            var focusRequestBuilder = new Android.Media.AudioFocusRequestClass.Builder(Android.Media.AudioFocus.GainTransient);
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
        var legacyResult = audioManager.RequestAudioFocus(_androidAudioFocusChangeListener, Android.Media.Stream.Alarm, Android.Media.AudioFocus.GainTransient);
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
                _ = StopAsync();
                break;
            case Android.Media.AudioFocus.LossTransient:
                _ = PauseForAndroidTransientFocusLossAsync();
                break;
            case Android.Media.AudioFocus.Gain:
                _ = ResumeAfterAndroidFocusGainAsync();
                break;
            case Android.Media.AudioFocus.LossTransientCanDuck:
            default:
                break;
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

