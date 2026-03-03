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
            Location = new LocationSettings { City = "Cairo", Country = "Egypt" }
        };

        service.Save(settings);
        var loaded = service.Load();

        Assert.Equal(CalculationMethod.Egypt, loaded.Method);
        Assert.Equal(Madhhab.Hanafi, loaded.Madhhab);
        Assert.Equal("Cairo", loaded.Location.City);
    }
}
