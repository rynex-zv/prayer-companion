using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;
using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;
using System.Globalization;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace Pray_Ad_Free.Pages.ThemeA;

public partial class QiblaPageA : ContentPage {
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
    private Microsoft.Maui.Controls.Maps.Map? _map;
    private double _manualPanLastTotalX;
    public QiblaPageA() : this( ServiceHelper.GetService<QiblaViewModel>() ) {
    }

    public QiblaPageA(QiblaViewModel viewModel) {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override async void OnAppearing() {
        base.OnAppearing();
        ThemeManager.RefreshTextScaleOnVisibleUIWithDeferredPasses();
        ApplyDesktopFallbackState();
        _ = LoadAndUpdateAsync();
        ApplyCompassPreferences(false);
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
        Compass.ReadingChanged -= OnCompassReadingChanged;
        if (_compassStarted && Compass.IsMonitoring) {
            Compass.Stop();
        }
        _compassStarted = false;
    }

    private void OnCompassReadingChanged(object? sender, CompassChangedEventArgs e) {
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
            ApplyHeadingMode(restartCompass: true);
            ApplyDesktopFallbackState();
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
        } catch {
            _compassSupported = false;
        }
    }

    private bool IsFiltered(double heading) {
        if (_filterMode == QiblaFilterMode.Off) {
            _lastAcceptedHeading = heading;
            return false;
        }

        if (!_lastAcceptedHeading.HasValue) {
            _lastAcceptedHeading = heading;
            return false;
        }

        var delta = NormalizeDelta(heading, _lastAcceptedHeading.Value);
        var threshold = _filterMode == QiblaFilterMode.Strict ? 20 : 45;
        if (Math.Abs(delta) > threshold) {
            return true;
        }

        _lastAcceptedHeading = heading;
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
        return true;
    }

    private void ApplyDesktopFallbackState() {
        CompassCard.IsVisible = true;
        MapContainer.IsVisible = ShouldShowMap();
    }

    private void HandleCompassUnavailable() {
        _compassSupported = false;
        StopCompassMonitoring();
        UpdateStatusMessage();
        ApplyDesktopFallbackState();
    }

    private void ApplyHeadingMode(bool restartCompass) {
        if (ViewModel.IsManualHeadingMode) {
            StopCompassMonitoring();
            UpdateStatusMessage();
            ApplyDesktopFallbackState();
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

            UpdateStatusMessage();
            ApplyDesktopFallbackState();
        } catch (FeatureNotSupportedException) {
            HandleCompassUnavailable();
        } catch (Exception) {
            HandleCompassUnavailable();
        }
    }

    private void StopCompassMonitoring() {
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
        var kaabaLat = QiblaCalculator.KaabaLatitudeDegrees.ToString(CultureInfo.InvariantCulture);
        var kaabaLon = QiblaCalculator.KaabaLongitudeDegrees.ToString(CultureInfo.InvariantCulture);
        var geodesicPath = string.Join(", ",
            QiblaCalculator.CreatePathToKaaba(latitude, longitude)
                .Select(point => $"[{point.Latitude.ToString(CultureInfo.InvariantCulture)}, {point.Longitude.ToString(CultureInfo.InvariantCulture)}]"));
        var html = $@"
<!doctype html>
<html>
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"">
  <link rel=""stylesheet"" href=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.css""/>
  <style>
    html, body, #map {{ height: 100%; margin: 0; background: #f3f1ec; }}
    #map {{
      touch-action: pan-y pinch-zoom;
      -ms-touch-action: pan-y pinch-zoom;
    }}
  </style>
</head>
<body>
  <div id=""map""></div>
  <script src=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.js""></script>
  <script>
    var user = [{lat}, {lon}];
    var kaaba = [{kaabaLat}, {kaabaLon}];
    var path = [{geodesicPath}];
    var map = L.map('map', {{
      zoomControl: true,
      dragging: false,
      tap: false,
      touchZoom: true
    }});
    L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
      maxZoom: 19,
      attribution: '&copy; OpenStreetMap contributors'
    }}).addTo(map);
    var userMarker = L.marker(user).addTo(map);
    var kaabaMarker = L.marker(kaaba).addTo(map);
    var line = L.polyline(path, {{ color: '#d35400', weight: 3, opacity: 0.85 }}).addTo(map);
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
        ApplyDesktopFallbackState();
        if (MapContainer.IsVisible) {
            MainThread.BeginInvokeOnMainThread(UpdateMap);
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
}
