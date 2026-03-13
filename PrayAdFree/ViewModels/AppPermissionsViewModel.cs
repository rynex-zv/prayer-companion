using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;
using Pray_Ad_Free.Models;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.ViewModels;

public sealed class AppPermissionsViewModel : ViewModelBase {
    private readonly AppPermissionCenterService _permissionCenterService;
    private readonly IAppLogger _logger;
    private bool _isBusy;

    public AppPermissionsViewModel(AppPermissionCenterService permissionCenterService, IAppLogger logger) {
        _permissionCenterService = permissionCenterService;
        _logger = logger;
        Items = new ObservableCollection<AppPermissionItemViewModel> {
            new(AppPermissionKind.Notifications),
            new(AppPermissionKind.FullScreenIntents),
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
            var lookup = snapshots.ToDictionary(item => item.Kind);
            RunOnMainThread(() => {
                foreach (var item in Items) {
                    if (!lookup.TryGetValue(item.Kind, out var snapshot)) {
                        continue;
                    }

                    item.Title = GetTitle(item.Kind);
                    item.Description = GetDescription(item.Kind);
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
            AppPermissionKind.ExactAlarms => LocalizationManager.Translate("PermissionsExactAlarmTitle"),
            AppPermissionKind.Location => LocalizationManager.Translate("PermissionsLocationTitle"),
            _ => LocalizationManager.Translate("PermissionsTitle")
        };
    }

    private static string GetDescription(AppPermissionKind kind) {
        return kind switch {
            AppPermissionKind.Notifications => LocalizationManager.Translate("PermissionsNotificationsDescription"),
            AppPermissionKind.FullScreenIntents => LocalizationManager.Translate("PermissionsFullScreenIntentDescription"),
            AppPermissionKind.ExactAlarms => LocalizationManager.Translate("PermissionsExactAlarmDescription"),
            AppPermissionKind.Location => LocalizationManager.Translate("PermissionsLocationDescription"),
            _ => string.Empty
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
