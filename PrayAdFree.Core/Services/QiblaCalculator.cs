namespace PrayAdFree.Core.Services;

public static class QiblaCalculator {
    private const double KaabaLatitude = 21.422515;
    private const double KaabaLongitude = 39.826187;

    public static double KaabaLatitudeDegrees => KaabaLatitude;

    public static double KaabaLongitudeDegrees => KaabaLongitude;

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

    public static IReadOnlyList<(double Latitude, double Longitude)> CreatePathToKaaba(
        double latitude,
        double longitude,
        int segments = 64) {
        return CreateGreatCirclePath(latitude, longitude, KaabaLatitude, KaabaLongitude, segments);
    }

    public static IReadOnlyList<(double Latitude, double Longitude)> CreateGreatCirclePath(
        double startLatitude,
        double startLongitude,
        double endLatitude,
        double endLongitude,
        int segments = 64) {
        if (segments < 1) {
            throw new ArgumentOutOfRangeException(nameof(segments), segments, "Segments must be at least 1.");
        }

        var start = ToUnitVector(startLatitude, startLongitude);
        var end = ToUnitVector(endLatitude, endLongitude);
        var dot = Math.Clamp((start.X * end.X) + (start.Y * end.Y) + (start.Z * end.Z), -1d, 1d);
        var omega = Math.Acos(dot);
        var sinOmega = Math.Sin(omega);
        var points = new List<(double Latitude, double Longitude)>(segments + 1);

        if (sinOmega < 1e-10) {
            points.Add((startLatitude, NormalizeLongitude(startLongitude)));
            points.Add((endLatitude, NormalizeLongitude(endLongitude)));
            return points;
        }

        for (var step = 0; step <= segments; step++) {
            var t = step / (double)segments;
            var scaleStart = Math.Sin((1d - t) * omega) / sinOmega;
            var scaleEnd = Math.Sin(t * omega) / sinOmega;

            var x = (scaleStart * start.X) + (scaleEnd * end.X);
            var y = (scaleStart * start.Y) + (scaleEnd * end.Y);
            var z = (scaleStart * start.Z) + (scaleEnd * end.Z);
            var length = Math.Sqrt((x * x) + (y * y) + (z * z));

            x /= length;
            y /= length;
            z /= length;

            var latitude = Math.Atan2(z, Math.Sqrt((x * x) + (y * y)));
            var longitude = Math.Atan2(y, x);
            points.Add((RadiansToDegrees(latitude), NormalizeLongitude(RadiansToDegrees(longitude))));
        }

        return points;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;

    private static (double X, double Y, double Z) ToUnitVector(double latitude, double longitude) {
        var latRad = DegreesToRadians(latitude);
        var lonRad = DegreesToRadians(longitude);
        var cosLat = Math.Cos(latRad);

        return (
            X: cosLat * Math.Cos(lonRad),
            Y: cosLat * Math.Sin(lonRad),
            Z: Math.Sin(latRad));
    }

    private static double NormalizeLongitude(double longitude) {
        var normalized = longitude % 360d;
        if (normalized > 180d) {
            normalized -= 360d;
        } else if (normalized < -180d) {
            normalized += 360d;
        }

        return normalized;
    }
}
