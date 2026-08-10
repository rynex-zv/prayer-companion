namespace PrayAdFree.Core.Services;

/// <summary>
/// Validates values received across the typed application boundary. Invalid
/// values are defects and must never be silently replaced with another value.
/// </summary>
public static class AppInputContract {
    public static string RequiredChoice(string? value, string field, params string[] allowed) {
        if (!string.IsNullOrWhiteSpace(value)) {
            var match = allowed.FirstOrDefault(item =>
                string.Equals(item, value, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }

        throw new ArgumentException(
            $"Invalid {field}: '{value ?? "<missing>"}'. Expected one of: {string.Join(", ", allowed)}.",
            field);
    }

    public static TEnum RequiredEnum<TEnum>(TEnum value, string field) where TEnum : struct, Enum {
        if (Enum.IsDefined(value)) return value;
        throw new ArgumentOutOfRangeException(field, value, $"Invalid {field} value.");
    }

    public static int RequiredIndex(string? value, int count, string field) {
        if (int.TryParse(value, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var index) &&
            index >= 0 && index < count) return index;

        throw new ArgumentOutOfRangeException(field, value,
            $"Invalid {field}: '{value ?? "<missing>"}'. Expected an index from 0 to {Math.Max(0, count - 1)}.");
    }
}
