using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public class PrayerTimesServiceCacheKeyTests {
    [Fact]
    public void BuildCacheKey_ChangesWhenCustomSunAnglesChange() {
        var baseSettings = new AppSettings {
            Location = new LocationSettings {
                Latitude = 24.0,
                Longitude = 46.0,
                CountryCode = "SA"
            },
            Method = CalculationMethod.Custom,
            SunAngles = new SunAngleSettings {
                Fajr = 18.5,
                Isha = 17.5
            }
        };
        var updatedSettings = new AppSettings {
            Location = baseSettings.Location,
            Method = baseSettings.Method,
            SunAngles = new SunAngleSettings {
                Fajr = 19.0,
                Isha = 17.5
            }
        };

        var baseKey = PrayerTimesService.BuildCacheKey(baseSettings, 2025, 1);
        var updatedKey = PrayerTimesService.BuildCacheKey(updatedSettings, 2025, 1);

        Assert.NotEqual(baseKey, updatedKey);
    }
}
