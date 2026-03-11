using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public static class LocationUpdatePolicy {
    public static bool ShouldThrottleGpsRefresh(DateTime nowUtc, DateTime lastRefreshUtc, TimeSpan minInterval, bool forceRefresh) {
        if (forceRefresh) {
            return false;
        }

        if (lastRefreshUtc == DateTime.MinValue) {
            return false;
        }

        return nowUtc - lastRefreshUtc < minInterval;
    }

    public static bool HasMeaningfulLocationChange(
        LocationSettings current,
        LocationSettings updated,
        double movementThresholdMeters,
        out double distanceMeters) {
        distanceMeters = 0;

        if (current.Mode != updated.Mode) {
            return true;
        }

        if (!TextEquals(current.City, updated.City) ||
            !TextEquals(current.Country, updated.Country) ||
            !TextEquals(current.CountryCode, updated.CountryCode) ||
            !TextEquals(current.TimeZoneId, updated.TimeZoneId)) {
            return true;
        }

        var currentHasCoordinates = HasValidCoordinates(current.Latitude, current.Longitude);
        var updatedHasCoordinates = HasValidCoordinates(updated.Latitude, updated.Longitude);

        if (currentHasCoordinates != updatedHasCoordinates) {
            return true;
        }

        if (!currentHasCoordinates || !updatedHasCoordinates) {
            return false;
        }

        distanceMeters = HaversineMeters(
            current.Latitude,
            current.Longitude,
            updated.Latitude,
            updated.Longitude);

        return distanceMeters > movementThresholdMeters;
    }

    private static bool TextEquals(string? a, string? b) {
        return string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasValidCoordinates(double latitude, double longitude) {
        return latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180 &&
               !(Math.Abs(latitude) < double.Epsilon && Math.Abs(longitude) < double.Epsilon);
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2) {
        const double radiusMeters = 6_371_000;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return radiusMeters * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
}
