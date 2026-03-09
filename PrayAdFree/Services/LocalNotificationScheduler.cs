using System.Collections.Generic;
using System.Linq;
using Plugin.LocalNotification;
#if ANDROID
using Plugin.LocalNotification.AndroidOption;
#endif
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace Pray_Ad_Free.Services;

public sealed class LocalNotificationScheduler : ILocalNotificationScheduler {
    private readonly PrayerSchedulePlanner _planner;

    public LocalNotificationScheduler(PrayerSchedulePlanner planner) {
        _planner = planner;
    }

    public async Task ScheduleAsync(IEnumerable<PrayerDay> days, AppSettings settings, CancellationToken cancellationToken, bool requestPermissions = true) {
        var permission = new NotificationPermission {
            AskPermission = requestPermissions
        };
#if ANDROID
        permission.Android = new AndroidNotificationPermission {
            RequestPermissionToScheduleExactAlarm = requestPermissions
        };
#endif
        if (requestPermissions) {
            await LocalNotificationCenter.Current.RequestNotificationPermission(permission);
        }
        await CancelAsync();

        var requests = new List<NotificationRequest>();

        foreach (var day in days) {
            var schedule = _planner.BuildSchedule(day, settings.Notifications);
            foreach (var item in schedule) {
                if (item.Time <= DateTime.Now) {
                    continue;
                }

                var prayerName = LocalizationManager.TranslatePrayer(item.Prayer);
                var overrideSettings = FindOverride(settings.Notifications.PrayerOverrides, item.Prayer);
                var soundKey = overrideSettings?.SoundKey ?? settings.Notifications.SoundKey;
                var effectiveSoundKey = AdhanSoundLibrary.ResolveEffectiveSoundKey(soundKey);
                var isSilent = AdhanSoundLibrary.IsSilent(effectiveSoundKey);
                var useRuntimeAdhanPlayback = ShouldUseRuntimeAdhanPlayback();
                var notificationSound = ResolveSystemNotificationSound(settings.Notifications, effectiveSoundKey, isSilent);
                var vibrationOverride = overrideSettings?.EnableVibration;
                var request = new NotificationRequest {
                    NotificationId = BuildId(day.Date, item.Prayer),
                    Title = string.Format(LocalizationManager.Translate("Notification_PrayerTitle"), prayerName),
                    Description = string.Format(LocalizationManager.Translate("Notification_PrayerBody"), prayerName),
                    Silent = ResolveNotificationSilent(useRuntimeAdhanPlayback, isSilent),
                    Sound = useRuntimeAdhanPlayback ? string.Empty : notificationSound ?? string.Empty,
                    ReturningData = isSilent
                        ? string.Empty
                        : AdhanNotificationPayload.BuildPlay(item.Prayer, effectiveSoundKey),
                    Schedule = new NotificationRequestSchedule {
                        NotifyTime = ToLocalKind(item.Time),
                        NotifyRepeatInterval = null
#if ANDROID
                        ,
                        Android = new AndroidScheduleOptions {
                            AlarmType = AndroidAlarmType.RtcWakeup
                        }
#endif
                    },
#if ANDROID
                    Android = new AndroidOptions {
                        Priority = AndroidPriority.Default,
                        ChannelId = BuildAndroidChannelId(effectiveSoundKey, isSilent, useRuntimeAdhanPlayback),
                        VibrationPattern = isSilent ? Array.Empty<long>() : BuildVibration(settings.Notifications, vibrationOverride)
                    }
#endif
                };
                requests.Add(request);
            }

            await ScheduleFastingRemindersAsync(day, settings, requests);
            await ScheduleAdhanRemindersAsync(day, settings, requests);
        }

        await EmitRequestsAsync(requests, settings);
    }

    public Task CancelAsync() {
        LocalNotificationCenter.Current.CancelAll();
        return Task.CompletedTask;
    }

    private static int BuildId(DateOnly date, PrayerId prayer) {
        return int.Parse($"{date:yyMMdd}{(int)prayer + 1}");
    }

    private static int BuildReminderId(DateOnly date, int group, int index) {
        return int.Parse($"{date:yyMMdd}{group}{index:00}");
    }

    private static bool ShouldSchedule(DateTime time) => time > DateTime.Now;

    private static DateTime ToLocalKind(DateTime time) {
        return time.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(time, DateTimeKind.Local)
            : time;
    }

    private static long[] BuildVibration(NotificationSettings settings, bool? overrideEnabled = null) {
        var enabled = overrideEnabled ?? settings.EnableVibration;
        if (!enabled) {
            return Array.Empty<long>();
        }

        var strength = settings.VibrationStrength switch {
            VibrationStrength.Low => 200,
            VibrationStrength.Medium => 400,
            _ => 700
        };

        return settings.VibrationPattern switch {
            VibrationPattern.Long => new long[] { 0, strength * 2 },
            VibrationPattern.Pulse => new long[] { 0, strength, 200, strength, 200, strength },
            _ => new long[] { 0, strength, 200, strength }
        };
    }

    private async Task ScheduleFastingRemindersAsync(
        PrayerDay day,
        AppSettings settings,
        ICollection<NotificationRequest> requests) {
        var baseImsak = day.Timings.Imsak.AddMinutes(-settings.FastingOffsets.ImsakAdvanceMinutes);
        var baseIftar = day.Timings.Maghrib.AddMinutes(settings.FastingOffsets.IftarDelayMinutes);

        var imsakReminders = settings.FastingReminders.ImsakRemindersMinutes.Distinct().OrderBy(item => item).ToList();
        for (var i = 0; i < imsakReminders.Count; i++) {
            var time = baseImsak.AddMinutes(imsakReminders[i]);
            if (!ShouldSchedule(time)) {
                continue;
            }

            requests.Add(new NotificationRequest {
                NotificationId = BuildReminderId(day.Date, 8, i),
                Title = LocalizationManager.Translate("ImsakReminder"),
                Description = LocalizationManager.Translate("Imsak"),
                Schedule = new NotificationRequestSchedule {
                    NotifyTime = ToLocalKind(time),
                    NotifyRepeatInterval = null
#if ANDROID
                    ,
                    Android = new AndroidScheduleOptions {
                        AlarmType = AndroidAlarmType.RtcWakeup
                    }
#endif
                }
            });
        }

        var iftarReminders = settings.FastingReminders.IftarRemindersMinutes.Distinct().OrderBy(item => item).ToList();
        for (var i = 0; i < iftarReminders.Count; i++) {
            var time = baseIftar.AddMinutes(iftarReminders[i]);
            if (!ShouldSchedule(time)) {
                continue;
            }

            requests.Add(new NotificationRequest {
                NotificationId = BuildReminderId(day.Date, 9, i),
                Title = LocalizationManager.Translate("IftarReminder"),
                Description = LocalizationManager.Translate("Iftar"),
                Schedule = new NotificationRequestSchedule {
                    NotifyTime = ToLocalKind(time),
                    NotifyRepeatInterval = null
#if ANDROID
                    ,
                    Android = new AndroidScheduleOptions {
                        AlarmType = AndroidAlarmType.RtcWakeup
                    }
#endif
                }
            });
        }
    }

    private async Task ScheduleAdhanRemindersAsync(
        PrayerDay day,
        AppSettings settings,
        ICollection<NotificationRequest> requests) {
        var notificationSettings = settings.Notifications;
        if (!notificationSettings.EnableAdhan) {
            return;
        }

        var offsets = notificationSettings.ReminderOffsetsMinutes
            .Where(item => item != 0)
            .Distinct()
            .OrderBy(item => item)
            .ToList();
        if (offsets.Count == 0) {
            return;
        }

        var prayers = notificationSettings.ReminderScope == AdhanReminderScope.SpecificPrayer
            ? new[] { notificationSettings.ReminderPrayer }
            : new[] { PrayerId.Fajr, PrayerId.Dhuhr, PrayerId.Asr, PrayerId.Maghrib, PrayerId.Isha };

        for (var p = 0; p < prayers.Length; p++) {
            var prayer = prayers[p];
            var overrideSettings = FindOverride(notificationSettings.PrayerOverrides, prayer);
            var soundKey = overrideSettings?.SoundKey ?? notificationSettings.SoundKey;
            var effectiveSoundKey = AdhanSoundLibrary.ResolveEffectiveSoundKey(soundKey);
            var isSilent = AdhanSoundLibrary.IsSilent(effectiveSoundKey);
            var useRuntimeAdhanPlayback = ShouldUseRuntimeAdhanPlayback();
            var notificationSound = ResolveSystemNotificationSound(notificationSettings, effectiveSoundKey, isSilent);
            var baseTime = day.Timings.Get(prayer);
            for (var i = 0; i < offsets.Count; i++) {
                var notifyTime = baseTime.AddMinutes(offsets[i]);
                if (!ShouldSchedule(notifyTime)) {
                    continue;
                }

                var request = new NotificationRequest {
                    NotificationId = BuildReminderId(day.Date, 20 + p, i),
                    Title = LocalizationManager.Translate("AdhanReminder"),
                    Description = LocalizationManager.TranslatePrayer(prayer),
                    Silent = ResolveNotificationSilent(useRuntimeAdhanPlayback, isSilent),
                    Sound = useRuntimeAdhanPlayback ? string.Empty : notificationSound ?? string.Empty,
                    ReturningData = isSilent
                        ? string.Empty
                        : AdhanNotificationPayload.BuildPlay(prayer, effectiveSoundKey),
                    Schedule = new NotificationRequestSchedule {
                        NotifyTime = ToLocalKind(notifyTime),
                        NotifyRepeatInterval = null
#if ANDROID
                        ,
                        Android = new AndroidScheduleOptions {
                            AlarmType = AndroidAlarmType.RtcWakeup
                        }
#endif
                    },
#if ANDROID
                    Android = new AndroidOptions {
                        Priority = AndroidPriority.Default,
                        ChannelId = BuildAndroidChannelId(effectiveSoundKey, isSilent, useRuntimeAdhanPlayback),
                        VibrationPattern = isSilent ? Array.Empty<long>() : BuildVibration(notificationSettings, overrideSettings?.EnableVibration)
                    }
#endif
                };
                requests.Add(request);
            }
        }
    }

    private async Task EmitRequestsAsync(IReadOnlyList<NotificationRequest> requests, AppSettings settings) {
        if (requests.Count == 0) {
            return;
        }

        if (OperatingSystem.IsWindows()) {
            var now = DateTime.Now.AddMinutes(2);
            var next = requests
                .Where(item => item.Schedule?.NotifyTime > now)
                .OrderBy(item => item.Schedule!.NotifyTime)
                .FirstOrDefault();
            if (next == null) {
                return;
            }

            await LocalNotificationCenter.Current.Show(next);
            return;
        }

#if ANDROID
        EnsureAndroidChannels(requests);
#endif

        foreach (var request in requests) {
#if ANDROID
            if (request.Android == null) {
                request.Android = new AndroidOptions {
                    Priority = AndroidPriority.Default,
                    ChannelId = "prayer_default",
                    VibrationPattern = BuildVibration(settings.Notifications)
                };
            }
#endif
            await LocalNotificationCenter.Current.Show(request);
        }
    }

    private static string? ResolveSystemNotificationSound(NotificationSettings settings, string soundKey, bool isSilent) {
        if (isSilent) {
            return null;
        }

        var sound = AdhanSoundLibrary.ResolveNotificationSound(settings, soundKey);
        if (string.IsNullOrWhiteSpace(sound)) {
            return null;
        }

        if (sound.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) >= 0) {
            return AdhanSoundLibrary.ResolveNotificationSound(settings, AdhanSoundLibrary.DefaultBuiltinKey);
        }

        return sound;
    }

    private static string BuildAndroidChannelId(string soundKey, bool isSilent, bool useRuntimeAdhanPlayback) {
        if (useRuntimeAdhanPlayback) {
            return "prayer_runtime_media_v2";
        }

        if (isSilent) {
            return "prayer_silent_v2";
        }

        return AdhanSoundLibrary.BuildChannelId(soundKey);
    }

    private static bool ShouldUseRuntimeAdhanPlayback() {
        return OperatingSystem.IsAndroid() || OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst();
    }

    private static bool ResolveNotificationSilent(bool useRuntimeAdhanPlayback, bool isSilent) {
#if ANDROID
        _ = useRuntimeAdhanPlayback;
        _ = isSilent;
        return false;
#else
        return useRuntimeAdhanPlayback || isSilent;
#endif
    }

#if ANDROID
    private static void EnsureAndroidChannels(IReadOnlyList<NotificationRequest> requests) {
        var channels = requests
            .Select(item => new {
                ChannelId = item.Android?.ChannelId,
                Sound = item.Sound,
                Vibration = item.Android?.VibrationPattern
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.ChannelId))
            .GroupBy(item => item.ChannelId!, StringComparer.OrdinalIgnoreCase)
            .Select(group => {
                var sample = group.First();
                var vibrationPattern = group
                    .Select(item => item.Vibration ?? Array.Empty<long>())
                    .OrderByDescending(pattern => pattern.Length)
                    .FirstOrDefault() ?? Array.Empty<long>();
                var soundFile = ResolveAndroidChannelSound(sample.Sound);
                var isSilentChannel = IsAndroidSilentChannel(group.Key);
                return new NotificationChannelRequest {
                    Id = group.Key,
                    Name = "Prayer Alerts",
                    Description = "Prayer and adhan notifications",
                    Importance = AndroidImportance.High,
                    EnableVibration = vibrationPattern.Length > 0,
                    VibrationPattern = vibrationPattern,
                    EnableSound = !isSilentChannel && !string.IsNullOrWhiteSpace(soundFile),
                    Sound = isSilentChannel ? string.Empty : soundFile ?? string.Empty
                };
            })
            .ToList();

        if (channels.Count == 0) {
            return;
        }

        LocalNotificationCenter.CreateNotificationChannels(channels);
    }

    private static string? ResolveAndroidChannelSound(string? sound) {
        if (string.IsNullOrWhiteSpace(sound)) {
            return null;
        }

        return Path.GetFileName(sound);
    }

    private static bool IsAndroidSilentChannel(string channelId) {
        return channelId.StartsWith("prayer_silent", StringComparison.OrdinalIgnoreCase)
            || channelId.StartsWith("prayer_runtime_media", StringComparison.OrdinalIgnoreCase);
    }
#endif

    private static AdhanPrayerOverride? FindOverride(IReadOnlyList<AdhanPrayerOverride> overrides, PrayerId prayer) {
        if (overrides == null || overrides.Count == 0) {
            return null;
        }

        for (var i = 0; i < overrides.Count; i++) {
            if (overrides[i].Prayer == prayer) {
                return overrides[i];
            }
        }

        return null;
    }
}
