using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public static class MethodResolver {
    private static readonly Dictionary<string, CalculationMethod> CountryMethods = new() {
        ["SA"] = CalculationMethod.UmmAlQura,
        ["AE"] = CalculationMethod.Dubai,
        ["KW"] = CalculationMethod.Kuwait,
        ["QA"] = CalculationMethod.Qatar,
        ["TR"] = CalculationMethod.Turkey,
        ["EG"] = CalculationMethod.Egypt,
        ["PK"] = CalculationMethod.Karachi,
        ["IN"] = CalculationMethod.Karachi,
        ["US"] = CalculationMethod.Isna,
        ["CA"] = CalculationMethod.Isna,
        ["GB"] = CalculationMethod.MuslimWorldLeague,
        ["FR"] = CalculationMethod.France,
        ["DE"] = CalculationMethod.MuslimWorldLeague,
        ["RU"] = CalculationMethod.Russia,
        ["MY"] = CalculationMethod.Jakim,
        ["ID"] = CalculationMethod.Kemenag,
        ["TN"] = CalculationMethod.Tunisia,
        ["DZ"] = CalculationMethod.Algeria,
        ["MA"] = CalculationMethod.Morocco,
        ["PT"] = CalculationMethod.Portugal,
        ["JO"] = CalculationMethod.Jordan
    };

    public static CalculationMethod Resolve(string countryCode, CalculationMethod fallback) {
        if (string.IsNullOrWhiteSpace(countryCode)) {
            return fallback;
        }

        return CountryMethods.TryGetValue(countryCode.ToUpperInvariant(), out var method)
            ? method
            : fallback;
    }
}
