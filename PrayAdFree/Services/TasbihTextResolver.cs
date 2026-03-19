using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Services;

public static class TasbihTextResolver {
    private static readonly Dictionary<string, string> LegacyTasbihValueMap = new(StringComparer.OrdinalIgnoreCase) {
        ["After prayer (33/33/34)"] = "TasbihPreset_AfterPrayer",
        ["100x Subhan Allah"] = "TasbihPreset_Hundred",
        ["100x Salawat"] = "TasbihPreset_Salawat",
        ["Subhan Allah"] = "Tasbih_SubhanAllah",
        ["Alhamdulillah"] = "Tasbih_Alhamdulillah",
        ["Allahu Akbar"] = "Tasbih_AllahuAkbar",
        ["La ilaha illa Allah"] = "Tasbih_LaIlahaIllaAllah",
        ["Astaghfirullah"] = "Tasbih_Astaghfirullah",
        ["La hawla wa la quwwata illa billah"] = "Tasbih_LaHawla",
        ["Salawat"] = "Tasbih_Salawat",
        ["New preset"] = "TasbihPreset_New"
    };

    public static string Translate(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return value;
        }

        var trimmed = value.Trim();
        if (LegacyTasbihValueMap.TryGetValue(trimmed, out var key)) {
            return LocalizationManager.Translate(key);
        }

        return LocalizationManager.Translate(trimmed);
    }

    public static string Translate(TasbihItemSettings item) {
        ArgumentNullException.ThrowIfNull(item);
        return Translate(item.Text);
    }
}
