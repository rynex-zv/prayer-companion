using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.ViewModels;

public sealed class QiblaViewModel : ViewModelBase {
    private readonly PrayerDataService _dataService;
    private double _bearing;
    private double _heading;
    private double _needleRotation;
    private string _locationTitle = "";
    private string _statusMessage = "";
    private LocationSettings? _location;

    public QiblaViewModel(PrayerDataService dataService) {
        _dataService = dataService;
    }

    public double Bearing {
        get => _bearing;
        set => SetProperty(ref _bearing, value);
    }

    public double Heading {
        get => _heading;
        set => SetProperty(ref _heading, value);
    }

    public double NeedleRotation {
        get => _needleRotation;
        set => SetProperty(ref _needleRotation, value);
    }

    public string LocationTitle {
        get => _locationTitle;
        set => SetProperty(ref _locationTitle, value);
    }

    public string StatusMessage {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public LocationSettings? Location => _location;

    public async Task LoadAsync() {
        StatusMessage = "Finding location...";
        var settings = _dataService.LoadSettings();
        var updated = await _dataService.UpdateLocationAsync(settings, CancellationToken.None);
        _location = updated.Location;
        if (_location != null) {
            Bearing = QiblaCalculator.CalculateBearing(_location.Latitude, _location.Longitude);
            LocationTitle = $"{_location.City}, {_location.Country}".Trim(' ', ',');
            UpdateNeedle();
            StatusMessage = "Calibrate compass by moving your device.";
        }
    }

    public void UpdateHeading(double heading) {
        Heading = heading;
        UpdateNeedle();
    }

    private void UpdateNeedle() {
        NeedleRotation = (Bearing - Heading + 360) % 360;
    }
}
