using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public static class MethodResolver {
    private static readonly Dictionary<string, CalculationMethod> CountryMethods = new() {
        ["SA"] = CalculationMethod.UmmAlQura,
        ["AE"] = CalculationMethod.Gulf,
        ["KW"] = CalculationMethod.Kuwait,
        ["QA"] = CalculationMethod.Qatar,
        ["TR"] = CalculationMethod.Turkey,
        ["EG"] = CalculationMethod.Egypt,
        ["PK"] = CalculationMethod.Karachi,
        ["IN"] = CalculationMethod.Karachi,
        ["US"] = CalculationMethod.Isna,
        ["CA"] = CalculationMethod.Isna,
        ["GB"] = CalculationMethod.MuslimWorldLeague,
        ["FR"] = CalculationMethod.MuslimWorldLeague,
        ["DE"] = CalculationMethod.MuslimWorldLeague
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
