using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Android.Views;
using PrayAdFree.Core.Models;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.Platforms.Android;

[Activity(
    Theme = "@style/PrayAdFree.AlarmFullscreen",
    LaunchMode = LaunchMode.SingleTop,
    Exported = false,
    ExcludeFromRecents = true,
    NoHistory = true,
    ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public sealed class AlarmActivity : Activity {
    private const string LogTag = "PrayAdFree.Alarm";

    protected override void OnCreate(Bundle? savedInstanceState) {
        base.OnCreate(savedInstanceState);
        ConfigureForLockScreen();
        ForwardToReactHost(Intent, "OnCreate");
    }

    protected override void OnNewIntent(Intent? intent) {
        base.OnNewIntent(intent);
        if (intent != null) {
            Intent = intent;
        }

        ForwardToReactHost(intent, "OnNewIntent");
    }

    private void ConfigureForLockScreen() {
        if (OperatingSystem.IsAndroidVersionAtLeast(27)) {
            SetShowWhenLocked(true);
            SetTurnScreenOn(true);
        } else {
#pragma warning disable CS0618
            Window?.AddFlags(WindowManagerFlags.ShowWhenLocked | WindowManagerFlags.TurnScreenOn);
#pragma warning restore CS0618
        }

        Window?.AddFlags(WindowManagerFlags.KeepScreenOn | WindowManagerFlags.DismissKeyguard);
    }

    private void ForwardToReactHost(Intent? source, string reason) {
        if (!TryGetAlarmPayload(source, out var payloadText)) {
            Log.Warn(LogTag, $"AlarmActivity payload missing reason={reason}");
            Finish();
            return;
        }

        AndroidAlarmFullscreenNotifier.Cancel(this);
        var target = new Intent(this, typeof(MainActivity));
        target.SetAction(AdhanPlaybackService.AndroidAlarmAction);
        target.PutExtra(AndroidAdhanAlarmScheduler.AlarmPayloadExtra, payloadText);
        target.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        StartActivity(target);
        Log.Info(LogTag, $"AlarmActivity forwarded to React host reason={reason}");
        Finish();
    }

    private static bool TryGetAlarmPayload(Intent? intent, out string payloadText) {
        payloadText = string.Empty;
        if (intent == null) {
            return false;
        }

        var directPayload = intent.GetStringExtra(AndroidAdhanAlarmScheduler.AlarmPayloadExtra);
        if (!string.IsNullOrWhiteSpace(directPayload) && AdhanAlarmPayload.TryParse(directPayload, out _)) {
            payloadText = directPayload;
            return true;
        }

        var keys = intent.Extras?.KeySet();
        if (keys == null) {
            return false;
        }

        foreach (var key in keys) {
            var text = intent.Extras?.Get(key)?.ToString();
            if (!string.IsNullOrWhiteSpace(text) && AdhanAlarmPayload.TryParse(text, out _)) {
                payloadText = text;
                return true;
            }
        }

        return false;
    }
}
