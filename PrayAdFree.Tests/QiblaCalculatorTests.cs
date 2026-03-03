using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public class QiblaCalculatorTests {
    [Fact]
    public void CalculateBearing_ReturnsValidRange() {
        var bearing = QiblaCalculator.CalculateBearing(24.7136, 46.6753);
        Assert.InRange(bearing, 0, 360);
    }
}
