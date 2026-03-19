using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public class MethodResolverTests {
    [Fact]
    public void Resolve_ReturnsFallback_WhenUnknown() {
        var method = MethodResolver.Resolve("XX", CalculationMethod.Egypt);
        Assert.Equal(CalculationMethod.Egypt, method);
    }

    [Fact]
    public void Resolve_ReturnsMappedMethod() {
        var method = MethodResolver.Resolve("SA", CalculationMethod.MuslimWorldLeague);
        Assert.Equal(CalculationMethod.UmmAlQura, method);
    }
}
