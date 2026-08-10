using System.Globalization;
using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public static class CalculationMethodPresetCatalog {
    // Only expose methods that this engine can represent completely. Jafari and
    // Tehran require a Maghrib solar angle, which Adhan .NET 0.9 cannot model.
    // Hiding them is deliberate: silently calculating Maghrib at sunset would
    // produce a materially different method under a trusted name.
    public static IReadOnlyList<CalculationMethod> SupportedMethods { get; } = new[] {
        CalculationMethod.Auto,
        CalculationMethod.Karachi,
        CalculationMethod.Isna,
        CalculationMethod.MuslimWorldLeague,
        CalculationMethod.UmmAlQura,
        CalculationMethod.Egypt,
        CalculationMethod.Gulf,
        CalculationMethod.Kuwait,
        CalculationMethod.Qatar,
        CalculationMethod.Singapore,
        CalculationMethod.France,
        CalculationMethod.Turkey,
        CalculationMethod.Russia,
        CalculationMethod.Moonsighting,
        CalculationMethod.Dubai,
        CalculationMethod.Jakim,
        CalculationMethod.Tunisia,
        CalculationMethod.Algeria,
        CalculationMethod.Kemenag,
        CalculationMethod.Morocco,
        CalculationMethod.Portugal,
        CalculationMethod.Jordan,
        CalculationMethod.Custom
    };

    private static readonly IReadOnlyDictionary<CalculationMethod, SunAnglePreset> Presets =
        new Dictionary<CalculationMethod, SunAnglePreset> {
            [CalculationMethod.Jafari] = new("16", "14"),
            [CalculationMethod.Karachi] = new("18", "18"),
            [CalculationMethod.Isna] = new("15", "15"),
            [CalculationMethod.MuslimWorldLeague] = new("18", "17"),
            [CalculationMethod.UmmAlQura] = new("18.5", "90 min"),
            [CalculationMethod.Egypt] = new("19.5", "17.5"),
            [CalculationMethod.Tehran] = new("17.7", "14"),
            [CalculationMethod.Gulf] = new("19.5", "90 min"),
            [CalculationMethod.Kuwait] = new("18", "17.5"),
            [CalculationMethod.Qatar] = new("18", "90 min"),
            [CalculationMethod.Singapore] = new("20", "18"),
            [CalculationMethod.France] = new("12", "12"),
            [CalculationMethod.Turkey] = new("18", "17"),
            [CalculationMethod.Russia] = new("16", "15"),
            [CalculationMethod.Moonsighting] = new("18", "General shafaq"),
            [CalculationMethod.Dubai] = new("18.2", "18.2"),
            [CalculationMethod.Jakim] = new("20", "18"),
            [CalculationMethod.Tunisia] = new("18", "18"),
            [CalculationMethod.Algeria] = new("18", "17"),
            [CalculationMethod.Kemenag] = new("20", "18"),
            [CalculationMethod.Morocco] = new("19", "17"),
            [CalculationMethod.Portugal] = new("18", "77 min"),
            [CalculationMethod.Jordan] = new("18", "18")
        };

    public static SunAnglePreset ResolvePreset(AppSettings settings) {
        var method = settings.Method == CalculationMethod.Auto
            ? MethodResolver.ResolveRequired(settings.Location.CountryCode)
            : settings.Method;

        if (method == CalculationMethod.Custom) {
            return new SunAnglePreset(
                FormatAngle(settings.SunAngles.Fajr),
                FormatAngle(settings.SunAngles.Isha));
        }

        return Presets.TryGetValue(method, out var preset)
            ? preset
            : throw new InvalidOperationException($"No verified display preset exists for calculation method '{method}'.");
    }

    private static string FormatAngle(double value) {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    public sealed class SunAnglePreset {
        public SunAnglePreset(string fajr, string isha) {
            Fajr = fajr;
            Isha = isha;
        }

        public string Fajr { get; }
        public string Isha { get; }
    }
}
