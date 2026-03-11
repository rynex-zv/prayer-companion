using Microsoft.Maui;
using Microsoft.Maui.Graphics;
using PrayAdFree.Core.Models;
using Pray_Ad_Free.Models;
using Pray_Ad_Free.Resources.Styles;

namespace Pray_Ad_Free.Services;

public static class ThemeManager {
    private const int AccentCount = 20;
    private static ThemeBStyles? _themeBStyles;

    private static readonly IReadOnlyList<AccentOption> ThemeAAccents = new List<AccentOption> {
        new AccentOption(0, "Teal", "#0E6B61"),
        new AccentOption(1, "Lagoon", "#1E7C6E"),
        new AccentOption(2, "Palm", "#3A8D6B"),
        new AccentOption(3, "Olive", "#5B955B"),
        new AccentOption(4, "Herb", "#7E9A4C"),
        new AccentOption(5, "Moss", "#A3A54B"),
        new AccentOption(6, "Gold", "#C7A646"),
        new AccentOption(7, "Amber", "#E09F3E"),
        new AccentOption(8, "Copper", "#D9843B"),
        new AccentOption(9, "Terracotta", "#C66B37"),
        new AccentOption(10, "Clay", "#B05233"),
        new AccentOption(11, "Brick", "#9A4031"),
        new AccentOption(12, "Oxide", "#7F2F2E"),
        new AccentOption(13, "Mulberry", "#6A3F4B"),
        new AccentOption(14, "Plum", "#6A5266"),
        new AccentOption(15, "Slate", "#5B5E7A"),
        new AccentOption(16, "Harbor", "#4B6E8A"),
        new AccentOption(17, "Depth", "#3C7A8D"),
        new AccentOption(18, "Breeze", "#2F7F86"),
        new AccentOption(19, "Reef", "#1D7A74")
    };

    private static readonly IReadOnlyList<AccentOption> ThemeBAccents = new List<AccentOption> {
        new AccentOption(0, "Amber", "#D1AD3A"),
        new AccentOption(1, "Orange", "#F97316"),
        new AccentOption(2, "Red", "#EF4444"),
        new AccentOption(3, "Violet", "#A855F7"),
        new AccentOption(4, "Blue", "#3B82F6"),
        new AccentOption(5, "Emerald", "#22C55E"),
        new AccentOption(6, "Teal", "#2FB79D"),
        new AccentOption(7, "Mint", "#20C997"),
        new AccentOption(8, "Cyan", "#22D3EE"),
        new AccentOption(9, "Sky", "#38BDF8"),
        new AccentOption(10, "Indigo", "#6366F1"),
        new AccentOption(11, "Purple", "#8B5CF6"),
        new AccentOption(12, "Rose", "#F43F5E"),
        new AccentOption(13, "Pink", "#EC4899"),
        new AccentOption(14, "Coral", "#FB7185"),
        new AccentOption(15, "Lime", "#84CC16"),
        new AccentOption(16, "Olive", "#A3A23B"),
        new AccentOption(17, "Gold", "#EAB308"),
        new AccentOption(18, "Copper", "#F59E0B"),
        new AccentOption(19, "Slate", "#94A3B8")
    };

    public static IReadOnlyList<AccentOption> GetAccentOptions(ThemeVariant variant) {
        return variant == ThemeVariant.B ? ThemeBAccents : ThemeAAccents;
    }

    public static void ApplyTheme(AppSettings settings) {
        var resources = Application.Current?.Resources;
        if (resources == null) {
            return;
        }

        ApplyThemeStyles(resources, settings.ThemeVariant);

        Application.Current!.UserAppTheme = settings.ThemeMode switch {
            ThemeMode.Light => AppTheme.Light,
            ThemeMode.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };

        var accentOptions = GetAccentOptions(settings.ThemeVariant);
        var accent = accentOptions[Math.Clamp(settings.AccentIndex, 0, AccentCount - 1)].Hex;

        var light = settings.ThemeVariant == ThemeVariant.B ? ThemeBLight : ThemeALight;
        var dark = settings.ThemeVariant == ThemeVariant.B ? ThemeBDark : ThemeADark;

        SetColor(resources, "Primary", accent);
        SetColor(resources, "PrimaryDark", Darken(accent, 0.75));
        SetColor(resources, "Accent", accent);
        SetColor(resources, "Secondary", light.Secondary);
        SetBrush(resources, "PrimaryBrush", accent);
        SetBrush(resources, "SecondaryBrush", light.Secondary);
        SetBrush(resources, "AccentBrush", accent);

        SetColor(resources, "SkyTop", light.SkyTop);
        SetColor(resources, "SkyMid", light.SkyMid);
        SetColor(resources, "SkyBase", light.SkyBase);
        SetColor(resources, "SandTop", light.SandTop);
        SetColor(resources, "SandMid", light.SandMid);
        SetColor(resources, "SandBase", light.SandBase);
        SetColor(resources, "SurfaceGlass", light.SurfaceGlass);
        SetColor(resources, "SurfaceSolid", light.SurfaceSolid);
        SetColor(resources, "SurfaceHighlight", light.SurfaceHighlight);
        SetColor(resources, "TextMuted", light.TextMuted);
        SetColor(resources, "TextSubtle", light.TextSubtle);
        SetColor(resources, "NightTop", light.NightTop);
        SetColor(resources, "NightBase", light.NightBase);

        SetColor(resources, "DarkSkyTop", dark.SkyTop);
        SetColor(resources, "DarkSkyMid", dark.SkyMid);
        SetColor(resources, "DarkSkyBase", dark.SkyBase);
        SetColor(resources, "DarkSandTop", dark.SandTop);
        SetColor(resources, "DarkSandMid", dark.SandMid);
        SetColor(resources, "DarkSandBase", dark.SandBase);
        SetColor(resources, "SurfaceGlassDark", dark.SurfaceGlass);
        SetColor(resources, "SurfaceHighlightDark", dark.SurfaceHighlight);
        SetColor(resources, "TextMutedDark", dark.TextMuted);

        ApplyTextScale(resources, NormalizeTextScalePercent(settings.TextScale));
    }

    private static void ApplyThemeStyles(ResourceDictionary resources, ThemeVariant variant) {
        var merged = resources.MergedDictionaries;
        var existingThemeB = merged.Where(dictionary => dictionary is ThemeBStyles).ToList();

        if (variant == ThemeVariant.B) {
            _themeBStyles ??= new ThemeBStyles();
            if (!merged.Contains(_themeBStyles)) {
                merged.Add(_themeBStyles);
            }

            foreach (var dictionary in existingThemeB.Where(dictionary => !ReferenceEquals(dictionary, _themeBStyles))) {
                merged.Remove(dictionary);
            }
            return;
        }

        foreach (var dictionary in existingThemeB) {
            merged.Remove(dictionary);
        }
    }

    private static void SetColor(ResourceDictionary resources, string key, string hex) {
        resources[key] = Color.FromArgb(hex);
    }

    private static void SetBrush(ResourceDictionary resources, string key, string hex) {
        resources[key] = new SolidColorBrush(Color.FromArgb(hex));
    }

    private static string Darken(string hex, double factor) {
        var color = Color.FromArgb(hex);
        var r = (int)(color.Red * 255 * factor);
        var g = (int)(color.Green * 255 * factor);
        var b = (int)(color.Blue * 255 * factor);
        return $"#{Clamp(r):X2}{Clamp(g):X2}{Clamp(b):X2}";
    }

    private static int Clamp(int value) => Math.Clamp(value, 0, 255);

    public static int NormalizeTextScalePercent(int storedValue) {
        // Legacy values were -2..6; map them to previous displayed percentages.
        if (storedValue is >= -2 and <= 6) {
            return Math.Clamp(100 + (storedValue * 7), 10, 500);
        }

        return Math.Clamp(storedValue, 10, 500);
    }

    private static void ApplyTextScale(ResourceDictionary resources, int percent) {
        var factor = percent / 100d;
        resources["FontSizeBase"] = 14d * factor;
        resources["FontSizeSmall"] = 12d * factor;
        resources["FontSizeMedium"] = 16d * factor;
        resources["FontSizeLarge"] = 20d * factor;
        resources["FontSizeDisplay"] = 32d * factor;
        resources["FontSizeSubDisplay"] = 24d * factor;
    }

    private sealed record ThemeColors(
        string SkyTop,
        string SkyMid,
        string SkyBase,
        string SandTop,
        string SandMid,
        string SandBase,
        string SurfaceGlass,
        string SurfaceSolid,
        string SurfaceHighlight,
        string TextMuted,
        string TextSubtle,
        string NightTop,
        string NightBase,
        string Secondary);

    private static readonly ThemeColors ThemeALight = new(
        SkyTop: "#E8F1FF",
        SkyMid: "#DCE9F4",
        SkyBase: "#F6E2C4",
        SandTop: "#F7F1E6",
        SandMid: "#EBDCC6",
        SandBase: "#D8C2A2",
        SurfaceGlass: "#FFFDF7",
        SurfaceSolid: "#FFFFFF",
        SurfaceHighlight: "#FFEED6",
        TextMuted: "#6F6356",
        TextSubtle: "#8C7F70",
        NightTop: "#1C2630",
        NightBase: "#0C1218",
        Secondary: "#F2E9D8");

    private static readonly ThemeColors ThemeADark = new(
        SkyTop: "#0E141C",
        SkyMid: "#141C26",
        SkyBase: "#1B242F",
        SandTop: "#12161E",
        SandMid: "#1A212B",
        SandBase: "#202A35",
        SurfaceGlass: "#1B232D",
        SurfaceSolid: "#0E141C",
        SurfaceHighlight: "#24303B",
        TextMuted: "#B6B0A6",
        TextSubtle: "#C2BAB0",
        NightTop: "#141B24",
        NightBase: "#0B1116",
        Secondary: "#28313B");

    private static readonly ThemeColors ThemeBLight = new(
        SkyTop: "#0F4A3B",
        SkyMid: "#165A49",
        SkyBase: "#236A57",
        SandTop: "#0E2430",
        SandMid: "#122B37",
        SandBase: "#173340",
        SurfaceGlass: "#102531",
        SurfaceSolid: "#0F2130",
        SurfaceHighlight: "#1A3C43",
        TextMuted: "#8EA0B4",
        TextSubtle: "#73859A",
        NightTop: "#0A0F1E",
        NightBase: "#070C18",
        Secondary: "#244739");

    private static readonly ThemeColors ThemeBDark = new(
        SkyTop: "#0B4A3A",
        SkyMid: "#145B4B",
        SkyBase: "#1C6A57",
        SandTop: "#0E2430",
        SandMid: "#122B37",
        SandBase: "#173340",
        SurfaceGlass: "#102531",
        SurfaceSolid: "#0F2130",
        SurfaceHighlight: "#1A3C43",
        TextMuted: "#95A7BC",
        TextSubtle: "#7A8DA3",
        NightTop: "#090F1E",
        NightBase: "#060B17",
        Secondary: "#254A3B");
}
