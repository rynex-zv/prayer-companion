using Microsoft.Maui.ApplicationModel;
#if ANDROID
using Android.Content;
using Android.OS;
#endif

namespace Pray_Ad_Free.Services;

public enum AlarmSchedulingMode {
    ExactAlarm,
    ApproximateNotification,
    Unsupported
}

public enum AlarmPresentationMode {
    Overlay,
    FullscreenActivity,
    ControlNotification,
    None
}

public enum AlarmSupportStatus {
    FullSupport,
    LockScreenAndNotifications,
    OverlayAndControlNotification,
    ControlNotificationOnly,
    ApproximateNotificationFallback,
    NotificationsMissing,
    Unsupported
}

public readonly record struct AlarmPermissionState(
    bool NotificationsGranted,
    bool ExactAlarmsGranted,
    bool FullScreenIntentsGranted,
    bool DisplayOverAppsGranted);

public readonly record struct AndroidAlarmCapabilityDecision(
    AlarmPermissionState Permissions,
    bool ScreenOnAndUnlocked,
    AlarmSchedulingMode SchedulingMode,
    AlarmPresentationMode PresentationMode,
    AlarmSupportStatus SupportStatus);

public sealed class AndroidAlarmCapabilityService {
    private readonly AppPermissionCenterService _permissionCenterService;

    public AndroidAlarmCapabilityService(AppPermissionCenterService permissionCenterService) {
        _permissionCenterService = permissionCenterService;
    }

    public async Task<AndroidAlarmCapabilityDecision> GetCurrentDecisionAsync() {
        var permissions = await _permissionCenterService.GetAlarmPermissionStateAsync().ConfigureAwait(false);
        return ResolveDecision(permissions, IsScreenOnAndUnlocked());
    }

    public AndroidAlarmCapabilityDecision ResolveDecision(
        AlarmPermissionState permissions,
        bool screenOnAndUnlocked) {
        var schedulingMode = ResolveSchedulingMode(permissions);
        var presentationMode = ResolvePresentationMode(permissions, screenOnAndUnlocked, schedulingMode);
        var supportStatus = ResolveSupportStatus(permissions);

        return new AndroidAlarmCapabilityDecision(
            permissions,
            screenOnAndUnlocked,
            schedulingMode,
            presentationMode,
            supportStatus);
    }

    public static string BuildLogDetails(AndroidAlarmCapabilityDecision decision) {
        return string.Join(';',
            $"schedulingMode={decision.SchedulingMode}",
            $"presentationMode={decision.PresentationMode}",
            $"supportStatus={decision.SupportStatus}",
            $"notificationsGranted={decision.Permissions.NotificationsGranted}",
            $"exactGranted={decision.Permissions.ExactAlarmsGranted}",
            $"fullscreenGranted={decision.Permissions.FullScreenIntentsGranted}",
            $"overlayGranted={decision.Permissions.DisplayOverAppsGranted}",
            $"screenUnlocked={decision.ScreenOnAndUnlocked}");
    }

    private static AlarmSchedulingMode ResolveSchedulingMode(AlarmPermissionState permissions) {
        if (!permissions.NotificationsGranted) {
            return AlarmSchedulingMode.Unsupported;
        }

        return permissions.ExactAlarmsGranted
            ? AlarmSchedulingMode.ExactAlarm
            : AlarmSchedulingMode.ApproximateNotification;
    }

    private static AlarmPresentationMode ResolvePresentationMode(
        AlarmPermissionState permissions,
        bool screenOnAndUnlocked,
        AlarmSchedulingMode schedulingMode) {
        if (schedulingMode == AlarmSchedulingMode.Unsupported) {
            return AlarmPresentationMode.None;
        }

        if (screenOnAndUnlocked) {
            return permissions.DisplayOverAppsGranted
                ? AlarmPresentationMode.Overlay
                : AlarmPresentationMode.ControlNotification;
        }

        if (permissions.FullScreenIntentsGranted && schedulingMode == AlarmSchedulingMode.ExactAlarm) {
            return AlarmPresentationMode.FullscreenActivity;
        }

        return AlarmPresentationMode.ControlNotification;
    }

    private static AlarmSupportStatus ResolveSupportStatus(AlarmPermissionState permissions) {
        if (!permissions.NotificationsGranted && !permissions.ExactAlarmsGranted) {
            return AlarmSupportStatus.Unsupported;
        }

        if (!permissions.NotificationsGranted) {
            return AlarmSupportStatus.NotificationsMissing;
        }

        if (!permissions.ExactAlarmsGranted) {
            return AlarmSupportStatus.ApproximateNotificationFallback;
        }

        if (permissions.DisplayOverAppsGranted && permissions.FullScreenIntentsGranted) {
            return AlarmSupportStatus.FullSupport;
        }

        if (!permissions.DisplayOverAppsGranted && permissions.FullScreenIntentsGranted) {
            return AlarmSupportStatus.LockScreenAndNotifications;
        }

        if (permissions.DisplayOverAppsGranted && !permissions.FullScreenIntentsGranted) {
            return AlarmSupportStatus.OverlayAndControlNotification;
        }

        return AlarmSupportStatus.ControlNotificationOnly;
    }

    private static bool IsScreenOnAndUnlocked() {
#if ANDROID
        var context = global::Android.App.Application.Context;
        if (context == null) {
            return false;
        }

        try {
            if (context.GetSystemService(Context.PowerService) is not PowerManager powerManager ||
                !powerManager.IsInteractive) {
                return false;
            }

            if (context.GetSystemService(Context.KeyguardService) is not global::Android.App.KeyguardManager keyguardManager) {
                return true;
            }

            return !keyguardManager.IsKeyguardLocked;
        } catch {
            return false;
        }
#else
        return MainThread.IsMainThread;
#endif
    }
}
