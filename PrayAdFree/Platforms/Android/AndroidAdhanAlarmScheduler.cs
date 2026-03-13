#if ANDROID
using System.Text.Json;
using Android.App;
using Android.Content;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.Platforms.Android;

internal static class AndroidAdhanAlarmScheduler {
    public const string AlarmPayloadExtra = "adhan_alarm_payload";
    private const string StoreFileName = "android_adhan_alarm_store.json";

    internal readonly record struct ScheduledAlarm(int RequestCode, DateTime When, string Payload);

    private sealed class AlarmStore {
        public List<int> RequestCodes { get; set; } = [];
    }

    public static void ReplaceScheduledAlarms(IReadOnlyList<ScheduledAlarm> alarms) {
        var context = global::Android.App.Application.Context;
        if (context == null) {
            return;
        }

        var existingRequestCodes = LoadRequestCodes(context);
        foreach (var requestCode in existingRequestCodes) {
            CancelCore(context, requestCode);
        }

        foreach (var alarm in alarms) {
            ScheduleCore(context, alarm);
        }

        SaveRequestCodes(context, alarms.Select(item => item.RequestCode));
    }

    public static void UpsertAlarm(ScheduledAlarm alarm) {
        var context = global::Android.App.Application.Context;
        if (context == null) {
            return;
        }

        var requestCodes = LoadRequestCodes(context);
        CancelCore(context, alarm.RequestCode);
        ScheduleCore(context, alarm);
        requestCodes.Add(alarm.RequestCode);
        SaveRequestCodes(context, requestCodes);
    }

    public static void Cancel(int requestCode) {
        var context = global::Android.App.Application.Context;
        if (context == null) {
            return;
        }

        var requestCodes = LoadRequestCodes(context);
        CancelCore(context, requestCode);
        requestCodes.Remove(requestCode);
        SaveRequestCodes(context, requestCodes);
    }

    public static void CancelAll() {
        var context = global::Android.App.Application.Context;
        if (context == null) {
            return;
        }

        var requestCodes = LoadRequestCodes(context);
        foreach (var requestCode in requestCodes) {
            CancelCore(context, requestCode);
        }

        SaveRequestCodes(context, []);
    }

    private static void ScheduleCore(Context context, ScheduledAlarm alarm) {
        if (context.GetSystemService(Context.AlarmService) is not AlarmManager alarmManager) {
            return;
        }

        var operation = BuildTriggerPendingIntent(context, alarm, PendingIntentFlags.UpdateCurrent);
        if (operation == null) {
            return;
        }
        var showIntent = BuildShowPendingIntent(context, alarm);
        var triggerAtMillis = new DateTimeOffset(NormalizeToLocal(alarm.When)).ToUnixTimeMilliseconds();
        var alarmClockInfo = new AlarmManager.AlarmClockInfo(triggerAtMillis, showIntent);
        alarmManager.SetAlarmClock(alarmClockInfo, operation);
    }

    private static void CancelCore(Context context, int requestCode) {
        if (context.GetSystemService(Context.AlarmService) is not AlarmManager alarmManager) {
            return;
        }

        var existing = BuildTriggerPendingIntent(context, new ScheduledAlarm(requestCode, DateTime.Now, string.Empty), PendingIntentFlags.NoCreate);
        if (existing == null) {
            return;
        }

        alarmManager.Cancel(existing);
        existing.Cancel();
        existing.Dispose();
    }

    private static PendingIntent? BuildTriggerPendingIntent(Context context, ScheduledAlarm alarm, PendingIntentFlags flags) {
        var intent = new Intent(context, typeof(AndroidAdhanAlarmReceiver));
        intent.SetAction(AdhanPlaybackService.AndroidAlarmAction);
        if (!string.IsNullOrWhiteSpace(alarm.Payload)) {
            intent.PutExtra(AlarmPayloadExtra, alarm.Payload);
        }

        return PendingIntent.GetBroadcast(
            context,
            alarm.RequestCode,
            intent,
            NormalizePendingIntentFlags(flags));
    }

    private static PendingIntent BuildShowPendingIntent(Context context, ScheduledAlarm alarm) {
        var intent = new Intent(context, typeof(AlarmActivity));
        intent.SetAction(AdhanPlaybackService.AndroidAlarmAction);
        intent.PutExtra(AlarmPayloadExtra, alarm.Payload);
        intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        return PendingIntent.GetActivity(
            context,
            alarm.RequestCode,
            intent,
            NormalizePendingIntentFlags(PendingIntentFlags.UpdateCurrent))!;
    }

    private static PendingIntentFlags NormalizePendingIntentFlags(PendingIntentFlags flags) {
        if (OperatingSystem.IsAndroidVersionAtLeast(23)) {
            flags |= PendingIntentFlags.Immutable;
        }

        return flags;
    }

    private static HashSet<int> LoadRequestCodes(Context context) {
        try {
            var path = GetStorePath(context);
            if (!File.Exists(path)) {
                return [];
            }

            var json = File.ReadAllText(path);
            var store = JsonSerializer.Deserialize<AlarmStore>(json);
            return store?.RequestCodes?.ToHashSet() ?? [];
        } catch {
            return [];
        }
    }

    private static void SaveRequestCodes(Context context, IEnumerable<int> requestCodes) {
        try {
            var path = GetStorePath(context);
            var store = new AlarmStore {
                RequestCodes = requestCodes
                    .Distinct()
                    .OrderBy(item => item)
                    .ToList()
            };
            File.WriteAllText(path, JsonSerializer.Serialize(store));
        } catch {
        }
    }

    private static string GetStorePath(Context context) {
        var root = context.FilesDir?.AbsolutePath ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Directory.CreateDirectory(root);
        return Path.Combine(root, StoreFileName);
    }

    private static DateTime NormalizeToLocal(DateTime value) {
        return value.Kind switch {
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Local),
            DateTimeKind.Utc => value.ToLocalTime(),
            _ => value
        };
    }
}
#endif
