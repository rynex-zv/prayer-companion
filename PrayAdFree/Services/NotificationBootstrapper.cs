using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Plugin.LocalNotification;
#if ANDROID
using Android.OS;
using Plugin.LocalNotification.AndroidOption;
#endif
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace Pray_Ad_Free.Services;

public sealed class NotificationBootstrapper {
    private readonly SettingsService _settingsService;
    private readonly PrayerDataService _dataService;
    private readonly IAppLogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _permissionGate = new(1, 1);
    private DateTime _lastRunUtc = DateTime.MinValue;
    private bool _permissionRequestedThisSession;
    private bool _locationPermissionRequestedThisSession;

    public NotificationBootstrapper(SettingsService settingsService, PrayerDataService dataService, IAppLogger logger) {
        _settingsService = settingsService;
        _dataService = dataService;
        _logger = logger;
    }

    public async Task EnsureScheduledAsync(string reason, bool requestPermissions) {
        var settings = _settingsService.Load();

        if (requestPermissions) {
            await EnsureNotificationPermissionAsync(reason).ConfigureAwait(false);
            await EnsureLocationPermissionAsync(reason, settings.Location.Mode == LocationMode.Gps).ConfigureAwait(false);
        }

        if (DateTime.UtcNow - _lastRunUtc < TimeSpan.FromMinutes(5)) {
            return;
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try {
            if (DateTime.UtcNow - _lastRunUtc < TimeSpan.FromMinutes(5)) {
                return;
            }

            var month = await _dataService.GetMonthAsync(settings, DateTime.Today, CancellationToken.None).ConfigureAwait(false);
            await _dataService.ScheduleNotificationsAsync(settings, month, CancellationToken.None, requestPermissions: false).ConfigureAwait(false);
            _lastRunUtc = DateTime.UtcNow;
            _logger.LogEvent("NotificationSchedule", $"{reason}|{_lastRunUtc:O}");
        } catch (Exception ex) {
            _logger.LogException(ex, "NotificationBootstrapper.Schedule");
        } finally {
            _gate.Release();
        }
    }

    private async Task EnsureNotificationPermissionAsync(string reason) {
        if (OperatingSystem.IsWindows()) {
            return;
        }

        if (_permissionRequestedThisSession) {
            return;
        }

        await _permissionGate.WaitAsync().ConfigureAwait(false);
        try {
            if (_permissionRequestedThisSession) {
                return;
            }

#if ANDROID
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu) {
                var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>().ConfigureAwait(false);
                _logger.LogEvent("NotificationPermission", $"android_status_before:{status}|{reason}");
                if (status != PermissionStatus.Granted) {
                    var requestStatus = await MainThread.InvokeOnMainThreadAsync(
                        Permissions.RequestAsync<Permissions.PostNotifications>
                    ).ConfigureAwait(false);
                    _logger.LogEvent("NotificationPermission", $"android_status_after:{requestStatus}|{reason}");
                }
            }
#endif

            var permission = new NotificationPermission { AskPermission = true };
#if ANDROID
            permission.Android = new AndroidNotificationPermission { RequestPermissionToScheduleExactAlarm = true };
#endif
            var pluginResult = await MainThread.InvokeOnMainThreadAsync(
                () => LocalNotificationCenter.Current.RequestNotificationPermission(permission)
            ).ConfigureAwait(false);

            _permissionRequestedThisSession = true;
            _logger.LogEvent("NotificationPermission", $"requested:{reason}|plugin={pluginResult}");
        } catch (Exception ex) {
            _logger.LogException(ex, "NotificationBootstrapper.RequestPermission");
        } finally {
            _permissionGate.Release();
        }
    }

    private async Task EnsureLocationPermissionAsync(string reason, bool shouldRequest) {
        if (!shouldRequest || OperatingSystem.IsWindows()) {
            return;
        }

        if (_locationPermissionRequestedThisSession) {
            return;
        }

        await _permissionGate.WaitAsync().ConfigureAwait(false);
        try {
            if (_locationPermissionRequestedThisSession) {
                return;
            }

            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>().ConfigureAwait(false);
            _logger.LogEvent("LocationPermission", $"status_before:{status}|{reason}");
            if (status != PermissionStatus.Granted) {
                var requestStatus = await MainThread.InvokeOnMainThreadAsync(
                    Permissions.RequestAsync<Permissions.LocationWhenInUse>
                ).ConfigureAwait(false);
                _logger.LogEvent("LocationPermission", $"status_after:{requestStatus}|{reason}");
            }

            _locationPermissionRequestedThisSession = true;
        } catch (Exception ex) {
            _logger.LogException(ex, "NotificationBootstrapper.RequestLocationPermission");
        } finally {
            _permissionGate.Release();
        }
    }
}
