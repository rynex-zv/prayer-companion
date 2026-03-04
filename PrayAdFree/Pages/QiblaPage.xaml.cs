using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;
using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages;

public partial class QiblaPage : ContentPage {
    private QiblaViewModel ViewModel => (QiblaViewModel)BindingContext;
    private bool _animated;
    private bool _compassSupported = true;
    private bool _compassStarted;
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
                Compass.Start(SensorSpeed.UI);
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
        MainThread.BeginInvokeOnMainThread(() => {
            ViewModel.UpdateHeading(e.Reading.HeadingMagneticNorth);
        });
    }

    private void UpdateMap() {
        var location = ViewModel.Location;
        if (location == null) {
            return;
        }

        if (DeviceInfo.Platform == DevicePlatform.WinUI) {
            var url = $"https://www.openstreetmap.org/?mlat={location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&mlon={location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}#map=12/{location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}/{location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            QiblaWeb.Source = new UrlWebViewSource { Url = url };
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

    private async Task LoadAndUpdateAsync() {
        await ViewModel.LoadAsync();
        if (!_compassSupported) {
            ViewModel.StatusMessage = LocalizationManager.Translate("CompassNotSupported");
        }
        MainThread.BeginInvokeOnMainThread(UpdateMap);
    }
}
