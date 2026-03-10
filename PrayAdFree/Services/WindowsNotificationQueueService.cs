using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
#if WINDOWS
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
#endif

namespace Pray_Ad_Free.Services;

public sealed class WindowsNotificationQueueService : IWindowsNotificationQueueService {
    private readonly IAdhanPlaybackService _adhanPlaybackService;
    private readonly IAppLogger _logger;
    private readonly object _lock = new();
    private List<PlannedNotification> _queue = new();
    private CancellationTokenSource? _armCts;

    public WindowsNotificationQueueService(IAdhanPlaybackService adhanPlaybackService, IAppLogger logger) {
        _adhanPlaybackService = adhanPlaybackService;
        _logger = logger;
    }

    public void ReplaceSchedule(IReadOnlyList<PlannedNotification> notifications) {
        if (!OperatingSystem.IsWindows()) {
            return;
        }

        var normalized = NotificationScheduleSelector.Normalize(notifications, DateTime.Now).ToList();

        lock (_lock) {
            _queue = normalized;
            ArmNextLocked();
        }
    }

    public void Clear() {
        lock (_lock) {
            _queue.Clear();
            _armCts?.Cancel();
            _armCts?.Dispose();
            _armCts = null;
        }
    }

    private void ArmNextLocked() {
        _armCts?.Cancel();
        _armCts?.Dispose();
        _armCts = null;

        if (_queue.Count == 0) {
            return;
        }

        var next = _queue[0];
        var delay = next.NotifyTime - DateTime.Now;
        if (delay <= TimeSpan.Zero) {
            _ = Task.Run(ProcessDueAsync);
            return;
        }

        var cts = new CancellationTokenSource();
        _armCts = cts;
        var token = cts.Token;
        _ = Task.Run(async () => {
            try {
                await Task.Delay(delay, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) {
                    return;
                }

                await ProcessDueAsync().ConfigureAwait(false);
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                _logger.LogException(ex, "WindowsNotificationQueueService.ArmNext");
            }
        }, token);
    }

    private async Task ProcessDueAsync() {
        while (true) {
            PlannedNotification? current;
            lock (_lock) {
                if (_queue.Count == 0) {
                    return;
                }

                if (_queue[0].NotifyTime > DateTime.Now.AddMilliseconds(200)) {
                    ArmNextLocked();
                    return;
                }

                current = _queue[0];
                _queue.RemoveAt(0);
                ArmNextLocked();
            }

            try {
                ShowToast(current);

                if (current.PlayAdhan && AdhanNotificationPayload.TryParse(current.ReturningData, out var payload)) {
                    await _adhanPlaybackService.PlayScheduledAsync(payload).ConfigureAwait(false);
                }
            } catch (Exception ex) {
                _logger.LogException(ex, "WindowsNotificationQueueService.ProcessDue");
            }
        }
    }

    private void ShowToast(PlannedNotification notification) {
#if WINDOWS
        if (string.IsNullOrWhiteSpace(notification.Title) || string.IsNullOrWhiteSpace(notification.Description)) {
            return;
        }

        var appNotification = new AppNotificationBuilder()
            .AddArgument("source", "prayer_schedule")
            .AddArgument("id", notification.NotificationId.ToString())
            .AddText(notification.Title)
            .AddText(notification.Description)
            .BuildNotification();

        appNotification.Tag = $"prayer_{notification.NotificationId}_{notification.NotifyTime:yyyyMMddHHmmss}";
        AppNotificationManager.Default.Show(appNotification);
#else
        _ = notification;
#endif
    }
}
