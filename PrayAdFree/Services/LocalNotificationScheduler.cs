using System.Collections.Generic;
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

    public async Task ScheduleAsync(IEnumerable<PrayerDay> days, NotificationSettings settings, CancellationToken cancellationToken) {
        await LocalNotificationCenter.Current.RequestNotificationPermission();
        await CancelAsync();

        foreach (var day in days) {
            var schedule = _planner.BuildSchedule(day, settings);
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
                    VibrationPattern = settings.EnableVibration ? new long[] { 0, 400, 200, 400 } : Array.Empty<long>()
                };
#endif

                await LocalNotificationCenter.Current.Show(request);
            }
        }
    }

    public Task CancelAsync() {
        LocalNotificationCenter.Current.CancelAll();
        return Task.CompletedTask;
    }

    private static int BuildId(DateOnly date, PrayerId prayer) {
        return int.Parse($"{date:yyMMdd}{(int)prayer + 1}");
    }
}
