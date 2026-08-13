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

    [Fact]
    public void Resolve_Iraq_UsesExplicitMuslimWorldLeagueMethod() {
        var method = MethodResolver.ResolveRequired("IQ");
        Assert.Equal(CalculationMethod.MuslimWorldLeague, method);
    }

    [Fact]
    public void Resolve_RepairsMissingCountryFromExplicitLocationTimeZone() {
        Assert.Equal(CalculationMethod.Dubai, MethodResolver.ResolveRequired("", "Asia/Dubai"));
        Assert.Equal(CalculationMethod.MuslimWorldLeague, MethodResolver.ResolveRequired(null, "Europe/Amsterdam"));
    }
}
