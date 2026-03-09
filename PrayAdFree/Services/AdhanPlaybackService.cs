using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;
using Plugin.LocalNotification.EventArgs;
using Plugin.LocalNotification.WindowsOption;
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
    public const string WindowsStopActionToken = "stop_adhan";
    public const string WindowsControlNotificationSourceToken = "adhan_control";
    public const string WindowsControlNotificationTag = "adhan_playback_control";

    private readonly SettingsService _settingsService;
    private readonly IAppLogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;
    private bool _disposed;

#if ANDROID
    private Android.Media.MediaPlayer? _androidPlayer;
#endif
#if WINDOWS
    private Windows.Media.Playback.MediaPlayer? _windowsPlayer;
    private CancellationTokenSource? _windowsNotificationMonitorCts;
#endif
#if IOS || MACCATALYST
    private AVAudioPlayer? _applePlayer;
#endif

    public AdhanPlaybackService(SettingsService settingsService, IAppLogger logger) {
        _settingsService = settingsService;
        _logger = logger;
    }

    public void Initialize() {
        if (_initialized) {
            return;
        }

        LocalNotificationCenter.Current.NotificationReceived += OnNotificationReceived;
        LocalNotificationCenter.Current.NotificationActionTapped += OnNotificationActionTapped;
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
            StartCore(source, settings.Notifications.AdhanVolume);
            await ShowControlNotificationAsync(LocalizationManager.Translate("AdhanPreviewTitle")).ConfigureAwait(false);
            return true;
        } catch (Exception ex) {
            _logger.LogException(ex, "AdhanPlaybackService.PlayPreviewAsync");
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
#if ANDROID || IOS || MACCATALYST
        await Task.CompletedTask;
        return;
#else
        try {
            var request = e?.Request;
            if (request == null || !AdhanNotificationPayload.TryParse(request.ReturningData, out var payload)) {
                return;
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
                StartCore(source, settings.Notifications.AdhanVolume);
                var prayerName = LocalizationManager.TranslatePrayer(payload.Prayer);
                await ShowControlNotificationAsync(prayerName).ConfigureAwait(false);
            } finally {
                _gate.Release();
            }
        } catch (Exception ex) {
            _logger.LogException(ex, "AdhanPlaybackService.HandleNotificationReceivedAsync");
        }
#endif
    }

    private async Task HandleNotificationActionTappedAsync(NotificationActionEventArgs e) {
        try {
            if (e == null) {
                return;
            }

            if (e.ActionId != StopActionId &&
                e.ActionId != NotificationActionEventArgs.TapActionId &&
                e.ActionId != NotificationActionEventArgs.DismissedActionId &&
                !e.IsTapped &&
                !e.IsDismissed) {
                return;
            }

            await StopAsync().ConfigureAwait(false);
        } catch (Exception ex) {
            _logger.LogException(ex, "AdhanPlaybackService.HandleNotificationActionTappedAsync");
        }
    }

    private async Task ShowControlNotificationAsync(string prayerName) {
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
        var request = new NotificationRequest {
            NotificationId = ControlNotificationId,
            CategoryType = NotificationCategoryType.Service,
            Title = prayerName,
            Description = LocalizationManager.Translate("AdhanPlaybackStopHint"),
            Silent = true,
            ReturningData = "adhan_control",
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

    private void StartCore(AdhanPlaybackSource source, double volume) {
        var normalizedVolume = NormalizeVolume(volume);
#if ANDROID
        var player = new Android.Media.MediaPlayer();
        if (source.IsPackageAsset) {
            var context = Android.App.Application.Context;
            if (context == null) {
                return;
            }

            var assets = context.Assets;
            if (assets == null) {
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
#elif WINDOWS
        var player = new Windows.Media.Playback.MediaPlayer();
        player.MediaEnded += OnWindowsMediaEnded;
        player.MediaFailed += OnWindowsMediaFailed;
        player.Volume = normalizedVolume;
        player.Source = MediaSource.CreateFromUri(BuildWindowsUri(source));
        player.Play();
        _windowsPlayer = player;
#elif IOS || MACCATALYST
        var filePath = ResolveApplePath(source);
        if (string.IsNullOrWhiteSpace(filePath)) {
            return;
        }

        var player = AVAudioPlayer.FromUrl(NSUrl.FromFilename(filePath));
        if (player == null) {
            return;
        }

        player.FinishedPlaying += OnAppleFinishedPlaying;
        player.Volume = normalizedVolume;
        player.PrepareToPlay();
        player.Play();
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
#endif
    }

#if ANDROID
    private static string NormalizeAssetPath(string path) => path.Replace('\\', '/');

    private void OnAndroidCompletion(object? sender, EventArgs e) {
        _ = StopAsync();
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
