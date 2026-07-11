using System.Globalization;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace Pray_Ad_Free.Services;

public static class LocalizationManager {
    public static string CurrentLanguage { get; private set; } = "en";
    public static event EventHandler? LanguageChanged;

    public static void EnsureInitialized(string? language) => SetLanguage(language);

    public static Task InitializeAsync(string? language) {
        SetLanguage(language);
        return Task.CompletedTask;
    }

    public static void SetLanguage(string? language) {
        var requested = ResolveLanguage(language);
        if (string.Equals(CurrentLanguage, requested, StringComparison.Ordinal)) {
            ApplyCulture(requested);
            return;
        }

        CurrentLanguage = requested;
        ApplyCulture(requested);
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static IReadOnlyList<LanguageOption> GetAvailableLanguages() => WebCatalog.Languages
        .Select(item => new LanguageOption { Code = item.Code, Name = item.Name })
        .ToArray();

    public static string Translate(string key) => WebCatalog.Translate(CurrentLanguage, key);

    public static string TranslatePrayer(PrayerId prayer) => Translate($"prayer_{prayer}");

    private static string ResolveLanguage(string? language) {
        if (string.IsNullOrWhiteSpace(language) || string.Equals(language, "auto", StringComparison.OrdinalIgnoreCase)) {
            var device = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return WebCatalog.Languages.Any(item => string.Equals(item.Code, device, StringComparison.Ordinal))
                ? device
                : "en";
        }

        return WebCatalog.NormalizeLanguage(language.Trim().ToLowerInvariant());
    }

    private static void ApplyCulture(string language) {
        var culture = CultureInfo.GetCultureInfo(language);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}

public sealed class LanguageOption {
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}
