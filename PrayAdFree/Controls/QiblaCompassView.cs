using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.Controls;

public enum QiblaCompassVisualFilter {
    None,
    Night,
    Contrast
}

public sealed class QiblaCompassView : GraphicsView, IDrawable {
    public static readonly BindableProperty BearingProperty = BindableProperty.Create(
        nameof(Bearing),
        typeof(double),
        typeof(QiblaCompassView),
        0d,
        propertyChanged: OnCompassValueChanged);

    public static readonly BindableProperty HeadingProperty = BindableProperty.Create(
        nameof(Heading),
        typeof(double),
        typeof(QiblaCompassView),
        0d,
        propertyChanged: OnCompassValueChanged);

    public static readonly BindableProperty VisualFilterProperty = BindableProperty.Create(
        nameof(VisualFilter),
        typeof(QiblaCompassVisualFilter),
        typeof(QiblaCompassView),
        QiblaCompassVisualFilter.None,
        propertyChanged: OnCompassValueChanged);

    private static readonly (string Label, double Angle)[] DirectionMarks = {
        ("ش", 0),
        ("شر", 45),
        ("شـ", 90),
        ("جش", 135),
        ("ج", 180),
        ("جغ", 225),
        ("غ", 270),
        ("شغ", 315)
    };

    private IDispatcherTimer? _pulseTimer;
    private float _pulsePhase;

    public QiblaCompassView() {
        Drawable = this;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public double Bearing {
        get => (double)GetValue(BearingProperty);
        set => SetValue(BearingProperty, value);
    }

    public double Heading {
        get => (double)GetValue(HeadingProperty);
        set => SetValue(HeadingProperty, value);
    }

    public QiblaCompassVisualFilter VisualFilter {
        get => (QiblaCompassVisualFilter)GetValue(VisualFilterProperty);
        set => SetValue(VisualFilterProperty, value);
    }

    public void Draw(ICanvas canvas, RectF dirtyRect) {
        var centerX = dirtyRect.Center.X;
        var centerY = dirtyRect.Center.Y;
        var radius = MathF.Min(dirtyRect.Width, dirtyRect.Height) * 0.5f - 8f;
        if (radius <= 0f) {
            return;
        }

        var theme = Application.Current?.UserAppTheme ?? AppTheme.Unspecified;
        if (theme == AppTheme.Unspecified) {
            theme = Application.Current?.RequestedTheme ?? AppTheme.Unspecified;
        }
        var isDark = theme != AppTheme.Light;
        var palette = ResolvePalette(isDark, VisualFilter);

        canvas.FillColor = palette.Background;
        canvas.FillCircle(centerX, centerY, radius - 0.5f);
        canvas.FillColor = palette.InnerBackground;
        canvas.FillCircle(centerX, centerY, radius - 22f);

        canvas.StrokeColor = palette.RingSoft;
        canvas.StrokeSize = 2f;
        canvas.DrawCircle(centerX, centerY, radius);
        canvas.StrokeColor = palette.Ring.WithAlpha(0.35f);
        canvas.StrokeSize = 1f;
        canvas.DrawCircle(centerX, centerY, radius - 22f);

        var rotation = -Heading;
        var tickOuter = radius - 8f;
        for (var i = 0; i < 72; i++) {
            var angle = (i * 5d) + rotation;
            var tickLength = i % 18 == 0 ? 11f : i % 9 == 0 ? 7f : 4f;
            var stroke = i % 18 == 0 ? palette.Ring : i % 9 == 0 ? palette.MajorTick : palette.MinorTick;
            DrawRadialLine(canvas, centerX, centerY, tickOuter, tickOuter - tickLength, angle, stroke, 1f);
        }

        foreach (var (label, baseAngle) in DirectionMarks) {
            var angle = baseAngle + rotation;
            var point = PolarPoint(centerX, centerY, radius - 34f, angle);
            canvas.FontSize = 22f;
            canvas.FontColor = baseAngle == 0 ? palette.North : palette.Text;
            canvas.DrawString(label, point.X - 18f, point.Y - 14f, 36f, 28f, HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        var northY = centerY - radius - 6f;
        var northPath = new PathF();
        northPath.MoveTo(centerX, northY);
        northPath.LineTo(centerX - 6f, northY + 10f);
        northPath.LineTo(centerX + 6f, northY + 10f);
        northPath.Close();
        canvas.FillColor = palette.North;
        canvas.FillPath(northPath);

        var qiblaRelative = NormalizeAngle(Bearing - Heading);
        var qiblaPoint = PolarPoint(centerX, centerY, radius - 28f, qiblaRelative);
        var pulse = 0.5f + (MathF.Sin(_pulsePhase) * 0.5f);
        var glowRadius = 12f + (pulse * 5f);
        canvas.FillColor = palette.DotGlow.WithAlpha(0.16f + (pulse * 0.20f));
        canvas.FillCircle(qiblaPoint.X, qiblaPoint.Y, glowRadius + 7f);
        canvas.FillColor = palette.DotGlow.WithAlpha(0.22f + (pulse * 0.22f));
        canvas.FillCircle(qiblaPoint.X, qiblaPoint.Y, glowRadius);
        canvas.FillColor = palette.Dot.WithAlpha(0.75f);
        canvas.FillCircle(qiblaPoint.X, qiblaPoint.Y, 8f);
        canvas.FillColor = palette.Dot;
        canvas.FillCircle(qiblaPoint.X, qiblaPoint.Y, 6f);

        canvas.FontSize = 52f;
        canvas.FontColor = palette.Text;
        canvas.DrawString($"{Math.Round(Bearing):0}\u00B0", centerX - 110f, centerY - 34f, 220f, 62f, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.FontSize = 13f;
        canvas.FontColor = palette.Muted;
        canvas.DrawString(LocalizationManager.Translate("QiblaDirection"), centerX - 110f, centerY + 22f, 220f, 24f, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    private void OnLoaded(object? sender, EventArgs e) {
        if (Dispatcher == null || _pulseTimer != null) {
            return;
        }

        _pulseTimer = Dispatcher.CreateTimer();
        _pulseTimer.Interval = TimeSpan.FromMilliseconds(55);
        _pulseTimer.IsRepeating = true;
        _pulseTimer.Tick += OnPulseTick;
        _pulseTimer.Start();
    }

    private void OnUnloaded(object? sender, EventArgs e) {
        if (_pulseTimer == null) {
            return;
        }

        _pulseTimer.Tick -= OnPulseTick;
        _pulseTimer.Stop();
        _pulseTimer = null;
    }

    private void OnPulseTick(object? sender, EventArgs e) {
        _pulsePhase += 0.18f;
        if (_pulsePhase > MathF.Tau) {
            _pulsePhase -= MathF.Tau;
        }
        Invalidate();
    }

    private static void OnCompassValueChanged(BindableObject bindable, object oldValue, object newValue) {
        if (bindable is QiblaCompassView view) {
            view.Invalidate();
        }
    }

    private static CompassPalette ResolvePalette(bool isDark, QiblaCompassVisualFilter filter) {
        if (!isDark) {
            return filter switch {
                QiblaCompassVisualFilter.Night => new CompassPalette(
                    Background: Color.FromArgb("#102334"),
                    InnerBackground: Color.FromArgb("#132A3C"),
                    Ring: Color.FromArgb("#2FB79D"),
                    RingSoft: Color.FromArgb("#2FB79D").WithAlpha(0.35f),
                    MajorTick: Color.FromArgb("#7FA0BC"),
                    MinorTick: Color.FromArgb("#5E7991"),
                    Text: Color.FromArgb("#EAF1FA"),
                    Muted: Color.FromArgb("#9EB1C7"),
                    North: Color.FromArgb("#EF4444"),
                    Dot: Color.FromArgb("#F0C339"),
                    DotGlow: Color.FromArgb("#F0C339")),
                QiblaCompassVisualFilter.Contrast => new CompassPalette(
                    Background: Color.FromArgb("#0A1D2B"),
                    InnerBackground: Color.FromArgb("#0F2434"),
                    Ring: Color.FromArgb("#4BEAC8"),
                    RingSoft: Color.FromArgb("#4BEAC8").WithAlpha(0.34f),
                    MajorTick: Color.FromArgb("#B7E7FF"),
                    MinorTick: Color.FromArgb("#7EA9C3"),
                    Text: Color.FromArgb("#FFFFFF"),
                    Muted: Color.FromArgb("#CBE2F5"),
                    North: Color.FromArgb("#F43F5E"),
                    Dot: Color.FromArgb("#FFD54A"),
                    DotGlow: Color.FromArgb("#FFD54A")),
                _ => new CompassPalette(
                    Background: Color.FromArgb("#F4FAF8"),
                    InnerBackground: Color.FromArgb("#E9F5F1"),
                    Ring: Color.FromArgb("#1FAF93"),
                    RingSoft: Color.FromArgb("#1FAF93").WithAlpha(0.28f),
                    MajorTick: Color.FromArgb("#4F6A84"),
                    MinorTick: Color.FromArgb("#8EA5B8"),
                    Text: Color.FromArgb("#0F172A"),
                    Muted: Color.FromArgb("#5C6E81"),
                    North: Color.FromArgb("#EF4444"),
                    Dot: Color.FromArgb("#F0C339"),
                    DotGlow: Color.FromArgb("#F0C339"))
            };
        }

        return filter switch {
            QiblaCompassVisualFilter.Night => new CompassPalette(
                Background: Color.FromArgb("#0D1A2A"),
                InnerBackground: Color.FromArgb("#112033"),
                Ring: Color.FromArgb("#2FB79D"),
                RingSoft: Color.FromArgb("#2FB79D").WithAlpha(0.40f),
                MajorTick: Color.FromArgb("#5E7A95"),
                MinorTick: Color.FromArgb("#2B3F55"),
                Text: Color.FromArgb("#EAF1FA"),
                Muted: Color.FromArgb("#8CA0B6"),
                North: Color.FromArgb("#EF4444"),
                Dot: Color.FromArgb("#F0C339"),
                DotGlow: Color.FromArgb("#F0C339")),
            QiblaCompassVisualFilter.Contrast => new CompassPalette(
                Background: Color.FromArgb("#081220"),
                InnerBackground: Color.FromArgb("#0C1828"),
                Ring: Color.FromArgb("#4DEBCB"),
                RingSoft: Color.FromArgb("#4DEBCB").WithAlpha(0.36f),
                MajorTick: Color.FromArgb("#BCE7FF"),
                MinorTick: Color.FromArgb("#6D93B2"),
                Text: Color.FromArgb("#FFFFFF"),
                Muted: Color.FromArgb("#CBE3F8"),
                North: Color.FromArgb("#F43F5E"),
                Dot: Color.FromArgb("#FFD54A"),
                DotGlow: Color.FromArgb("#FFD54A")),
            _ => new CompassPalette(
                Background: Color.FromArgb("#101C2C"),
                InnerBackground: Color.FromArgb("#132236"),
                Ring: Color.FromArgb("#2FB79D"),
                RingSoft: Color.FromArgb("#2FB79D").WithAlpha(0.30f),
                MajorTick: Color.FromArgb("#4F6A84"),
                MinorTick: Color.FromArgb("#2B3F55"),
                Text: Color.FromArgb("#EAF1FA"),
                Muted: Color.FromArgb("#8CA0B6"),
                North: Color.FromArgb("#EF4444"),
                Dot: Color.FromArgb("#F0C339"),
                DotGlow: Color.FromArgb("#F0C339"))
        };
    }

    private static void DrawRadialLine(ICanvas canvas, float centerX, float centerY, float fromRadius, float toRadius, double angleDeg, Color color, float strokeSize) {
        var p1 = PolarPoint(centerX, centerY, fromRadius, angleDeg);
        var p2 = PolarPoint(centerX, centerY, toRadius, angleDeg);
        canvas.StrokeColor = color;
        canvas.StrokeSize = strokeSize;
        canvas.DrawLine(p1.X, p1.Y, p2.X, p2.Y);
    }

    private static PointF PolarPoint(float centerX, float centerY, float radius, double angleDeg) {
        var radians = (Math.PI / 180d) * (angleDeg - 90d);
        var x = centerX + (float)(Math.Cos(radians) * radius);
        var y = centerY + (float)(Math.Sin(radians) * radius);
        return new PointF(x, y);
    }

    private static double NormalizeAngle(double degrees) {
        var normalized = degrees % 360d;
        return normalized < 0 ? normalized + 360d : normalized;
    }

    private sealed record CompassPalette(
        Color Background,
        Color InnerBackground,
        Color Ring,
        Color RingSoft,
        Color MajorTick,
        Color MinorTick,
        Color Text,
        Color Muted,
        Color North,
        Color Dot,
        Color DotGlow);
}
