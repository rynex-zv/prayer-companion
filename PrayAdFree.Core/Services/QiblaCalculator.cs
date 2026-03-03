namespace PrayAdFree.Core.Services;

public static class QiblaCalculator {
    private const double KaabaLatitude = 21.422515;
    private const double KaabaLongitude = 39.826187;

    public static double CalculateBearing(double latitude, double longitude) {
        var latRad = DegreesToRadians(latitude);
        var lonRad = DegreesToRadians(longitude);
        var kaabaLat = DegreesToRadians(KaabaLatitude);
        var kaabaLon = DegreesToRadians(KaabaLongitude);

        var dLon = kaabaLon - lonRad;
        var x = Math.Sin(dLon) * Math.Cos(kaabaLat);
        var y = Math.Cos(latRad) * Math.Sin(kaabaLat) - Math.Sin(latRad) * Math.Cos(kaabaLat) * Math.Cos(dLon);
        var bearing = Math.Atan2(x, y);
        return (RadiansToDegrees(bearing) + 360) % 360;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;
}
