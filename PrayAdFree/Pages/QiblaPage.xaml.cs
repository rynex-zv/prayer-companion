using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;
using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;
using System.Globalization;

namespace Pray_Ad_Free.Pages;

public partial class QiblaPage : ContentPage {
    private QiblaViewModel ViewModel => (QiblaViewModel)BindingContext;
    private bool _animated;
    private bool _compassSupported = true;
    private bool _compassStarted;
    private bool _hasSmoothHeading;
    private double _smoothX;
    private double _smoothY;
    private const double _headingAlpha = 0.18;
    private Microsoft.Maui.Controls.Maps.Map? _map;

    public QiblaPage() : this( ServiceHelper.GetService<QiblaViewModel>() ) {
    }

    public QiblaPage(QiblaViewModel viewModel) {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing() {
        base.OnAppearing();
        _ = LoadAndUpdateAsync();
        _compassSupported = Compass.IsSupported;
        if (_compassSupported) {
            Compass.ReadingChanged += OnCompassReadingChanged;
            try {
                _hasSmoothHeading = false;
                Compass.Start(SensorSpeed.UI, true);
                _compassStarted = true;
            } catch (FeatureNotSupportedException) {
                _compassSupported = false;
                ViewModel.StatusMessage = LocalizationManager.Translate("CompassNotSupported");
            } catch (Exception) {
                _compassSupported = false;
                ViewModel.StatusMessage = LocalizationManager.Translate("CompassNotSupported");
            }
        } else {
            ViewModel.StatusMessage = LocalizationManager.Translate("CompassNotSupported");
        }

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
            Label = "You",
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
        var html = $@"
<!doctype html>
<html>
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"">
  <link rel=""stylesheet"" href=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.css""/>
  <style>
    html, body, #map {{ height: 100%; margin: 0; background: #f3f1ec; }}
  </style>
</head>
<body>
  <div id=""map""></div>
  <script src=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.js""></script>
  <script>
    var map = L.map('map', {{ zoomControl: true }}).setView([{lat}, {lon}], 12);
    L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
      maxZoom: 19,
      attribution: '&copy; OpenStreetMap contributors'
    }}).addTo(map);
    L.marker([{lat}, {lon}]).addTo(map);
  </script>
</body>
</html>";
        QiblaWeb.Source = new HtmlWebViewSource { Html = html };
    }

    private async Task LoadAndUpdateAsync() {
        await ViewModel.LoadAsync();
        if (!_compassSupported) {
            ViewModel.StatusMessage = LocalizationManager.Translate("CompassNotSupported");
        }
        MainThread.BeginInvokeOnMainThread(UpdateMap);
    }
}
