using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Services;

public static class TasbihTextResolver {
    public static string Translate(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return value;
        }

        var trimmed = value.Trim();
        return IsCatalogKey(trimmed)
            ? LocalizationManager.Translate(trimmed)
            : trimmed;
    }

    public static string Translate(TasbihItemSettings item) {
        ArgumentNullException.ThrowIfNull(item);
        return Translate(item.Text);
    }

    private static bool IsCatalogKey(string value) {
        return value.StartsWith("Tasbih_", StringComparison.Ordinal)
            || value.StartsWith("TasbihPreset_", StringComparison.Ordinal);
    }
}
