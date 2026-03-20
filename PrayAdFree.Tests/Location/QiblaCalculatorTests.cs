using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public class QiblaCalculatorTests {
    [Fact]
    public void CalculateBearing_ReturnsValidRange() {
        var bearing = QiblaCalculator.CalculateBearing(24.7136, 46.6753);
        Assert.InRange(bearing, 0, 360);
    }

    [Fact]
    public void CreatePathToKaaba_ReturnsCurvedGeodesicPath() {
        var startLatitude = 51.5074;
        var startLongitude = -0.1278;
        var path = QiblaCalculator.CreatePathToKaaba(startLatitude, startLongitude, segments: 8);

        Assert.Equal(9, path.Count);
        Assert.Equal(startLatitude, path[0].Latitude, 3);
        Assert.Equal(startLongitude, path[0].Longitude, 3);
        Assert.Equal(QiblaCalculator.KaabaLatitudeDegrees, path[^1].Latitude, 3);
        Assert.Equal(QiblaCalculator.KaabaLongitudeDegrees, path[^1].Longitude, 3);

        var midPoint = path[path.Count / 2];
        var linearMidLatitude = (startLatitude + QiblaCalculator.KaabaLatitudeDegrees) / 2d;
        var linearMidLongitude = (startLongitude + QiblaCalculator.KaabaLongitudeDegrees) / 2d;

        Assert.True(
            Math.Abs(midPoint.Latitude - linearMidLatitude) > 0.1
            || Math.Abs(midPoint.Longitude - linearMidLongitude) > 0.1,
            "Expected a great-circle midpoint that differs from simple linear interpolation.");
    }
}
