using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Graphics;
using Pray_Ad_Free.Controls;
using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;
using System.Globalization;
using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Pages;

public partial class QiblaPage : ContentPage {
    private const double ManualHeadingSensitivity = 0.45;
    private QiblaViewModel ViewModel => (QiblaViewModel)BindingContext;
    private bool _animated;
    private bool _compassSupported = true;
    private bool _compassStarted;
    private bool _hasSmoothHeading;
    private double _smoothX;
    private double _smoothY;
    private double _headingAlpha = 0.18;
    private SensorSpeed _sensorSpeed = SensorSpeed.UI;
    private bool _applyLowPass = true;
    private QiblaFilterMode _filterMode = QiblaFilterMode.Normal;
    private double? _lastAcceptedHeading;
    private int _consecutiveRejectedReadings;
    private DateTime _lastCompassReadingUtc = DateTime.MinValue;
    private CancellationTokenSource? _compassWatchdogCts;
    private Microsoft.Maui.Controls.Maps.Map? _map;
    private double _manualPanLastTotalX;

    private enum QiblaDisplayMode {
        Compass,
        Map
    }

    private QiblaDisplayMode _displayMode = QiblaDisplayMode.Compass;
    private QiblaCompassVisualFilter _visualFilter = QiblaCompassVisualFilter.None;

    public QiblaPage() : this(ServiceHelper.GetService<QiblaViewModel>()) {
    }

    public QiblaPage(QiblaViewModel viewModel) {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override async void OnAppearing() {
        base.OnAppearing();
        ThemeManager.RefreshTextScaleOnVisibleUIWithDeferredPasses();
        ApplyInitialDisplayMode();
        ApplyDisplayState();

        _ = LoadAndUpdateAsync();
        ApplyCompassPreferences(false);
        _lastCompassReadingUtc = DateTime.MinValue;
        _compassSupported = Compass.IsSupported;
        Compass.ReadingChanged -= OnCompassReadingChanged;
        if (_compassSupported) {
            Compass.ReadingChanged += OnCompassReadingChanged;
        }
        ApplyHeadingMode(restartCompass: true);

        if (!_animated) {
            _animated = true;
            Opacity = 0;
            await this.FadeToAsync(1, 500, Easing.CubicOut);
        }
    }

    protected override void OnDisappearing() {
        base.OnDisappearing();
        StopCompassWatchdog();
        Compass.ReadingChanged -= OnCompassReadingChanged;
        if (_compassStarted && Compass.IsMonitoring) {
            Compass.Stop();
        }
        _compassStarted = false;
    }

    private void OnCompassReadingChanged(object? sender, CompassChangedEventArgs e) {
        _lastCompassReadingUtc = DateTime.UtcNow;
        var heading = e.Reading.HeadingMagneticNorth;
        if (IsFiltered(heading)) {
            return;
        }

        var radians = heading * Math.PI / 180.0;
        if (!_hasSmoothHeading) {
            _smoothX = Math.Cos(radians);
            _smoothY = Math.Sin(radians);
            _hasSmoothHeading = true;
        } else {
            _smoothX = (_smoothX * (1 - _headingAlpha)) + (Math.Cos(radians) * _headingAlpha);
            _smoothY = (_smoothY * (1 - _headingAlpha)) + (Math.Sin(radians) * _headingAlpha);
        }

        var smooth = Math.Atan2(_smoothY, _smoothX) * 180.0 / Math.PI;
        if (smooth < 0) {
            smooth += 360;
        }

        MainThread.BeginInvokeOnMainThread(() => {
            ViewModel.UpdateHeading(smooth);
        });
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(QiblaViewModel.SelectedHeadingMode)) {
            ApplyInitialDisplayMode();
            ApplyHeadingMode(restartCompass: true);
            ApplyDisplayState();
            return;
        }

        if (e.PropertyName == nameof(QiblaViewModel.SelectedReadingMode)
            || e.PropertyName == nameof(QiblaViewModel.SelectedFilterMode)) {
            ApplyCompassPreferences(true);
        }
    }

    private void ApplyCompassPreferences(bool restart) {
        var mode = ViewModel.SelectedReadingMode?.Value ?? QiblaReadingMode.Balanced;
        _filterMode = ViewModel.SelectedFilterMode?.Value ?? QiblaFilterMode.Normal;
        (_sensorSpeed, _applyLowPass, _headingAlpha) = mode switch {
            QiblaReadingMode.Smooth => (SensorSpeed.UI, true, 0.12),
            QiblaReadingMode.Fast => (SensorSpeed.Game, false, 0.28),
            QiblaReadingMode.Raw => (SensorSpeed.Fastest, false, 1.0),
            _ => (SensorSpeed.Default, true, 0.18)
        };
        _hasSmoothHeading = false;
        _lastAcceptedHeading = null;
        _consecutiveRejectedReadings = 0;
        if (restart && _compassSupported) {
            RestartCompass();
        }
    }

    private void StartCompass() {
        Compass.Start(_sensorSpeed, _applyLowPass);
    }

    private void RestartCompass() {
        try {
            if (Compass.IsMonitoring) {
                Compass.Stop();
            }
            StartCompass();
            _compassStarted = true;
            _lastCompassReadingUtc = DateTime.MinValue;
            _consecutiveRejectedReadings = 0;
        } catch {
            _compassSupported = false;
        }
    }

    private bool IsFiltered(double heading) {
        if (_filterMode == QiblaFilterMode.Off) {
            _lastAcceptedHeading = heading;
            _consecutiveRejectedReadings = 0;
            return false;
        }

        if (!_lastAcceptedHeading.HasValue) {
            _lastAcceptedHeading = heading;
            _consecutiveRejectedReadings = 0;
            return false;
        }

        var delta = NormalizeDelta(heading, _lastAcceptedHeading.Value);
        var threshold = _filterMode == QiblaFilterMode.Strict ? 20 : 45;
        if (Math.Abs(delta) > threshold) {
            _consecutiveRejectedReadings++;
            if (_consecutiveRejectedReadings < 3) {
                return true;
            }
        }

        _lastAcceptedHeading = heading;
        _consecutiveRejectedReadings = 0;
        return false;
    }

    private static double NormalizeDelta(double current, double previous) {
        var delta = current - previous;
        if (delta > 180) {
            delta -= 360;
        } else if (delta < -180) {
            delta += 360;
        }
        return delta;
    }

    private bool ShouldShowMap() {
        return _displayMode == QiblaDisplayMode.Map;
    }

    private void ApplyInitialDisplayMode() {
        if (ViewModel.IsManualHeadingMode) {
            _displayMode = QiblaDisplayMode.Compass;
        }
    }

    private void HandleCompassUnavailable() {
        _compassSupported = false;
        StopCompassWatchdog();
        StopCompassMonitoring();
        UpdateStatusMessage();
    }

    private void ApplyHeadingMode(bool restartCompass) {
        if (ViewModel.IsManualHeadingMode) {
            StopCompassMonitoring();
            UpdateStatusMessage();
            SetDisplayMode(QiblaDisplayMode.Compass);
            return;
        }

        if (!_compassSupported) {
            HandleCompassUnavailable();
            return;
        }

        try {
            if (restartCompass && Compass.IsMonitoring) {
                Compass.Stop();
            }

            if (restartCompass || !Compass.IsMonitoring) {
                _hasSmoothHeading = false;
                StartCompass();
                _compassStarted = true;
            }

            StartCompassWatchdog();
            UpdateStatusMessage();
        } catch (FeatureNotSupportedException) {
            HandleCompassUnavailable();
        } catch (Exception) {
            HandleCompassUnavailable();
        }
    }

    private void StopCompassMonitoring() {
        StopCompassWatchdog();
        if (_compassStarted && Compass.IsMonitoring) {
            Compass.Stop();
        }
        _compassStarted = false;
    }

    private void UpdateStatusMessage() {
        if (ViewModel.IsManualHeadingMode) {
            ViewModel.StatusMessage = LocalizationManager.Translate("QiblaManualHint");
            return;
        }

        ViewModel.StatusMessage = _compassSupported
            ? string.Empty
            : LocalizationManager.Translate("CompassNotSupported");
    }

    private void StartCompassWatchdog() {
        StopCompassWatchdog();
        _compassWatchdogCts = new CancellationTokenSource();
        var token = _compassWatchdogCts.Token;
        _ = Task.Run(async () => {
            var retries = 0;
            while (!token.IsCancellationRequested) {
                try {
                    await Task.Delay(2200, token).ConfigureAwait(false);
                } catch (TaskCanceledException) {
                    break;
                }

                if (!_compassSupported || !_compassStarted || token.IsCancellationRequested) {
                    continue;
                }

                if (_lastCompassReadingUtc == DateTime.MinValue || DateTime.UtcNow - _lastCompassReadingUtc > TimeSpan.FromSeconds(2.2)) {
                    retries++;
                    if (retries <= 3) {
                        MainThread.BeginInvokeOnMainThread(RestartCompass);
                    }
                } else {
                    retries = 0;
                }
            }
        }, token);
    }

    private void StopCompassWatchdog() {
        if (_compassWatchdogCts == null) {
            return;
        }

        try {
            _compassWatchdogCts.Cancel();
            _compassWatchdogCts.Dispose();
        } catch {
        } finally {
            _compassWatchdogCts = null;
        }
    }

    private void UpdateMap() {
        var location = ViewModel.Location;
        if (location == null) {
            return;
        }

        if (DeviceInfo.Platform == DevicePlatform.WinUI || DeviceInfo.Platform == DevicePlatform.Android) {
            SetWebMap(location.Latitude, location.Longitude);
            return;
        }

        EnsureMap();
        if (_map == null) {
            return;
        }

        var mapLocation = new Location(location.Latitude, location.Longitude);
        _map.MoveToRegion(MapSpan.FromCenterAndRadius(mapLocation, Distance.FromKilometers(3)));
        _map.Pins.Clear();
        _map.Pins.Add(new Pin {
            Label = LocalizationManager.Translate("MapYouPin"),
            Location = mapLocation
        });
    }

    private void EnsureMap() {
        if (_map != null) {
            return;
        }

        _map = new Microsoft.Maui.Controls.Maps.Map {
            HeightRequest = 220,
            IsShowingUser = true
        };
        MapHost.Content = _map;
    }

    private void SetWebMap(double latitude, double longitude) {
        var lat = latitude.ToString(CultureInfo.InvariantCulture);
        var lon = longitude.ToString(CultureInfo.InvariantCulture);
        const string kaabaLat = "21.422487";
        const string kaabaLon = "39.826206";

        var isDark = IsDarkTheme();
        var tileUrl = isDark
            ? "https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png"
            : "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";
        var attribution = isDark
            ? "&copy; OpenStreetMap &copy; CARTO"
            : "&copy; OpenStreetMap contributors";
        var lineColor = isDark ? "#2FB79D" : "#2C8A71";
        var background = isDark ? "#0B1426" : "#F3F6F4";

        var html = $@"
<!doctype html>
<html>
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"">
  <link rel=""stylesheet"" href=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.css""/>
  <style>
    html, body, #map {{ height: 100%; margin: 0; background: {background}; }}
  </style>
</head>
<body>
  <div id=""map""></div>
  <script src=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.js""></script>
  <script>
    var user = [{lat}, {lon}];
    var kaaba = [{kaabaLat}, {kaabaLon}];
    var map = L.map('map', {{ zoomControl: true }});
    L.tileLayer('{tileUrl}', {{
      maxZoom: 19,
      attribution: '{attribution}'
    }}).addTo(map);
    var userMarker = L.marker(user).addTo(map);
    var kaabaMarker = L.marker(kaaba).addTo(map);
    var line = L.polyline([user, kaaba], {{ color: '{lineColor}', weight: 3, opacity: 0.92 }}).addTo(map);
    map.fitBounds(line.getBounds().pad(0.2));
  </script>
</body>
</html>";
        QiblaWeb.Source = new HtmlWebViewSource { Html = html };
    }

    private async Task LoadAndUpdateAsync() {
        await ViewModel.LoadAsync();
        ThemeManager.RefreshTextScaleOnVisibleUIWithDeferredPasses();
        UpdateStatusMessage();

        if (ShouldShowMap()) {
            MainThread.BeginInvokeOnMainThread(UpdateMap);
        }
    }

    private void OnModeCompassTapped(object? sender, TappedEventArgs e) {
        SetDisplayMode(QiblaDisplayMode.Compass);
    }

    private void OnHeadingAutoTapped(object? sender, TappedEventArgs e) {
        var option = ViewModel.HeadingModes.FirstOrDefault(item => item.Value == QiblaHeadingMode.Sensor);
        if (option != null) {
            ViewModel.SelectedHeadingMode = option;
        }
    }

    private void OnHeadingManualTapped(object? sender, TappedEventArgs e) {
        var option = ViewModel.HeadingModes.FirstOrDefault(item => item.Value == QiblaHeadingMode.Manual);
        if (option != null) {
            ViewModel.SelectedHeadingMode = option;
        }
    }

    private void OnManualPanUpdated(object? sender, PanUpdatedEventArgs e) {
        if (!ViewModel.IsManualHeadingMode) {
            return;
        }

        switch (e.StatusType) {
            case GestureStatus.Started:
                _manualPanLastTotalX = 0;
                break;
            case GestureStatus.Running:
                var deltaX = e.TotalX - _manualPanLastTotalX;
                _manualPanLastTotalX = e.TotalX;
                ViewModel.AdjustManualHeading(deltaX * ManualHeadingSensitivity);
                break;
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _manualPanLastTotalX = 0;
                ViewModel.CommitManualHeading();
                break;
        }
    }

    private void OnModeMapTapped(object? sender, TappedEventArgs e) {
        SetDisplayMode(QiblaDisplayMode.Map);
    }

    private void OnFilterNoneTapped(object? sender, TappedEventArgs e) {
        SetVisualFilter(QiblaCompassVisualFilter.None);
    }

    private void OnFilterNightTapped(object? sender, TappedEventArgs e) {
        SetVisualFilter(QiblaCompassVisualFilter.Night);
    }

    private void OnFilterContrastTapped(object? sender, TappedEventArgs e) {
        SetVisualFilter(QiblaCompassVisualFilter.Contrast);
    }

    private void SetDisplayMode(QiblaDisplayMode mode) {
        if (_displayMode == mode) {
            return;
        }

        _displayMode = mode;
        ApplyDisplayState();
        if (_displayMode == QiblaDisplayMode.Map) {
            UpdateMap();
        }
    }

    private void SetVisualFilter(QiblaCompassVisualFilter filter) {
        if (_visualFilter == filter) {
            return;
        }

        _visualFilter = filter;
        ApplyDisplayState();
    }

    private void ApplyDisplayState() {
        CompassDial.VisualFilter = _visualFilter;

        var isDark = IsDarkTheme();
        var inactiveBackground = isDark ? Color.FromArgb("#16283A") : Color.FromArgb("#E8F0EB");
        var inactiveStroke = isDark ? Color.FromArgb("#234258") : Color.FromArgb("#C5D6CE");
        var inactiveText = isDark ? Color.FromArgb("#8CA0B6") : Color.FromArgb("#556B7E");
        var activeBackground = Color.FromArgb("#2FB79D");
        var activeStroke = Color.FromArgb("#2FB79D");
        var activeText = Color.FromArgb("#EAF7F8");

        SetChipState(ModeCompassChip, ModeCompassLabel, _displayMode == QiblaDisplayMode.Compass,
            activeBackground, activeStroke, activeText, inactiveBackground, inactiveStroke, inactiveText);
        SetChipState(ModeMapChip, ModeMapLabel, _displayMode == QiblaDisplayMode.Map,
            activeBackground, activeStroke, activeText, inactiveBackground, inactiveStroke, inactiveText);
        SetChipState(HeadingAutoChip, HeadingAutoLabel, !ViewModel.IsManualHeadingMode,
            activeBackground, activeStroke, activeText, inactiveBackground, inactiveStroke, inactiveText);
        SetChipState(HeadingManualChip, HeadingManualLabel, ViewModel.IsManualHeadingMode,
            activeBackground, activeStroke, activeText, inactiveBackground, inactiveStroke, inactiveText);

        SetChipState(FilterNoneChip, FilterNoneLabel, _visualFilter == QiblaCompassVisualFilter.None,
            activeBackground, activeStroke, activeText, inactiveBackground, inactiveStroke, inactiveText);
        SetChipState(FilterNightChip, FilterNightLabel, _visualFilter == QiblaCompassVisualFilter.Night,
            activeBackground, activeStroke, activeText, inactiveBackground, inactiveStroke, inactiveText);
        SetChipState(FilterContrastChip, FilterContrastLabel, _visualFilter == QiblaCompassVisualFilter.Contrast,
            activeBackground, activeStroke, activeText, inactiveBackground, inactiveStroke, inactiveText);

        CompassCard.IsVisible = _displayMode == QiblaDisplayMode.Compass;
        MapContainer.IsVisible = _displayMode == QiblaDisplayMode.Map;
    }

    private static void SetChipState(
        Border border,
        Label label,
        bool selected,
        Color activeBackground,
        Color activeStroke,
        Color activeText,
        Color inactiveBackground,
        Color inactiveStroke,
        Color inactiveText) {
        border.BackgroundColor = selected ? activeBackground : inactiveBackground;
        border.Stroke = selected ? new SolidColorBrush(activeStroke) : new SolidColorBrush(inactiveStroke);
        label.TextColor = selected ? activeText : inactiveText;
    }

    private static bool IsDarkTheme() {
        var theme = Application.Current?.UserAppTheme ?? AppTheme.Unspecified;
        if (theme == AppTheme.Unspecified) {
            theme = Application.Current?.RequestedTheme ?? AppTheme.Unspecified;
        }
        return theme != AppTheme.Light;
    }
}
