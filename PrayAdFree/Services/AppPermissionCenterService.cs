using Microsoft.Maui.ApplicationModel;
using Plugin.LocalNotification;
#if ANDROID
using Android.App;
using Android.Content;
using Android.Provider;
#endif

namespace Pray_Ad_Free.Services;

public enum AppPermissionKind {
    Notifications,
    FullScreenIntents,
    DisplayOverApps,
    ExactAlarms,
    Location
}

public readonly record struct AppPermissionSnapshot(
    AppPermissionKind Kind,
    bool IsGranted,
    bool UsesSettingsFlow,
    bool IsCritical,
    bool IsSupported);

public sealed class AppPermissionCenterService : IAppPermissionCenterService {
    public async Task<IReadOnlyList<AppPermissionSnapshot>> GetSnapshotsAsync() {
        var items = new List<AppPermissionSnapshot> {
            new(AppPermissionKind.Notifications, await IsNotificationsGrantedAsync().ConfigureAwait(false), UsesSettingsFlow: false, IsCritical: true, IsSupported: IsNotificationsSupported()),
            new(AppPermissionKind.FullScreenIntents, await IsFullScreenIntentsGrantedAsync().ConfigureAwait(false), UsesSettingsFlow: true, IsCritical: false, IsSupported: IsFullScreenIntentsSupported()),
            new(AppPermissionKind.DisplayOverApps, await IsDisplayOverAppsGrantedAsync().ConfigureAwait(false), UsesSettingsFlow: true, IsCritical: false, IsSupported: IsDisplayOverAppsSupported()),
            new(AppPermissionKind.ExactAlarms, await IsExactAlarmsGrantedAsync().ConfigureAwait(false), UsesSettingsFlow: true, IsCritical: true, IsSupported: IsExactAlarmsSupported()),
            new(AppPermissionKind.Location, await IsLocationGrantedAsync().ConfigureAwait(false), UsesSettingsFlow: false, IsCritical: false, IsSupported: IsLocationSupported())
        };

        return items.Where(item => item.IsSupported).ToList();
    }

    public async Task<AlarmPermissionState> GetAlarmPermissionStateAsync() {
        return new AlarmPermissionState(
            NotificationsGranted: await IsNotificationsGrantedAsync().ConfigureAwait(false),
            ExactAlarmsGranted: await IsExactAlarmsGrantedAsync().ConfigureAwait(false),
            FullScreenIntentsGranted: await IsFullScreenIntentsGrantedAsync().ConfigureAwait(false),
            DisplayOverAppsGranted: await IsDisplayOverAppsGrantedAsync().ConfigureAwait(false));
    }

    public async Task ResolveAsync(AppPermissionKind kind) {
        switch (kind) {
            case AppPermissionKind.Notifications:
                await ResolveNotificationsAsync().ConfigureAwait(false);
                return;
            case AppPermissionKind.FullScreenIntents:
                await OpenFullScreenIntentSettingsAsync().ConfigureAwait(false);
                return;
            case AppPermissionKind.DisplayOverApps:
                await OpenDisplayOverAppsSettingsAsync().ConfigureAwait(false);
                return;
            case AppPermissionKind.ExactAlarms:
                await OpenExactAlarmSettingsAsync().ConfigureAwait(false);
                return;
            case AppPermissionKind.Location:
                await ResolveLocationAsync().ConfigureAwait(false);
                return;
        }
    }

    private static async Task<bool> IsNotificationsGrantedAsync() {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(33)) {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>().ConfigureAwait(false);
            return status == PermissionStatus.Granted;
        }
#endif
        return true;
    }

    private static bool IsNotificationsSupported() {
#if ANDROID
        return OperatingSystem.IsAndroidVersionAtLeast(33);
#else
        return !OperatingSystem.IsWindows();
#endif
    }

    private static async Task<bool> IsLocationGrantedAsync() {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>().ConfigureAwait(false);
        return status == PermissionStatus.Granted;
    }

    private static bool IsLocationSupported() => true;

    private static Task<bool> IsExactAlarmsGrantedAsync() {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(31)) {
            var context = global::Android.App.Application.Context;
            var manager = context?.GetSystemService(Context.AlarmService) as AlarmManager;
            return Task.FromResult(manager?.CanScheduleExactAlarms() ?? false);
        }
#endif
        return Task.FromResult(true);
    }

    private static bool IsExactAlarmsSupported() {
#if ANDROID
        return OperatingSystem.IsAndroidVersionAtLeast(31);
#else
        return false;
#endif
    }

    private static Task<bool> IsFullScreenIntentsGrantedAsync() {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(34)) {
            var context = global::Android.App.Application.Context;
            var manager = context?.GetSystemService(Context.NotificationService) as NotificationManager;
            try {
                return Task.FromResult(manager?.CanUseFullScreenIntent() ?? false);
            } catch {
                return Task.FromResult(false);
            }
        }
#endif
        return Task.FromResult(true);
    }

    private static bool IsFullScreenIntentsSupported() {
#if ANDROID
        return OperatingSystem.IsAndroidVersionAtLeast(34);
#else
        return false;
#endif
    }

    private static Task<bool> IsDisplayOverAppsGrantedAsync() {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(23)) {
            var context = global::Android.App.Application.Context;
            return Task.FromResult(context != null && Settings.CanDrawOverlays(context));
        }
#endif
        return Task.FromResult(true);
    }

    private static bool IsDisplayOverAppsSupported() {
#if ANDROID
        return OperatingSystem.IsAndroidVersionAtLeast(23);
#else
        return false;
#endif
    }

    private static async Task ResolveNotificationsAsync() {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(33)) {
            var status = await MainThread.InvokeOnMainThreadAsync(
                Permissions.RequestAsync<Permissions.PostNotifications>).ConfigureAwait(false);
            if (status == PermissionStatus.Granted) {
                return;
            }

            await OpenAppSettingsAsync().ConfigureAwait(false);
            return;
        }
#endif
        await MainThread.InvokeOnMainThreadAsync(() => LocalNotificationCenter.Current.RequestNotificationPermission())
            .ConfigureAwait(false);
    }

    private static async Task ResolveLocationAsync() {
        var status = await MainThread.InvokeOnMainThreadAsync(
            Permissions.RequestAsync<Permissions.LocationWhenInUse>).ConfigureAwait(false);
        if (status == PermissionStatus.Granted) {
            return;
        }

        await OpenAppSettingsAsync().ConfigureAwait(false);
    }

    private static Task OpenAppSettingsAsync() {
        return MainThread.InvokeOnMainThreadAsync(() => {
            AppInfo.Current.ShowSettingsUI();
            return Task.CompletedTask;
        });
    }

    private static Task OpenExactAlarmSettingsAsync() {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(31)) {
            return MainThread.InvokeOnMainThreadAsync(() => {
                var context = global::Android.App.Application.Context;
                if (context == null) {
                    AppInfo.Current.ShowSettingsUI();
                    return Task.CompletedTask;
                }

#pragma warning disable CA1416
                var intent = new Intent(Settings.ActionRequestScheduleExactAlarm);
#pragma warning restore CA1416
                intent.SetData(global::Android.Net.Uri.Parse($"package:{context.PackageName}"));
                intent.AddFlags(ActivityFlags.NewTask);
                context.StartActivity(intent);
                return Task.CompletedTask;
            });
        }
#endif
        return OpenAppSettingsAsync();
    }

    private static Task OpenFullScreenIntentSettingsAsync() {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(34)) {
            return MainThread.InvokeOnMainThreadAsync(() => {
                var context = global::Android.App.Application.Context;
                if (context == null) {
                    AppInfo.Current.ShowSettingsUI();
                    return Task.CompletedTask;
                }

                try {
#pragma warning disable CA1416
                    var intent = new Intent(Settings.ActionManageAppUseFullScreenIntent);
#pragma warning restore CA1416
                    intent.SetData(global::Android.Net.Uri.Parse($"package:{context.PackageName}"));
                    intent.AddFlags(ActivityFlags.NewTask);
                    context.StartActivity(intent);
                } catch {
                    AppInfo.Current.ShowSettingsUI();
                }

                return Task.CompletedTask;
            });
        }
#endif
        return OpenAppSettingsAsync();
    }

    private static Task OpenDisplayOverAppsSettingsAsync() {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(23)) {
            return MainThread.InvokeOnMainThreadAsync(() => {
                var context = global::Android.App.Application.Context;
                if (context == null) {
                    AppInfo.Current.ShowSettingsUI();
                    return Task.CompletedTask;
                }

                try {
#pragma warning disable CA1416
                    var intent = new Intent(Settings.ActionManageOverlayPermission);
#pragma warning restore CA1416
                    intent.SetData(global::Android.Net.Uri.Parse($"package:{context.PackageName}"));
                    intent.AddFlags(ActivityFlags.NewTask);
                    context.StartActivity(intent);
                } catch {
                    AppInfo.Current.ShowSettingsUI();
                }

                return Task.CompletedTask;
            });
        }
#endif
        return OpenAppSettingsAsync();
    }
}
