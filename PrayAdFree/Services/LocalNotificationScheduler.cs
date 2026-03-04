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

    public async Task ScheduleAsync(IEnumerable<PrayerDay> days, AppSettings settings, CancellationToken cancellationToken) {
        await LocalNotificationCenter.Current.RequestNotificationPermission();
        await CancelAsync();

        var requests = new List<NotificationRequest>();

        foreach (var day in days) {
            var schedule = _planner.BuildSchedule(day, settings.Notifications);
            foreach (var item in schedule) {
                if (item.Time <= DateTime.Now) {
                    continue;
                }

                var prayerName = LocalizationManager.TranslatePrayer(item.Prayer);
                requests.Add(new NotificationRequest {
                    NotificationId = BuildId(day.Date, item.Prayer),
                    Title = string.Format(LocalizationManager.Translate("Notification_PrayerTitle"), prayerName),
                    Description = string.Format(LocalizationManager.Translate("Notification_PrayerBody"), prayerName),
                    Schedule = new NotificationRequestSchedule {
                        NotifyTime = ToLocalKind(item.Time),
                        NotifyRepeatInterval = null
                    }
                });
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

    private static long[] BuildVibration(NotificationSettings settings) {
        if (!settings.EnableVibration) {
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
            var baseTime = day.Timings.Get(prayer);
            for (var i = 0; i < offsets.Count; i++) {
                var notifyTime = baseTime.AddMinutes(offsets[i]);
                if (!ShouldSchedule(notifyTime)) {
                    continue;
                }

                requests.Add(new NotificationRequest {
                    NotificationId = BuildReminderId(day.Date, 20 + p, i),
                    Title = LocalizationManager.Translate("AdhanReminder"),
                    Description = LocalizationManager.TranslatePrayer(prayer),
                    Schedule = new NotificationRequestSchedule {
                        NotifyTime = ToLocalKind(notifyTime),
                        NotifyRepeatInterval = null
                    }
                });
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

        foreach (var request in requests) {
#if ANDROID
            request.Android = new AndroidOptions {
                Priority = AndroidPriority.Default,
                ChannelId = "prayer_times",
                VibrationPattern = BuildVibration(settings.Notifications)
            };
#endif
            await LocalNotificationCenter.Current.Show(request);
        }
    }
}
