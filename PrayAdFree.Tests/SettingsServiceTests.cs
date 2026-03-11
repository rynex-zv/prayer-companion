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
                AdhanVolume = 0.35,
                PendingDeferredReminder = new DeferredAdhanReminder {
                    NotifyTime = new DateTime(2026, 3, 11, 12, 15, 0, DateTimeKind.Local),
                    Prayer = PrayerId.Asr,
                    SoundKey = "adhan_default"
                }
            }
        };

        service.Save(settings);
        var loaded = service.Load();

        Assert.Equal(CalculationMethod.Egypt, loaded.Method);
        Assert.Equal(Madhhab.Hanafi, loaded.Madhhab);
        Assert.Equal("Cairo", loaded.Location.City);
        Assert.Equal(0.35, loaded.Notifications.AdhanVolume, 3);
        Assert.NotNull(loaded.Notifications.PendingDeferredReminder);
        Assert.Equal(PrayerId.Asr, loaded.Notifications.PendingDeferredReminder!.Prayer);
    }
}
