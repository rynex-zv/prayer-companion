using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Services;

public sealed class WindowsAdhanAlarmService : IWindowsAdhanAlarmService {
    private readonly IAdhanPlaybackService _playbackService;
    private readonly IAppLogger _logger;
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;

    public WindowsAdhanAlarmService(IAdhanPlaybackService playbackService, IAppLogger logger) {
        _playbackService = playbackService;
        _logger = logger;
    }

    public void Schedule(DateTime when, AdhanNotificationPayload payload) {
        if (!OperatingSystem.IsWindows()) {
            return;
        }

        Clear();

        var delay = when - DateTime.Now;
        if (delay <= TimeSpan.Zero) {
            _ = Task.Run(async () => {
                try {
                    await _playbackService.PlayScheduledAsync(payload).ConfigureAwait(false);
                } catch (Exception ex) {
                    _logger.LogException(ex, "WindowsAdhanAlarmService.ScheduleImmediate");
                }
            });
            return;
        }

        var cts = new CancellationTokenSource();
        lock (_lock) {
            _cts = cts;
        }
        var token = cts.Token;

        _ = Task.Run(async () => {
            try {
                await Task.Delay(delay, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) {
                    return;
                }

                await _playbackService.PlayScheduledAsync(payload).ConfigureAwait(false);
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                _logger.LogException(ex, "WindowsAdhanAlarmService.Schedule");
            }
        }, token);
    }

    public void Clear() {
        lock (_lock) {
            if (_cts == null) {
                return;
            }

            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }
}
