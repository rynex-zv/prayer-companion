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
        // Iraq is an explicit regional mapping, not a generic fallback. The
        // shared MWL parameters (Fajr 18°, Isha 17°) are used by the
        // published Baghdad, Mosul, Erbil, Karbala and Nasiriyah schedules.
        ["IQ"] = CalculationMethod.MuslimWorldLeague,
        ["PK"] = CalculationMethod.Karachi,
        ["IN"] = CalculationMethod.Karachi,
        ["US"] = CalculationMethod.Isna,
        ["CA"] = CalculationMethod.Isna,
        ["GB"] = CalculationMethod.MuslimWorldLeague,
        ["NL"] = CalculationMethod.MuslimWorldLeague,
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

    private static readonly Dictionary<string, CalculationMethod> TimeZoneMethods = new(StringComparer.OrdinalIgnoreCase) {
        ["Asia/Dubai"] = CalculationMethod.Dubai,
        ["Asia/Baghdad"] = CalculationMethod.MuslimWorldLeague,
        ["Europe/Amsterdam"] = CalculationMethod.MuslimWorldLeague
    };

    public static CalculationMethod ResolveRequired(string? countryCode, string? timeZoneId = null) {
        if (string.IsNullOrWhiteSpace(countryCode)) {
            if (!string.IsNullOrWhiteSpace(timeZoneId) && TimeZoneMethods.TryGetValue(timeZoneId.Trim(), out var timeZoneMethod)) {
                return timeZoneMethod;
            }
            throw new ArgumentException(
                "Automatic calculation requires a country code or a recognized location time zone. Select a location or choose a calculation method explicitly.",
                nameof(countryCode));
        }

        if (CountryMethods.TryGetValue(countryCode.Trim().ToUpperInvariant(), out var method)) return method;
        throw new ArgumentException(
            $"No automatic calculation method is configured for country code '{countryCode}'. Choose a calculation method explicitly.",
            nameof(countryCode));
    }
}
