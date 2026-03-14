using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;
using Pray_Ad_Free.Models;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.ViewModels;

public sealed class AppPermissionsViewModel : ViewModelBase {
    private readonly AppPermissionCenterService _permissionCenterService;
    private readonly AndroidAlarmCapabilityService _alarmCapabilityService;
    private readonly IAppLogger _logger;
    private bool _isBusy;
    private string _alarmModeTitle = string.Empty;
    private string _alarmModeStatus = string.Empty;
    private string _alarmModeDescription = string.Empty;

    public AppPermissionsViewModel(
        AppPermissionCenterService permissionCenterService,
        AndroidAlarmCapabilityService alarmCapabilityService,
        IAppLogger logger) {
        _permissionCenterService = permissionCenterService;
        _alarmCapabilityService = alarmCapabilityService;
        _logger = logger;
        Items = new ObservableCollection<AppPermissionItemViewModel> {
            new(AppPermissionKind.Notifications),
            new(AppPermissionKind.FullScreenIntents),
            new(AppPermissionKind.DisplayOverApps),
            new(AppPermissionKind.ExactAlarms),
            new(AppPermissionKind.Location)
        };
        ResolvePermissionCommand = new Command<AppPermissionItemViewModel>(async item => await ResolvePermissionAsync(item));
        RefreshCommand = new Command(async () => await RefreshAsync());

        _ = RefreshAsync();
        LocalizationManager.LanguageChanged += (_, _) => _ = RefreshAsync();
    }

    public ObservableCollection<AppPermissionItemViewModel> Items { get; }

    public Command<AppPermissionItemViewModel> ResolvePermissionCommand { get; }
    public Command RefreshCommand { get; }

    public string AlarmModeTitle {
        get => _alarmModeTitle;
        private set => SetProperty(ref _alarmModeTitle, value);
    }

    public string AlarmModeStatus {
        get => _alarmModeStatus;
        private set => SetProperty(ref _alarmModeStatus, value);
    }

    public string AlarmModeDescription {
        get => _alarmModeDescription;
        private set => SetProperty(ref _alarmModeDescription, value);
    }

    public bool IsBusy {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public async Task RefreshAsync() {
        if (IsBusy) {
            return;
        }

        try {
            IsBusy = true;
            var snapshots = await _permissionCenterService.GetSnapshotsAsync().ConfigureAwait(false);
            var alarmDecision = await _alarmCapabilityService.GetCurrentDecisionAsync().ConfigureAwait(false);
            var lookup = snapshots.ToDictionary(item => item.Kind);
            RunOnMainThread(() => {
                AlarmModeTitle = LocalizationManager.Translate("PermissionsAlarmModeTitle");
                AlarmModeStatus = GetAlarmModeStatus(alarmDecision.SupportStatus);
                AlarmModeDescription = GetAlarmModeDescription(alarmDecision.SupportStatus);

                foreach (var item in Items) {
                    if (!lookup.TryGetValue(item.Kind, out var snapshot)) {
                        continue;
                    }

                    item.Title = GetTitle(item.Kind);
                    item.Description = GetDescription(item.Kind);
                    item.RoleText = LocalizationManager.Translate(snapshot.IsCritical
                        ? "PermissionCategory_Critical"
                        : "PermissionCategory_Optional");
                    item.FallbackText = GetFallbackDescription(item.Kind);
                    item.IsCritical = snapshot.IsCritical;
                    item.IsGranted = snapshot.IsGranted;
                    item.StatusText = LocalizationManager.Translate(snapshot.IsGranted
                        ? "PermissionStatus_Enabled"
                        : "PermissionStatus_Disabled");
                    item.ActionText = LocalizationManager.Translate(snapshot.IsGranted || snapshot.UsesSettingsFlow
                        ? "PermissionAction_OpenSettings"
                        : "PermissionAction_Request");
                }
            });
        } catch (Exception ex) {
            _logger.LogException(ex, "AppPermissionsViewModel.RefreshAsync");
        } finally {
            IsBusy = false;
        }
    }

    private async Task ResolvePermissionAsync(AppPermissionItemViewModel? item) {
        if (item == null || IsBusy) {
            return;
        }

        try {
            IsBusy = true;
            await _permissionCenterService.ResolveAsync(item.Kind).ConfigureAwait(false);
        } catch (Exception ex) {
            _logger.LogException(ex, "AppPermissionsViewModel.ResolvePermissionAsync");
        } finally {
            IsBusy = false;
            await RefreshAsync().ConfigureAwait(false);
        }
    }

    private static string GetTitle(AppPermissionKind kind) {
        return kind switch {
            AppPermissionKind.Notifications => LocalizationManager.Translate("PermissionsNotificationsTitle"),
            AppPermissionKind.FullScreenIntents => LocalizationManager.Translate("PermissionsFullScreenIntentTitle"),
            AppPermissionKind.DisplayOverApps => LocalizationManager.Translate("PermissionsOverlayTitle"),
            AppPermissionKind.ExactAlarms => LocalizationManager.Translate("PermissionsExactAlarmTitle"),
            AppPermissionKind.Location => LocalizationManager.Translate("PermissionsLocationTitle"),
            _ => LocalizationManager.Translate("PermissionsTitle")
        };
    }

    private static string GetDescription(AppPermissionKind kind) {
        return kind switch {
            AppPermissionKind.Notifications => LocalizationManager.Translate("PermissionsNotificationsDescription"),
            AppPermissionKind.FullScreenIntents => LocalizationManager.Translate("PermissionsFullScreenIntentDescription"),
            AppPermissionKind.DisplayOverApps => LocalizationManager.Translate("PermissionsOverlayDescription"),
            AppPermissionKind.ExactAlarms => LocalizationManager.Translate("PermissionsExactAlarmDescription"),
            AppPermissionKind.Location => LocalizationManager.Translate("PermissionsLocationDescription"),
            _ => string.Empty
        };
    }

    private static string GetFallbackDescription(AppPermissionKind kind) {
        return kind switch {
            AppPermissionKind.Notifications => LocalizationManager.Translate("PermissionsNotificationsFallback"),
            AppPermissionKind.FullScreenIntents => LocalizationManager.Translate("PermissionsFullScreenIntentFallback"),
            AppPermissionKind.DisplayOverApps => LocalizationManager.Translate("PermissionsOverlayFallback"),
            AppPermissionKind.ExactAlarms => LocalizationManager.Translate("PermissionsExactAlarmFallback"),
            AppPermissionKind.Location => LocalizationManager.Translate("PermissionsLocationFallback"),
            _ => string.Empty
        };
    }

    private static string GetAlarmModeStatus(AlarmSupportStatus status) {
        return status switch {
            AlarmSupportStatus.FullSupport => LocalizationManager.Translate("PermissionsAlarmMode_FullSupport"),
            AlarmSupportStatus.LockScreenAndNotifications => LocalizationManager.Translate("PermissionsAlarmMode_LockScreenAndNotifications"),
            AlarmSupportStatus.OverlayAndControlNotification => LocalizationManager.Translate("PermissionsAlarmMode_OverlayAndControl"),
            AlarmSupportStatus.ControlNotificationOnly => LocalizationManager.Translate("PermissionsAlarmMode_ControlNotificationOnly"),
            AlarmSupportStatus.ApproximateNotificationFallback => LocalizationManager.Translate("PermissionsAlarmMode_ApproximateFallback"),
            AlarmSupportStatus.NotificationsMissing => LocalizationManager.Translate("PermissionsAlarmMode_NotificationsMissing"),
            _ => LocalizationManager.Translate("PermissionsAlarmMode_Unsupported")
        };
    }

    private static string GetAlarmModeDescription(AlarmSupportStatus status) {
        return status switch {
            AlarmSupportStatus.FullSupport => LocalizationManager.Translate("PermissionsAlarmModeDescription_FullSupport"),
            AlarmSupportStatus.LockScreenAndNotifications => LocalizationManager.Translate("PermissionsAlarmModeDescription_LockScreenAndNotifications"),
            AlarmSupportStatus.OverlayAndControlNotification => LocalizationManager.Translate("PermissionsAlarmModeDescription_OverlayAndControl"),
            AlarmSupportStatus.ControlNotificationOnly => LocalizationManager.Translate("PermissionsAlarmModeDescription_ControlNotificationOnly"),
            AlarmSupportStatus.ApproximateNotificationFallback => LocalizationManager.Translate("PermissionsAlarmModeDescription_ApproximateFallback"),
            AlarmSupportStatus.NotificationsMissing => LocalizationManager.Translate("PermissionsAlarmModeDescription_NotificationsMissing"),
            _ => LocalizationManager.Translate("PermissionsAlarmModeDescription_Unsupported")
        };
    }

    private static void RunOnMainThread(Action action) {
        if (MainThread.IsMainThread) {
            action();
        } else {
            MainThread.BeginInvokeOnMainThread(action);
        }
    }
}
