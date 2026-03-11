using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public sealed class LocationUpdatePolicyTests {
    [Fact]
    public void ShouldThrottleGpsRefresh_WithinInterval_ReturnsTrue() {
        var now = new DateTime(2026, 3, 11, 12, 0, 0, DateTimeKind.Utc);
        var last = now.AddMinutes(-3);

        var result = LocationUpdatePolicy.ShouldThrottleGpsRefresh(now, last, TimeSpan.FromMinutes(15), forceRefresh: false);

        Assert.True(result);
    }

    [Fact]
    public void ShouldThrottleGpsRefresh_ForceRefresh_ReturnsFalse() {
        var now = new DateTime(2026, 3, 11, 12, 0, 0, DateTimeKind.Utc);
        var last = now.AddMinutes(-3);

        var result = LocationUpdatePolicy.ShouldThrottleGpsRefresh(now, last, TimeSpan.FromMinutes(15), forceRefresh: true);

        Assert.False(result);
    }

    [Fact]
    public void HasMeaningfulLocationChange_SameLocation_DoesNotChange() {
        var current = BuildLocation(52.3878, 4.9121);
        var updated = BuildLocation(52.3879, 4.9122);

        var changed = LocationUpdatePolicy.HasMeaningfulLocationChange(current, updated, 500, out var meters);

        Assert.False(changed);
        Assert.True(meters < 500);
    }

    [Fact]
    public void HasMeaningfulLocationChange_ChangedCity_IsMeaningful() {
        var current = BuildLocation(52.3878, 4.9121, city: "Amsterdam");
        var updated = BuildLocation(52.3878, 4.9121, city: "Utrecht");

        var changed = LocationUpdatePolicy.HasMeaningfulLocationChange(current, updated, 500, out _);

        Assert.True(changed);
    }

    [Fact]
    public void HasMeaningfulLocationChange_MovedMoreThanThreshold_IsMeaningful() {
        var current = BuildLocation(52.3878, 4.9121);
        var updated = BuildLocation(52.3980, 4.9121);

        var changed = LocationUpdatePolicy.HasMeaningfulLocationChange(current, updated, 500, out var meters);

        Assert.True(changed);
        Assert.True(meters > 500);
    }

    [Fact]
    public void HasMeaningfulLocationChange_InvalidToValidCoordinates_IsMeaningful() {
        var current = BuildLocation(0, 0);
        var updated = BuildLocation(52.3878, 4.9121);

        var changed = LocationUpdatePolicy.HasMeaningfulLocationChange(current, updated, 500, out _);

        Assert.True(changed);
    }

    private static LocationSettings BuildLocation(
        double latitude,
        double longitude,
        string city = "Amsterdam",
        string country = "Nederland",
        string countryCode = "NL",
        string timeZoneId = "W. Europe Standard Time",
        LocationMode mode = LocationMode.Gps) {
        return new LocationSettings {
            Mode = mode,
            City = city,
            Country = country,
            CountryCode = countryCode,
            Latitude = latitude,
            Longitude = longitude,
            TimeZoneId = timeZoneId,
            LastUpdatedUtc = DateTime.UtcNow
        };
    }
}
