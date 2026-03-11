using Microsoft.Maui;
using Microsoft.Maui.Graphics;
using PrayAdFree.Core.Models;
using Pray_Ad_Free.Models;

namespace Pray_Ad_Free.Services;

public static class ThemeManager {
    private const int AccentCount = 20;

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
        new AccentOption(0, "Ocean", "#1B6EA5"),
        new AccentOption(1, "Wave", "#1E7FB0"),
        new AccentOption(2, "Lagoon", "#2B8CB8"),
        new AccentOption(3, "Harbor", "#3A97B0"),
        new AccentOption(4, "Glacier", "#4AA3A6"),
        new AccentOption(5, "Meadow", "#5FAF9D"),
        new AccentOption(6, "Mist", "#74B892"),
        new AccentOption(7, "Sage", "#8BC184"),
        new AccentOption(8, "Lime", "#A3C972"),
        new AccentOption(9, "Pollen", "#BDC059"),
        new AccentOption(10, "Saffron", "#D2A94A"),
        new AccentOption(11, "Coral", "#E08A4F"),
        new AccentOption(12, "Flare", "#E06C5B"),
        new AccentOption(13, "Blush", "#D4536E"),
        new AccentOption(14, "Rose", "#B9487A"),
        new AccentOption(15, "Iris", "#9B3F86"),
        new AccentOption(16, "Dusk", "#7C3E8A"),
        new AccentOption(17, "Indigo", "#5B4A8A"),
        new AccentOption(18, "Rain", "#3F5685"),
        new AccentOption(19, "Deep", "#2C6286")
    };

    public static IReadOnlyList<AccentOption> GetAccentOptions(ThemeVariant variant) {
        return variant == ThemeVariant.B ? ThemeBAccents : ThemeAAccents;
    }

    public static void ApplyTheme(AppSettings settings) {
        var resources = Application.Current?.Resources;
        if (resources == null) {
            return;
        }

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
        SkyTop: "#E7F3F8",
        SkyMid: "#D7E9F1",
        SkyBase: "#CFE2EB",
        SandTop: "#F1F5F7",
        SandMid: "#E1E8EE",
        SandBase: "#C8D3DD",
        SurfaceGlass: "#F6FAFC",
        SurfaceSolid: "#FFFFFF",
        SurfaceHighlight: "#E6F2FA",
        TextMuted: "#5C6975",
        TextSubtle: "#748392",
        NightTop: "#15202B",
        NightBase: "#0B1118",
        Secondary: "#E1E8EE");

    private static readonly ThemeColors ThemeBDark = new(
        SkyTop: "#0C141B",
        SkyMid: "#111D27",
        SkyBase: "#182531",
        SandTop: "#0F1720",
        SandMid: "#161F2A",
        SandBase: "#1D2834",
        SurfaceGlass: "#182330",
        SurfaceSolid: "#0C141B",
        SurfaceHighlight: "#223043",
        TextMuted: "#A9B5C2",
        TextSubtle: "#B7C2CD",
        NightTop: "#101722",
        NightBase: "#0A1016",
        Secondary: "#24303C");
}
