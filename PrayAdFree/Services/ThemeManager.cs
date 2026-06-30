using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using PrayAdFree.Core.Models;
using Pray_Ad_Free.Models;
using Pray_Ad_Free.Resources.Styles;
using System.Globalization;
#if ANDROID
using Android.Util;
using AToolbar = AndroidX.AppCompat.Widget.Toolbar;
using Google.Android.Material.BottomNavigation;
using ASwitchCompat = AndroidX.AppCompat.Widget.SwitchCompat;
using ATextView = Android.Widget.TextView;
using AView = Android.Views.View;
using AViewGroup = Android.Views.ViewGroup;
#endif

namespace Pray_Ad_Free.Services;

public static class ThemeManager {
    private const int AccentCount = 20;
    private static ThemeBStyles? _themeBStyles;
    private static int _activeTextScalePercent = 100;
    private static double _activeTextScaleFactor = 1d;
    private static double _activeIconScaleFactor = 2d;
    private static readonly BindableProperty UnscaledFontSizeProperty = BindableProperty.CreateAttached(
        "UnscaledFontSize",
        typeof(double),
        typeof(ThemeManager),
        0d);
    private static readonly BindableProperty UnscaledIconWidthProperty = BindableProperty.CreateAttached(
        "UnscaledIconWidth",
        typeof(double),
        typeof(ThemeManager),
        0d);
    private static readonly BindableProperty UnscaledIconHeightProperty = BindableProperty.CreateAttached(
        "UnscaledIconHeight",
        typeof(double),
        typeof(ThemeManager),
        0d);
    private static readonly BindableProperty UnscaledIconFontSizeProperty = BindableProperty.CreateAttached(
        "UnscaledIconFontSize",
        typeof(double),
        typeof(ThemeManager),
        0d);

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

    public static IReadOnlyList<AccentOption> GetAccentOptions() {
        return ThemeBAccents;
    }

    public static void ApplyTheme(AppSettings settings) {
        var resources = Application.Current?.Resources;
        if (resources == null) {
            return;
        }

        ApplyThemeStyles(resources);

        Application.Current!.UserAppTheme = settings.ThemeMode switch {
            ThemeMode.Light => AppTheme.Light,
            ThemeMode.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };

        var accentOptions = GetAccentOptions();
        var accent = accentOptions[Math.Clamp(settings.AccentIndex, 0, AccentCount - 1)].Hex;

        var light = ThemeBLight;
        var dark = ThemeBDark;

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

        var useDarkTokens = UseDarkTokens(settings.ThemeMode);
        var accentSoft = Mix(light.SurfaceHighlight, accent, 0.12);
        var accentSoftDark = Mix(dark.SurfaceHighlight, accent, 0.1);
        var accentMid = Mix(light.SurfaceHighlight, accent, 0.24);
        var accentMidDark = Mix(dark.SurfaceHighlight, accent, 0.16);
        var inputFill = Mix(light.SurfaceHighlight, accent, 0.05);
        var inputFillDark = Mix(dark.SurfaceHighlight, accent, 0.02);
        var inputFillFocused = Mix(light.SurfaceHighlight, accent, 0.09);
        var inputFillFocusedDark = Mix(dark.SurfaceHighlight, accent, 0.04);
        var inputStroke = Mix(light.SurfaceHighlight, accent, 0.16);
        var inputStrokeDark = Mix(dark.SurfaceHighlight, accent, 0.08);
        var primaryDisabled = ScaleLightness(accent, 0.4);
        var inputFillDisabled = ScaleLightness(inputFill, 0.4);
        var inputFillDisabledDark = ScaleLightness(inputFillDark, 0.4);
        var subtleActionFill = inputFill;
        var subtleActionFillDark = inputFillDark;
        var subtleActionDisabled = ScaleLightness(subtleActionFill, 0.4);
        var subtleActionDisabledDark = ScaleLightness(subtleActionFillDark, 0.4);
        var switchTrackOn = Mix(light.SurfaceHighlight, accent, 0.24);
        var switchTrackOnDark = Mix(dark.SurfaceHighlight, accent, 0.14);
        var switchTrackOff = inputFillDisabled;
        var switchTrackOffDark = inputFillDisabledDark;

        SetColor(resources, "AccentSoft", accentSoft);
        SetColor(resources, "AccentSoftDark", accentSoftDark);
        SetColor(resources, "AccentMid", accentMid);
        SetColor(resources, "AccentMidDark", accentMidDark);
        SetColor(resources, "InputFill", inputFill);
        SetColor(resources, "InputFillDark", inputFillDark);
        SetColor(resources, "InputFillFocused", inputFillFocused);
        SetColor(resources, "InputFillFocusedDark", inputFillFocusedDark);
        SetColor(resources, "InputFillDisabled", inputFillDisabled);
        SetColor(resources, "InputFillDisabledDark", inputFillDisabledDark);
        SetColor(resources, "InputStroke", inputStroke);
        SetColor(resources, "InputStrokeDark", inputStrokeDark);
        SetColor(resources, "InputTint", accent);
        SetColor(resources, "InputTintDark", Mix(accent, "#FFFFFF", 0.16));
        SetColor(resources, "InputForeground", ContrastText(inputFill));
        SetColor(resources, "InputForegroundDark", ContrastText(inputFillDark));
        SetColor(resources, "InputForegroundFocused", ContrastText(inputFillFocused));
        SetColor(resources, "InputForegroundFocusedDark", ContrastText(inputFillFocusedDark));
        SetColor(resources, "InputForegroundDisabled", ContrastText(inputFillDisabled));
        SetColor(resources, "InputForegroundDisabledDark", ContrastText(inputFillDisabledDark));
        SetColor(resources, "PrimaryForeground", ContrastText(accent));
        SetColor(resources, "PrimaryDisabled", primaryDisabled);
        SetColor(resources, "PrimaryDisabledDark", primaryDisabled);
        SetColor(resources, "PrimaryDisabledForeground", ContrastText(primaryDisabled));
        SetColor(resources, "PrimaryDisabledForegroundDark", ContrastText(primaryDisabled));
        SetColor(resources, "SubtleActionFill", subtleActionFill);
        SetColor(resources, "SubtleActionFillDark", subtleActionFillDark);
        SetColor(resources, "SubtleActionForeground", ContrastText(subtleActionFill));
        SetColor(resources, "SubtleActionForegroundDark", ContrastText(subtleActionFillDark));
        SetColor(resources, "SubtleActionDisabled", subtleActionDisabled);
        SetColor(resources, "SubtleActionDisabledDark", subtleActionDisabledDark);
        SetColor(resources, "SubtleActionDisabledForeground", ContrastText(subtleActionDisabled));
        SetColor(resources, "SubtleActionDisabledForegroundDark", ContrastText(subtleActionDisabledDark));
        SetColor(resources, "ChipActiveFill", accentSoft);
        SetColor(resources, "ChipActiveFillDark", accentSoftDark);
        SetColor(resources, "ChipActiveStroke", accentMid);
        SetColor(resources, "ChipActiveStrokeDark", accentMidDark);
        SetColor(resources, "ChipActiveForeground", ContrastText(accentSoft));
        SetColor(resources, "ChipActiveForegroundDark", ContrastText(accentSoftDark));
        SetColor(resources, "ChipInactiveFill", inputFill);
        SetColor(resources, "ChipInactiveFillDark", inputFillDark);
        SetColor(resources, "ChipInactiveStroke", inputStroke);
        SetColor(resources, "ChipInactiveStrokeDark", inputStrokeDark);
        SetColor(resources, "ChipInactiveForeground", ContrastText(inputFill));
        SetColor(resources, "ChipInactiveForegroundDark", ContrastText(inputFillDark));
        SetColor(resources, "SwitchTrackOn", switchTrackOn);
        SetColor(resources, "SwitchTrackOnDark", switchTrackOnDark);
        SetColor(resources, "SwitchTrackOff", switchTrackOff);
        SetColor(resources, "SwitchTrackOffDark", switchTrackOffDark);
        SetColor(resources, "SwitchThumbOn", ContrastText(switchTrackOn));
        SetColor(resources, "SwitchThumbOnDark", ContrastText(switchTrackOnDark));
        SetColor(resources, "SwitchThumbOff", ContrastText(switchTrackOff));
        SetColor(resources, "SwitchThumbOffDark", ContrastText(switchTrackOffDark));
        SetColor(resources, "InputFillActive", useDarkTokens ? inputFillDark : inputFill);
        SetColor(resources, "InputFillFocusedActive", useDarkTokens ? inputFillFocusedDark : inputFillFocused);
        SetColor(resources, "InputFillDisabledActive", useDarkTokens ? inputFillDisabledDark : inputFillDisabled);
        SetColor(resources, "InputStrokeActive", useDarkTokens ? inputStrokeDark : inputStroke);
        SetColor(resources, "InputTintActive", useDarkTokens ? Mix(accent, "#FFFFFF", 0.16) : accent);
        SetColor(resources, "InputForegroundActive", useDarkTokens ? ContrastText(inputFillDark) : ContrastText(inputFill));
        SetColor(resources, "InputForegroundFocusedActive", useDarkTokens ? ContrastText(inputFillFocusedDark) : ContrastText(inputFillFocused));
        SetColor(resources, "InputForegroundDisabledActive", useDarkTokens ? ContrastText(inputFillDisabledDark) : ContrastText(inputFillDisabled));
        SetColor(resources, "PrimaryDisabledActive", primaryDisabled);
        SetColor(resources, "PrimaryDisabledForegroundActive", ContrastText(primaryDisabled));
        SetColor(resources, "SubtleActionFillActive", useDarkTokens ? subtleActionFillDark : subtleActionFill);
        SetColor(resources, "SubtleActionForegroundActive", useDarkTokens ? ContrastText(subtleActionFillDark) : ContrastText(subtleActionFill));
        SetColor(resources, "SubtleActionDisabledActive", useDarkTokens ? subtleActionDisabledDark : subtleActionDisabled);
        SetColor(resources, "SubtleActionDisabledForegroundActive", useDarkTokens ? ContrastText(subtleActionDisabledDark) : ContrastText(subtleActionDisabled));
        SetColor(resources, "ChipActiveFillActive", useDarkTokens ? accentSoftDark : accentSoft);
        SetColor(resources, "ChipActiveStrokeActive", useDarkTokens ? accentMidDark : accentMid);
        SetColor(resources, "ChipActiveForegroundActive", useDarkTokens ? ContrastText(accentSoftDark) : ContrastText(accentSoft));
        SetColor(resources, "ChipInactiveFillActive", useDarkTokens ? inputFillDark : inputFill);
        SetColor(resources, "ChipInactiveStrokeActive", useDarkTokens ? inputStrokeDark : inputStroke);
        SetColor(resources, "ChipInactiveForegroundActive", useDarkTokens ? ContrastText(inputFillDark) : ContrastText(inputFill));
        SetColor(resources, "SwitchTrackOnActive", useDarkTokens ? switchTrackOnDark : switchTrackOn);
        SetColor(resources, "SwitchTrackOffActive", useDarkTokens ? switchTrackOffDark : switchTrackOff);
        SetColor(resources, "SwitchThumbOnActive", useDarkTokens ? ContrastText(switchTrackOnDark) : ContrastText(switchTrackOn));
        SetColor(resources, "SwitchThumbOffActive", useDarkTokens ? ContrastText(switchTrackOffDark) : ContrastText(switchTrackOff));
        RefreshThemeSurfacesOnVisibleUI();

        var textScalePercent = NormalizeTextScalePercent(settings.TextScale);
        ApplyTextScale(resources, textScalePercent);
        _activeTextScalePercent = textScalePercent;
        _activeTextScaleFactor = Math.Max(0.1d, textScalePercent / 100d);
        _activeIconScaleFactor = 2d * _activeTextScaleFactor;
        if (ShouldBypassRuntimeTextScaling()) {
            WindowsStartupSafety.Trace($"Theme.TextScale.Apply:runtime_skip_windows_or_safe percent={textScalePercent}");
            return;
        }

        WindowsStartupSafety.Trace($"Theme.TextScale.Apply:runtime_start percent={textScalePercent}");
        ApplyRuntimeTextScale(textScalePercent);
        WindowsStartupSafety.Trace($"Theme.TextScale.Apply:runtime_end percent={textScalePercent}");
    }

    public static void RefreshTextScaleOnVisibleUI() {
        if (ShouldBypassRuntimeTextScaling()) {
            WindowsStartupSafety.Trace("Theme.TextScale.Refresh:skip_windows_or_safe");
            return;
        }

        WindowsStartupSafety.Trace("Theme.TextScale.Refresh:start");
        ApplyRuntimeTextScaleCore(Application.Current, _activeTextScaleFactor, _activeIconScaleFactor);
        WindowsStartupSafety.Trace("Theme.TextScale.Refresh:end");
    }

    public static void RefreshTextScaleOnVisibleUIWithDeferredPasses() {
        if (ShouldBypassRuntimeTextScaling()) {
            WindowsStartupSafety.Trace("Theme.TextScale.RefreshDeferred:skip_windows_or_safe");
            return;
        }

        WindowsStartupSafety.Trace("Theme.TextScale.RefreshDeferred:start");
        RefreshTextScaleOnVisibleUI();
        _ = MainThread.InvokeOnMainThreadAsync(async () => {
            await Task.Delay(220).ConfigureAwait(true);
            RefreshTextScaleOnVisibleUI();
            await Task.Delay(650).ConfigureAwait(true);
            RefreshTextScaleOnVisibleUI();
            WindowsStartupSafety.Trace("Theme.TextScale.RefreshDeferred:end");
        });
    }

    private static void ApplyThemeStyles(ResourceDictionary resources) {
        var merged = resources.MergedDictionaries;
        var existingThemeB = merged.Where(dictionary => dictionary is ThemeBStyles).ToList();

        _themeBStyles ??= new ThemeBStyles();
        if (!merged.Contains(_themeBStyles)) {
            merged.Add(_themeBStyles);
        }

        foreach (var dictionary in existingThemeB.Where(dictionary => !ReferenceEquals(dictionary, _themeBStyles))) {
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

    private static string Mix(string baseHex, string tintHex, double tintRatio) {
        var baseColor = Color.FromArgb(baseHex);
        var tintColor = Color.FromArgb(tintHex);
        var ratio = Math.Clamp(tintRatio, 0d, 1d);
        var baseRatio = 1d - ratio;

        var r = (int)Math.Round(((baseColor.Red * baseRatio) + (tintColor.Red * ratio)) * 255d);
        var g = (int)Math.Round(((baseColor.Green * baseRatio) + (tintColor.Green * ratio)) * 255d);
        var b = (int)Math.Round(((baseColor.Blue * baseRatio) + (tintColor.Blue * ratio)) * 255d);
        var a = (int)Math.Round(((baseColor.Alpha * baseRatio) + (tintColor.Alpha * ratio)) * 255d);
        return $"#{Clamp(a):X2}{Clamp(r):X2}{Clamp(g):X2}{Clamp(b):X2}";
    }

    private static string ScaleLightness(string hex, double factor) {
        var color = Color.FromArgb(hex);
        var hsl = ToHsl(color);
        var scaledLightness = Math.Clamp(hsl.Lightness * Math.Clamp(factor, 0d, 1d), 0d, 1d);
        return FromHsl(hsl.Hue, hsl.Saturation, scaledLightness, color.Alpha);
    }

    private static string ContrastText(string backgroundHex) {
        var color = Color.FromArgb(backgroundHex);
        var hsl = ToHsl(color);
        return hsl.Lightness >= 0.58d ? "#000000" : "#FFFFFF";
    }

    private static bool UseDarkTokens(ThemeMode mode) {
        return mode switch {
            ThemeMode.Dark => true,
            ThemeMode.Light => false,
            _ => (Application.Current?.RequestedTheme ?? AppTheme.Unspecified) == AppTheme.Dark
        };
    }

    private static (double Hue, double Saturation, double Lightness) ToHsl(Color color) {
        var r = color.Red;
        var g = color.Green;
        var b = color.Blue;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        var lightness = (max + min) / 2d;

        if (delta <= 0d) {
            return (0d, 0d, lightness);
        }

        var saturation = lightness > 0.5d
            ? delta / (2d - max - min)
            : delta / (max + min);

        double hue;
        if (Math.Abs(max - r) < double.Epsilon) {
            hue = ((g - b) / delta) + (g < b ? 6d : 0d);
        } else if (Math.Abs(max - g) < double.Epsilon) {
            hue = ((b - r) / delta) + 2d;
        } else {
            hue = ((r - g) / delta) + 4d;
        }

        hue /= 6d;
        return (hue, saturation, lightness);
    }

    private static string FromHsl(double hue, double saturation, double lightness, double alpha) {
        double r;
        double g;
        double b;

        if (saturation <= 0d) {
            r = g = b = lightness;
        } else {
            var q = lightness < 0.5d
                ? lightness * (1d + saturation)
                : lightness + saturation - (lightness * saturation);
            var p = (2d * lightness) - q;
            r = HueToRgb(p, q, hue + (1d / 3d));
            g = HueToRgb(p, q, hue);
            b = HueToRgb(p, q, hue - (1d / 3d));
        }

        return $"#{Clamp((int)Math.Round(alpha * 255d)):X2}{Clamp((int)Math.Round(r * 255d)):X2}{Clamp((int)Math.Round(g * 255d)):X2}{Clamp((int)Math.Round(b * 255d)):X2}";
    }

    private static double HueToRgb(double p, double q, double t) {
        if (t < 0d) {
            t += 1d;
        }

        if (t > 1d) {
            t -= 1d;
        }

        if (t < (1d / 6d)) {
            return p + ((q - p) * 6d * t);
        }

        if (t < 0.5d) {
            return q;
        }

        if (t < (2d / 3d)) {
            return p + ((q - p) * ((2d / 3d) - t) * 6d);
        }

        return p;
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

    private static void ApplyRuntimeTextScale(int percent) {
        if (ShouldBypassRuntimeTextScaling()) {
            WindowsStartupSafety.Trace($"Theme.TextScale.ApplyRuntime:skip_windows_or_safe percent={percent}");
            return;
        }

        var factor = Math.Max(0.1d, percent / 100d);
        var iconFactor = 2d * factor;
        if (MainThread.IsMainThread) {
            ApplyRuntimeTextScaleCore(Application.Current, factor, iconFactor);
            return;
        }

        MainThread.BeginInvokeOnMainThread(() => ApplyRuntimeTextScaleCore(Application.Current, factor, iconFactor));
    }

    private static bool ShouldBypassRuntimeTextScaling() {
        if (OperatingSystem.IsWindows()) {
            return true;
        }

        return RuntimeStabilityState.IsWindowsSafeStartupMode;
    }

    private static void ApplyRuntimeTextScaleCore(Application? app, double textFactor, double iconFactor) {
        if (app == null) {
            return;
        }

        foreach (var window in app.Windows) {
            if (window?.Page is not IVisualTreeElement root) {
                continue;
            }

            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            ApplyRuntimeTextScaleNode(root, textFactor, iconFactor, visited);
            ApplyNativeShellTextScale(window, textFactor);
        }
    }

    private static void RefreshThemeSurfacesOnVisibleUI() {
#if ANDROID
        if (MainThread.IsMainThread) {
            RefreshThemeSurfacesCore(Application.Current);
            return;
        }

        MainThread.BeginInvokeOnMainThread(() => RefreshThemeSurfacesCore(Application.Current));
#endif
    }

    private static void RefreshThemeSurfacesCore(Application? app) {
#if ANDROID
        if (app == null) {
            return;
        }

        foreach (var window in app.Windows) {
            if (window?.Page is not IVisualTreeElement root) {
                continue;
            }

            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            RefreshThemeSurfaceNode(root, visited);
        }
#endif
    }

    private static void RefreshThemeSurfaceNode(IVisualTreeElement node, HashSet<object> visited) {
#if ANDROID
        if (!visited.Add(node)) {
            return;
        }

        if (node is BindableObject bindable) {
            ApplyRuntimeThemeSurface(bindable);
        }

        foreach (var child in node.GetVisualChildren()) {
            RefreshThemeSurfaceNode(child, visited);
        }
#endif
    }

    private static void ApplyRuntimeTextScaleNode(
        IVisualTreeElement node,
        double textFactor,
        double iconFactor,
        HashSet<object> visited) {
        if (!visited.Add(node)) {
            return;
        }

        if (node is BindableObject bindable) {
            ApplyRuntimeFontSize(bindable, textFactor);
            ApplyRuntimeIconSize(bindable, iconFactor);

            if (bindable is Label label && label.FormattedText != null) {
                foreach (var span in label.FormattedText.Spans) {
                    ApplyRuntimeFontSize(span, textFactor);
                }
            }
        }

        foreach (var child in node.GetVisualChildren()) {
            ApplyRuntimeTextScaleNode(child, textFactor, iconFactor, visited);
        }
    }

    private static void ApplyRuntimeFontSize(BindableObject target, double factor) {
        switch (target) {
            case Label label:
                ApplyRuntimeFontSizeCore(label, Label.FontSizeProperty, label.FontSize, size => label.FontSize = size, factor);
                return;
            case Button button:
                ApplyRuntimeFontSizeCore(button, Button.FontSizeProperty, button.FontSize, size => button.FontSize = size, factor);
                return;
            case Entry entry:
                ApplyRuntimeFontSizeCore(entry, Entry.FontSizeProperty, entry.FontSize, size => entry.FontSize = size, factor);
                return;
            case Editor editor:
                ApplyRuntimeFontSizeCore(editor, Editor.FontSizeProperty, editor.FontSize, size => editor.FontSize = size, factor);
                return;
            case Picker picker:
                ApplyRuntimeFontSizeCore(picker, Picker.FontSizeProperty, picker.FontSize, size => picker.FontSize = size, factor);
                return;
            case SearchBar searchBar:
                ApplyRuntimeFontSizeCore(searchBar, SearchBar.FontSizeProperty, searchBar.FontSize, size => searchBar.FontSize = size, factor);
                return;
            case DatePicker datePicker:
                ApplyRuntimeFontSizeCore(datePicker, DatePicker.FontSizeProperty, datePicker.FontSize, size => datePicker.FontSize = size, factor);
                return;
            case TimePicker timePicker:
                ApplyRuntimeFontSizeCore(timePicker, TimePicker.FontSizeProperty, timePicker.FontSize, size => timePicker.FontSize = size, factor);
                return;
            case RadioButton radioButton:
                ApplyRuntimeFontSizeCore(radioButton, RadioButton.FontSizeProperty, radioButton.FontSize, size => radioButton.FontSize = size, factor);
                return;
            case Span span:
                ApplyRuntimeFontSizeCore(span, Span.FontSizeProperty, span.FontSize, size => span.FontSize = size, factor);
                return;
        }
    }

    private static void ApplyRuntimeFontSizeCore(
        BindableObject target,
        BindableProperty fontSizeProperty,
        double currentSize,
        Action<double> setter,
        double factor) {
        if (currentSize <= 0 || double.IsNaN(currentSize) || double.IsInfinity(currentSize)) {
            return;
        }

        var storedValue = target.GetValue(UnscaledFontSizeProperty) is double stored && stored > 0
            ? stored
            : 0d;
        var hasStored = storedValue > 0;
        var hasLocalFontSize = target.IsSet(fontSizeProperty);

        if (!hasStored && !hasLocalFontSize) {
            return;
        }

        var unscaled = hasStored
            ? storedValue
            : hasLocalFontSize
                ? currentSize
                : currentSize / factor;

        target.SetValue(UnscaledFontSizeProperty, unscaled);
        var scaled = Math.Max(1d, Math.Round(unscaled * factor, 2));
        if (Math.Abs(currentSize - scaled) < 0.01d) {
            return;
        }

        setter(scaled);
    }

    private static void ApplyRuntimeIconSize(BindableObject target, double iconFactor) {
        switch (target) {
            case Image image:
                ApplyRuntimeIconSizeCore(image, image.Source, iconFactor);
                return;
            case ImageButton imageButton:
                ApplyRuntimeIconSizeCore(imageButton, imageButton.Source, iconFactor);
                return;
            case Button button when button.ImageSource != null:
                ApplyRuntimeIconSizeCore(button, button.ImageSource, iconFactor);
                return;
            case Label label when IsLikelyGlyphIconText(label.Text):
                ApplyRuntimeGlyphIconFontSize(label, label.FontSize, size => label.FontSize = size, iconFactor);
                return;
            case Button button when button.ImageSource == null && IsLikelyGlyphIconText(button.Text):
                ApplyRuntimeGlyphIconFontSize(button, button.FontSize, size => button.FontSize = size, iconFactor);
                return;
        }
    }

    private static void ApplyRuntimeIconSizeCore(VisualElement element, ImageSource? source, double iconFactor) {
        if (source == null) {
            return;
        }

        var (unscaledWidth, hasWidth) = GetOrCaptureUnscaledDimension(
            element,
            UnscaledIconWidthProperty,
            element.WidthRequest,
            element.Width,
            iconFactor);
        var (unscaledHeight, hasHeight) = GetOrCaptureUnscaledDimension(
            element,
            UnscaledIconHeightProperty,
            element.HeightRequest,
            element.Height,
            iconFactor);

        if (!IsLikelyIcon(source, unscaledWidth, unscaledHeight)) {
            return;
        }

        if (hasWidth) {
            element.WidthRequest = Math.Round(unscaledWidth * iconFactor, 2);
        }

        if (hasHeight) {
            element.HeightRequest = Math.Round(unscaledHeight * iconFactor, 2);
        }

        if (source is FontImageSource fontImage) {
            var baseline = fontImage.GetValue(UnscaledIconFontSizeProperty) is double stored && stored > 0
                ? stored
                : (fontImage.Size > 0 ? fontImage.Size / iconFactor : 0d);

            if (baseline > 0) {
                fontImage.SetValue(UnscaledIconFontSizeProperty, baseline);
                fontImage.Size = Math.Round(baseline * iconFactor, 2);
            }
        }
    }

    private static (double Value, bool HasValue) GetOrCaptureUnscaledDimension(
        BindableObject target,
        BindableProperty property,
        double requestValue,
        double actualValue,
        double iconFactor) {
        if (target.GetValue(property) is double stored && stored > 0) {
            return (stored, true);
        }

        double raw = 0;
        if (requestValue > 0 && !double.IsNaN(requestValue) && !double.IsInfinity(requestValue)) {
            raw = requestValue;
        } else if (actualValue > 0 && !double.IsNaN(actualValue) && !double.IsInfinity(actualValue)) {
            raw = actualValue;
        }

        if (raw <= 0) {
            return (0, false);
        }

        var baseline = raw / Math.Max(0.1d, iconFactor);
        target.SetValue(property, baseline);
        return (baseline, true);
    }

    private static bool IsLikelyIcon(ImageSource source, double unscaledWidth, double unscaledHeight) {
        if (source is FontImageSource) {
            return true;
        }

        var largestDimension = Math.Max(unscaledWidth, unscaledHeight);
        if (largestDimension > 0 && largestDimension <= 96) {
            return true;
        }

        if (source is FileImageSource fileImage) {
            var file = fileImage.File?.ToLowerInvariant() ?? string.Empty;
            if (file.Contains("icon") || file.Contains("tab_") || file.Contains("glyph")) {
                return true;
            }
        }

        return false;
    }

    private static void ApplyRuntimeGlyphIconFontSize(
        BindableObject target,
        double currentSize,
        Action<double> setter,
        double iconFactor) {
        if (currentSize <= 0 || double.IsNaN(currentSize) || double.IsInfinity(currentSize)) {
            return;
        }

        var baseline = target.GetValue(UnscaledIconFontSizeProperty) is double storedIconBaseline && storedIconBaseline > 0
            ? storedIconBaseline
            : target.GetValue(UnscaledFontSizeProperty) is double storedTextBaseline && storedTextBaseline > 0
                ? storedTextBaseline
                : currentSize;

        target.SetValue(UnscaledIconFontSizeProperty, baseline);
        var scaled = Math.Max(1d, Math.Round(baseline * iconFactor, 2));
        if (Math.Abs(currentSize - scaled) < 0.01d) {
            return;
        }

        setter(scaled);
    }

    private static bool IsLikelyGlyphIconText(string? text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.Length > 3) {
            return false;
        }

        var symbolCount = 0;
        foreach (var ch in trimmed) {
            if (char.IsWhiteSpace(ch)) {
                continue;
            }

            if (char.IsLetterOrDigit(ch)) {
                return false;
            }

            var category = char.GetUnicodeCategory(ch);
            if (category is UnicodeCategory.MathSymbol
                or UnicodeCategory.CurrencySymbol
                or UnicodeCategory.ModifierSymbol
                or UnicodeCategory.OtherSymbol
                or UnicodeCategory.DashPunctuation
                or UnicodeCategory.OpenPunctuation
                or UnicodeCategory.ClosePunctuation
                or UnicodeCategory.InitialQuotePunctuation
                or UnicodeCategory.FinalQuotePunctuation
                or UnicodeCategory.OtherPunctuation) {
                symbolCount++;
            }
        }

        return symbolCount > 0;
    }

#if ANDROID
    private static void ApplyNativeShellTextScale(Microsoft.Maui.Controls.Window window, double textFactor) {
        if (window.Page?.Handler?.PlatformView is not AView rootView) {
            return;
        }

        var bottomNav = FindBottomNavigationView(rootView);
        if (bottomNav != null) {
            var tabTextSp = (float)Math.Max(8d, Math.Round(12d * textFactor, 2));
            ApplyTextSizeRecursive(bottomNav, tabTextSp);
        }

        var toolbar = FindToolbar(rootView);
        if (toolbar != null) {
            var titleTextSp = (float)Math.Max(10d, Math.Round(20d * textFactor, 2));
            ApplyTextSizeRecursive(toolbar, titleTextSp);
        }
    }

    private static BottomNavigationView? FindBottomNavigationView(AView view) {
        if (view is BottomNavigationView nav) {
            return nav;
        }

        if (view is not AViewGroup group) {
            return null;
        }

        for (var i = 0; i < group.ChildCount; i++) {
            var child = group.GetChildAt(i);
            if (child == null) {
                continue;
            }

            var found = FindBottomNavigationView(child);
            if (found != null) {
                return found;
            }
        }

        return null;
    }

    private static AToolbar? FindToolbar(AView view) {
        if (view is AToolbar toolbar) {
            return toolbar;
        }

        if (view is not AViewGroup group) {
            return null;
        }

        for (var i = 0; i < group.ChildCount; i++) {
            var child = group.GetChildAt(i);
            if (child == null) {
                continue;
            }

            var found = FindToolbar(child);
            if (found != null) {
                return found;
            }
        }

        return null;
    }

    private static void ApplyTextSizeRecursive(AView view, float textSp) {
        if (view is ATextView textView) {
            textView.SetTextSize(ComplexUnitType.Sp, textSp);
        }

        if (view is not AViewGroup group) {
            return;
        }

        for (var i = 0; i < group.ChildCount; i++) {
            var child = group.GetChildAt(i);
            if (child == null) {
                continue;
            }

            ApplyTextSizeRecursive(child, textSp);
        }
    }

    private static void ApplyRuntimeThemeSurface(BindableObject target) {
        if (target is not (Entry or Picker or DatePicker or TimePicker or Switch)) {
            return;
        }

        if (target is not Element element) {
            return;
        }

        if (element.Handler?.PlatformView is not AView platformView) {
            return;
        }

        if (target is Switch && platformView is ASwitchCompat switchCompat) {
            ApplyAndroidSwitchSurface(switchCompat);
            return;
        }

        platformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(
            ResolveAndroidThemeColor("InputTint", "InputTintDark", "#2FB79D"));
        ApplyAndroidInputSurface(platformView);
    }

    private static void ApplyAndroidInputSurface(AView view) {
        view.Background = BuildAndroidInputDrawable(view.Context);
        var density = view.Context?.Resources?.DisplayMetrics?.Density ?? 1f;
        var horizontal = (int)(12 * density);
        var vertical = (int)(10 * density);
        view.SetPadding(horizontal, vertical, horizontal, vertical);
    }

    private static Android.Graphics.Drawables.GradientDrawable BuildAndroidInputDrawable(Android.Content.Context? context) {
        var density = context?.Resources?.DisplayMetrics?.Density ?? 1f;
        var fill = ResolveAndroidThemeColor("InputFill", "InputFillDark", "#ECF4F0");
        var stroke = ResolveAndroidThemeColor("InputStroke", "InputStrokeDark", "#BAD4C9");

        var drawable = new Android.Graphics.Drawables.GradientDrawable();
        drawable.SetShape(Android.Graphics.Drawables.ShapeType.Rectangle);
        drawable.SetColor(fill);
        drawable.SetCornerRadius(14f * density);
        drawable.SetStroke(Math.Max(1, (int)Math.Round(density)), stroke);
        return drawable;
    }

    private static void ApplyAndroidSwitchSurface(ASwitchCompat switchCompat) {
        var trackStates = new[] {
            new[] { Android.Resource.Attribute.StateEnabled, Android.Resource.Attribute.StateChecked },
            new[] { Android.Resource.Attribute.StateEnabled, -Android.Resource.Attribute.StateChecked },
            new[] { -Android.Resource.Attribute.StateEnabled, Android.Resource.Attribute.StateChecked },
            new[] { -Android.Resource.Attribute.StateEnabled, -Android.Resource.Attribute.StateChecked }
        };
        var trackColors = new[] {
            ResolveAndroidThemeColor("SwitchTrackOn", "SwitchTrackOnDark", "#7BC9B7"),
            ResolveAndroidThemeColor("SwitchTrackOff", "SwitchTrackOffDark", "#54606C"),
            ResolveAndroidThemeColor("PrimaryDisabled", "PrimaryDisabledDark", "#6A5032"),
            ResolveAndroidThemeColor("InputFillDisabled", "InputFillDisabledDark", "#18222C")
        };
        var thumbColors = new[] {
            ResolveAndroidThemeColor("SwitchThumbOn", "SwitchThumbOnDark", "#FFFFFF"),
            ResolveAndroidThemeColor("SwitchThumbOff", "SwitchThumbOffDark", "#FFFFFF"),
            ResolveAndroidThemeColor("PrimaryDisabledForeground", "PrimaryDisabledForegroundDark", "#FFFFFF"),
            ResolveAndroidThemeColor("InputForegroundDisabled", "InputForegroundDisabledDark", "#FFFFFF")
        };
        switchCompat.TrackTintList = new Android.Content.Res.ColorStateList( trackStates , Array.ConvertAll( trackColors , color => color.ToArgb() ) );

        switchCompat.ThumbTintList = new Android.Content.Res.ColorStateList( trackStates , Array.ConvertAll( thumbColors , color => color.ToArgb() ) );
        // switchCompat.TrackTintList = new Android.Content.Res.ColorStateList(trackStates, trackColors);
        //  switchCompat.ThumbTintList = new Android.Content.Res.ColorStateList(trackStates, thumbColors);
    }

    private static Android.Graphics.Color ResolveAndroidThemeColor(string lightKey, string darkKey, string fallbackHex) {
        var resource = Application.Current?.Resources;
        var key = IsDarkThemeActive() ? darkKey : lightKey;

        if (resource != null && resource.TryGetValue(key, out var value)) {
            if (value is Color mauiColor) {
                return Android.Graphics.Color.Argb(
                    (int)Math.Round(mauiColor.Alpha * 255),
                    (int)Math.Round(mauiColor.Red * 255),
                    (int)Math.Round(mauiColor.Green * 255),
                    (int)Math.Round(mauiColor.Blue * 255));
            }

            if (value is SolidColorBrush brush) {
                var brushColor = brush.Color;
                return Android.Graphics.Color.Argb(
                    (int)Math.Round(brushColor.Alpha * 255),
                    (int)Math.Round(brushColor.Red * 255),
                    (int)Math.Round(brushColor.Green * 255),
                    (int)Math.Round(brushColor.Blue * 255));
            }
        }

        return Android.Graphics.Color.ParseColor(fallbackHex);
    }

    private static bool IsDarkThemeActive() {
        var appTheme = Application.Current?.UserAppTheme ?? AppTheme.Unspecified;
        if (appTheme == AppTheme.Unspecified) {
            appTheme = Application.Current?.RequestedTheme ?? AppTheme.Unspecified;
        }

        return appTheme != AppTheme.Light;
    }
#else
    private static void ApplyNativeShellTextScale(Microsoft.Maui.Controls.Window window, double textFactor) {
    }
#endif

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

    private static readonly ThemeColors ThemeBLight = new(
        SkyTop: "#EEF7F3",
        SkyMid: "#E2F2EA",
        SkyBase: "#D5EDE1",
        SandTop: "#FFFFFF",
        SandMid: "#F6FBF8",
        SandBase: "#EEF6F1",
        SurfaceGlass: "#FFFFFF",
        SurfaceSolid: "#FCFEFD",
        SurfaceHighlight: "#D5E7DE",
        TextMuted: "#5E6B74",
        TextSubtle: "#7A8892",
        NightTop: "#F2F7F5",
        NightBase: "#E6F1EC",
        Secondary: "#E2EFE8");

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
