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
