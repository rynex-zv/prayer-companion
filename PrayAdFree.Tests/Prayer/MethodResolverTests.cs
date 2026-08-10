using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests;

public class MethodResolverTests {
    [Fact]
    public void ResolveRequired_RejectsUnknownCountry() {
        Assert.Throws<ArgumentException>(() => MethodResolver.ResolveRequired("XX"));
    }

    [Fact]
    public void Resolve_ReturnsMappedMethod() {
        var method = MethodResolver.ResolveRequired("SA");
        Assert.Equal(CalculationMethod.UmmAlQura, method);
    }
}
