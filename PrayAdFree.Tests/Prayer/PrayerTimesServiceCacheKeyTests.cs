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

    [Fact]
    public void BuildCacheKey_ChangesForSubFourDecimalCoordinateAndTimezoneInputs() {
        var baseline = new AppSettings {
            Location = new LocationSettings { Latitude = 52.36760001, Longitude = 4.9041, TimeZoneId = "Europe/Amsterdam" }
        };
        var coordinate = new AppSettings {
            Location = new LocationSettings { Latitude = 52.36760002, Longitude = 4.9041, TimeZoneId = "Europe/Amsterdam" }
        };
        var timezone = new AppSettings {
            Location = new LocationSettings { Latitude = 52.36760001, Longitude = 4.9041, TimeZoneId = "UTC" }
        };

        var key = PrayerTimesService.BuildCacheKey(baseline, 2026, 7);
        Assert.NotEqual(key, PrayerTimesService.BuildCacheKey(coordinate, 2026, 7));
        Assert.NotEqual(key, PrayerTimesService.BuildCacheKey(timezone, 2026, 7));
    }
}
