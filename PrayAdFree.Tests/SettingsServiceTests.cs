using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public class SettingsServiceTests {
    [Fact]
    public void SaveAndLoad_RoundTrips() {
        var store = new InMemorySettingsStore();
        var service = new SettingsService(store);
        var settings = new AppSettings {
            Method = CalculationMethod.Egypt,
            Madhhab = Madhhab.Hanafi,
            Location = new LocationSettings { City = "Cairo", Country = "Egypt" },
            Notifications = new NotificationSettings {
                MobilePrimaryAdhanType = MobilePrimaryAdhanType.Alarm,
                AdhanVolume = 0.35,
                PendingDeferredReminder = new DeferredAdhanReminder {
                    NotifyTime = new DateTime(2026, 3, 11, 12, 15, 0, DateTimeKind.Local),
                    BasePrayerTime = new DateTime(2026, 3, 11, 12, 0, 0, DateTimeKind.Local),
                    Prayer = PrayerId.Asr,
                    SoundKey = "adhan_default",
                    OpenAlarmScreen = true
                }
            },
            AlarmReminders = new AlarmRemindersSettings {
                DisabledBuiltInIds = new List<string> { "khushu" },
                UserItems = new List<AlarmUserReminderItem> {
                    new() {
                        Id = "user_1",
                        Text = "Test dua",
                        IsEnabled = true
                    }
                }
            }
        };

        service.Save(settings);
        var loaded = service.Load();

        Assert.Equal(CalculationMethod.Egypt, loaded.Method);
        Assert.Equal(Madhhab.Hanafi, loaded.Madhhab);
        Assert.Equal("Cairo", loaded.Location.City);
        Assert.Equal(0.35, loaded.Notifications.AdhanVolume, 3);
        Assert.Equal(MobilePrimaryAdhanType.Alarm, loaded.Notifications.MobilePrimaryAdhanType);
        Assert.NotNull(loaded.Notifications.PendingDeferredReminder);
        Assert.Equal(PrayerId.Asr, loaded.Notifications.PendingDeferredReminder!.Prayer);
        Assert.Equal(new DateTime(2026, 3, 11, 12, 0, 0, DateTimeKind.Local), loaded.Notifications.PendingDeferredReminder!.BasePrayerTime);
        Assert.True(loaded.Notifications.PendingDeferredReminder!.OpenAlarmScreen);
        Assert.Contains("khushu", loaded.AlarmReminders.DisabledBuiltInIds);
        Assert.Contains(loaded.AlarmReminders.UserItems, item => item.Id == "user_1" && item.Text == "Test dua" && item.IsEnabled);
    }
}
