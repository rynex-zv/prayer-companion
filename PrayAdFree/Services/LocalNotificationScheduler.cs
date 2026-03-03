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

        foreach (var day in days) {
            var schedule = _planner.BuildSchedule(day, settings.Notifications);
            foreach (var item in schedule) {
                if (item.Time <= DateTime.Now) {
                    continue;
                }

                var request = new NotificationRequest {
                    NotificationId = BuildId(day.Date, item.Prayer),
                    Title = $"Prayer time: {item.Prayer}",
                    Description = $"It is time for {item.Prayer}",
                    Schedule = new NotificationRequestSchedule {
                        NotifyTime = item.Time,
                        NotifyRepeatInterval = null
                    }
                };

#if ANDROID
                request.Android = new AndroidOptions {
                    Priority = AndroidPriority.High,
                    ChannelId = "prayer_times",
                    VibrationPattern = settings.Notifications.EnableVibration ? new long[] { 0, 400, 200, 400 } : Array.Empty<long>()
                };
#endif

                await LocalNotificationCenter.Current.Show(request);
            }

            await ScheduleFastingRemindersAsync(day, settings);
        }
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

    private async Task ScheduleFastingRemindersAsync(PrayerDay day, AppSettings settings) {
        var baseImsak = day.Timings.Imsak.AddMinutes(-settings.FastingOffsets.ImsakAdvanceMinutes);
        var baseIftar = day.Timings.Maghrib.AddMinutes(settings.FastingOffsets.IftarDelayMinutes);

        var imsakReminders = settings.FastingReminders.ImsakRemindersMinutes.Distinct().OrderBy(item => item).ToList();
        for (var i = 0; i < imsakReminders.Count; i++) {
            var time = baseImsak.AddMinutes(imsakReminders[i]);
            if (!ShouldSchedule(time)) {
                continue;
            }

            var request = new NotificationRequest {
                NotificationId = BuildReminderId(day.Date, 8, i),
                Title = LocalizationManager.Translate("ImsakReminder"),
                Description = LocalizationManager.Translate("Imsak"),
                Schedule = new NotificationRequestSchedule {
                    NotifyTime = time,
                    NotifyRepeatInterval = null
                }
            };
#if ANDROID
            request.Android = new AndroidOptions {
                Priority = AndroidPriority.Default,
                ChannelId = "prayer_times",
                VibrationPattern = settings.Notifications.EnableVibration ? new long[] { 0, 300, 200, 300 } : Array.Empty<long>()
            };
#endif
            await LocalNotificationCenter.Current.Show(request);
        }

        var iftarReminders = settings.FastingReminders.IftarRemindersMinutes.Distinct().OrderBy(item => item).ToList();
        for (var i = 0; i < iftarReminders.Count; i++) {
            var time = baseIftar.AddMinutes(iftarReminders[i]);
            if (!ShouldSchedule(time)) {
                continue;
            }

            var request = new NotificationRequest {
                NotificationId = BuildReminderId(day.Date, 9, i),
                Title = LocalizationManager.Translate("IftarReminder"),
                Description = LocalizationManager.Translate("Iftar"),
                Schedule = new NotificationRequestSchedule {
                    NotifyTime = time,
                    NotifyRepeatInterval = null
                }
            };
#if ANDROID
            request.Android = new AndroidOptions {
                Priority = AndroidPriority.Default,
                ChannelId = "prayer_times",
                VibrationPattern = settings.Notifications.EnableVibration ? new long[] { 0, 300, 200, 300 } : Array.Empty<long>()
            };
#endif
            await LocalNotificationCenter.Current.Show(request);
        }
    }
}
