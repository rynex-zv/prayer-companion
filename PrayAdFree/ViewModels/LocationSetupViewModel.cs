using System.Collections.ObjectModel;
using System.Globalization;
using PrayAdFree.Core.Models;
using Pray_Ad_Free.Models;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.ViewModels;

public sealed class LocationSetupViewModel : ViewModelBase {
    private readonly IGeoLookupService _geoLookupService;
    private readonly ILocationProvider _locationProvider;
    private readonly IAppPermissionCenterService _permissionCenterService;
    private readonly IAppLogger _logger;
    private bool _useGps;
    private string _city = string.Empty;
    private string _country = string.Empty;
    private string _latitude = string.Empty;
    private string _longitude = string.Empty;
    private PlaceOption? _selectedCountry;
    private PlaceOption? _selectedCity;
    private bool _gpsBusy;
    private bool _canUseGps;
    private bool _suspendUpdates;
    private bool _suspendPlaceSelection;
    private bool _hasUserEditedLocation;
    private int _geoVersion;
    private CancellationTokenSource? _gpsLoopCts;
    private LocationSettings _loadedLocation = new();

    public LocationSetupViewModel(
        IGeoLookupService geoLookupService,
        ILocationProvider locationProvider,
        IAppPermissionCenterService permissionCenterService,
        IAppLogger logger) {
        _geoLookupService = geoLookupService;
        _locationProvider = locationProvider;
        _permissionCenterService = permissionCenterService;
        _logger = logger;
        CountryOptions = new ObservableCollection<PlaceOption>();
        CityOptions = new ObservableCollection<PlaceOption>();
        RefreshGpsCommand = new Command(async () => await RefreshGpsAsync(), () => !GpsBusy && CanUseGps);
    }

    public ObservableCollection<PlaceOption> CountryOptions { get; }
    public ObservableCollection<PlaceOption> CityOptions { get; }
    public Command RefreshGpsCommand { get; }

    public bool UseGps {
        get => _useGps;
        set {
            if (!SetProperty(ref _useGps, value)) {
                return;
            }

            OnPropertyChanged(nameof(IsManualLocationEnabled));
            if (_suspendUpdates) {
                return;
            }

            if (value) {
                _ = EnableGpsIfPermittedAsync();
            } else {
                StopGpsLoop();
            }
        }
    }

    public bool IsManualLocationEnabled => !UseGps;

    public bool GpsBusy {
        get => _gpsBusy;
        private set {
            if (!MainThread.IsMainThread) {
                MainThread.BeginInvokeOnMainThread(() => GpsBusy = value);
                return;
            }

            if (SetProperty(ref _gpsBusy, value)) {
                RefreshGpsCommand.ChangeCanExecute();
            }
        }
    }

    public bool CanUseGps {
        get => _canUseGps;
        private set {
            if (SetProperty(ref _canUseGps, value)) {
                RefreshGpsCommand.ChangeCanExecute();
            }
        }
    }

    public bool HasUserEditedLocation {
        get => _hasUserEditedLocation;
        private set => SetProperty(ref _hasUserEditedLocation, value);
    }

    public string City {
        get => _city;
        set {
            if (SetProperty(ref _city, value) && !_suspendUpdates) {
                HasUserEditedLocation = true;
                if (UseGps) {
                    UseGps = false;
                }
            }
        }
    }

    public string Country {
        get => _country;
        set {
            if (SetProperty(ref _country, value) && !_suspendUpdates) {
                HasUserEditedLocation = true;
                if (UseGps) {
                    UseGps = false;
                }
            }
        }
    }

    public string Latitude {
        get => _latitude;
        set {
            if (SetProperty(ref _latitude, value) && !_suspendUpdates) {
                HasUserEditedLocation = true;
                if (UseGps) {
                    UseGps = false;
                }
                ScheduleReverseLookup();
                OnPropertyChanged(nameof(HasUsableLocation));
            }
        }
    }

    public string Longitude {
        get => _longitude;
        set {
            if (SetProperty(ref _longitude, value) && !_suspendUpdates) {
                HasUserEditedLocation = true;
                if (UseGps) {
                    UseGps = false;
                }
                ScheduleReverseLookup();
                OnPropertyChanged(nameof(HasUsableLocation));
            }
        }
    }

    public PlaceOption? SelectedCountry {
        get => _selectedCountry;
        set {
            if (SetProperty(ref _selectedCountry, value) && !_suspendPlaceSelection) {
                HasUserEditedLocation = true;
                _ = ApplyCountrySelectionAsync(value);
            }
        }
    }

    public PlaceOption? SelectedCity {
        get => _selectedCity;
        set {
            if (SetProperty(ref _selectedCity, value) && !_suspendPlaceSelection) {
                HasUserEditedLocation = true;
                ApplyCitySelection(value);
            }
        }
    }

    public bool HasUsableLocation {
        get {
            if (!TryParseCoordinates(out var latitude, out var longitude)) {
                return false;
            }

            return latitude is >= -90 and <= 90
                && longitude is >= -180 and <= 180
                && (Math.Abs(latitude) > 0.000001 || Math.Abs(longitude) > 0.000001);
        }
    }

    public void Load(LocationSettings location, bool startGpsTracking = false) {
        StopGpsLoop();
        _loadedLocation = location ?? new LocationSettings();
        _suspendUpdates = true;
        UseGps = _loadedLocation.Mode == LocationMode.Gps;
        City = NormalizeName(_loadedLocation.City);
        Country = NormalizeName(_loadedLocation.Country);
        Latitude = FormatCoordinate(_loadedLocation.Latitude);
        Longitude = FormatCoordinate(_loadedLocation.Longitude);
        HasUserEditedLocation = false;
        _suspendUpdates = false;
        BuildPlaceOptions();
        _ = RefreshGpsPermissionStateAsync();

        if (startGpsTracking && UseGps) {
            _ = EnableGpsIfPermittedAsync();
        }

        OnPropertyChanged(nameof(HasUsableLocation));
    }

    public LocationSettings BuildLocationSettings(LocationSettings fallback) {
        var parsed = TryParseCoordinates(out var latitude, out var longitude);
        return new LocationSettings {
            Mode = UseGps ? LocationMode.Gps : LocationMode.Manual,
            City = NormalizeName(City),
            Country = NormalizeName(Country),
            CountryCode = fallback.CountryCode,
            Latitude = parsed ? latitude : fallback.Latitude,
            Longitude = parsed ? longitude : fallback.Longitude,
            TimeZoneId = string.IsNullOrWhiteSpace(fallback.TimeZoneId) ? TimeZoneInfo.Local.Id : fallback.TimeZoneId,
            LastUpdatedUtc = fallback.LastUpdatedUtc
        };
    }

    public void RefreshLocalizedPlaceOptions() {
        BuildPlaceOptions();
        OnPropertyChanged(nameof(HasUsableLocation));
    }

    public void ApplyAutofillLocation(GeoLocationResult location) {
        if (HasUserEditedLocation || HasUsableLocation) {
            return;
        }

        _loadedLocation = new LocationSettings {
            Mode = LocationMode.Manual,
            City = NormalizeName(location.City),
            Country = NormalizeName(location.Country),
            CountryCode = location.CountryCode,
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            TimeZoneId = TimeZoneInfo.Local.Id,
            LastUpdatedUtc = DateTime.UtcNow
        };

        _suspendUpdates = true;
        UseGps = false;
        City = _loadedLocation.City;
        Country = _loadedLocation.Country;
        Latitude = FormatCoordinate(_loadedLocation.Latitude);
        Longitude = FormatCoordinate(_loadedLocation.Longitude);
        HasUserEditedLocation = false;
        _suspendUpdates = false;

        BuildPlaceOptions();
        OnPropertyChanged(nameof(HasUsableLocation));
    }

    public async Task RefreshGpsAsync() {
        if (_suspendUpdates || GpsBusy) {
            return;
        }

        try {
            GpsBusy = true;
            if (!await HasLocationPermissionAsync().ConfigureAwait(false)) {
                RunOnMainThread(() => {
                    _suspendUpdates = true;
                    UseGps = false;
                    _suspendUpdates = false;
                });
                StopGpsLoop();
                return;
            }

            var current = BuildLocationSettings(_loadedLocation);
            current = new LocationSettings {
                Mode = LocationMode.Gps,
                City = current.City,
                Country = current.Country,
                CountryCode = current.CountryCode,
                Latitude = current.Latitude,
                Longitude = current.Longitude,
                TimeZoneId = current.TimeZoneId,
                LastUpdatedUtc = current.LastUpdatedUtc
            };

            var updated = await _locationProvider.GetLocationAsync(current, CancellationToken.None).ConfigureAwait(false);
            RunOnMainThread(() => {
                _loadedLocation = updated;
                _suspendUpdates = true;
                UseGps = true;
                City = NormalizeName(updated.City);
                Country = NormalizeName(updated.Country);
                Latitude = FormatCoordinate(updated.Latitude);
                Longitude = FormatCoordinate(updated.Longitude);
                HasUserEditedLocation = false;
                _suspendUpdates = false;
                BuildPlaceOptions();
                OnPropertyChanged(nameof(HasUsableLocation));
            });
        } catch (Exception ex) {
            _logger.LogException(ex, "LocationSetupViewModel.RefreshGpsAsync");
        } finally {
            GpsBusy = false;
        }
    }

    private async Task EnableGpsIfPermittedAsync() {
        if (!await HasLocationPermissionAsync().ConfigureAwait(false)) {
            RunOnMainThread(() => {
                _suspendUpdates = true;
                UseGps = false;
                _suspendUpdates = false;
            });
            StopGpsLoop();
            return;
        }

        StartGpsLoop();
    }

    private async Task<bool> HasLocationPermissionAsync() {
        var snapshots = await _permissionCenterService.GetSnapshotsAsync().ConfigureAwait(false);
        var snapshot = snapshots.FirstOrDefault(item => item.Kind == AppPermissionKind.Location);
        var granted = snapshot.IsSupported && snapshot.IsGranted;
        RunOnMainThread(() => {
            CanUseGps = granted;
            if (!granted && UseGps) {
                _suspendUpdates = true;
                UseGps = false;
                _suspendUpdates = false;
            }
        });
        return granted;
    }

    private async Task RefreshGpsPermissionStateAsync() {
        await HasLocationPermissionAsync().ConfigureAwait(false);
    }

    public async Task ResolveCoordinatesAsync() {
        if (!TryParseCoordinates(out var latitude, out var longitude)) {
            return;
        }

        var result = await _geoLookupService.ReverseAsync(latitude, longitude, CancellationToken.None).ConfigureAwait(false);
        RunOnMainThread(() => {
            _suspendUpdates = true;
            UseGps = false;
            City = result?.City is { Length: > 0 }
                ? NormalizeName(result.City)
                : LocalizationManager.Translate("UnknownCity");
            Country = result?.Country is { Length: > 0 }
                ? NormalizeName(result.Country)
                : LocalizationManager.Translate("UnknownCountry");
            _suspendUpdates = false;
            BuildPlaceOptions();
            OnPropertyChanged(nameof(HasUsableLocation));
        });
    }

    public async Task ApplyCountrySelectionAsync(PlaceOption? option) {
        if (_suspendUpdates || option == null || string.IsNullOrWhiteSpace(option.Country)) {
            return;
        }

        var country = option.Country;
        var result = await _geoLookupService.ForwardAsync(country, CancellationToken.None).ConfigureAwait(false);
        RunOnMainThread(() => {
            _suspendUpdates = true;
            UseGps = false;
            Country = NormalizeName(country);
            City = result?.City is { Length: > 0 }
                ? NormalizeName(result.City)
                : LocalizationManager.Translate("UnknownCity");
            Latitude = result != null ? FormatCoordinate(result.Latitude) : string.Empty;
            Longitude = result != null ? FormatCoordinate(result.Longitude) : string.Empty;
            _suspendUpdates = false;
            UpdateCityOptions(country);
            OnPropertyChanged(nameof(HasUsableLocation));
        });
    }

    public void ApplyCitySelection(PlaceOption? option) {
        if (_suspendUpdates || option == null) {
            return;
        }

        _suspendUpdates = true;
        var matchesGps = UseGps
            && string.Equals(option.City, _loadedLocation.City, StringComparison.OrdinalIgnoreCase)
            && string.Equals(option.Country, _loadedLocation.Country, StringComparison.OrdinalIgnoreCase);
        if (matchesGps) {
            City = NormalizeName(_loadedLocation.City);
            Country = NormalizeName(_loadedLocation.Country);
            Latitude = FormatCoordinate(_loadedLocation.Latitude);
            Longitude = FormatCoordinate(_loadedLocation.Longitude);
            UseGps = true;
        } else {
            UseGps = false;
            City = NormalizeName(option.City);
            Country = NormalizeName(option.Country);
            Latitude = FormatCoordinate(option.Latitude);
            Longitude = FormatCoordinate(option.Longitude);
        }
        _suspendUpdates = false;
        OnPropertyChanged(nameof(HasUsableLocation));
    }

    private void BuildPlaceOptions() {
        var known = _geoLookupService.GetKnownPlaces()
            .Where(item => !string.IsNullOrWhiteSpace(item.Country))
            .ToList();

        if (!string.IsNullOrWhiteSpace(Country)) {
            known.Insert(0, new GeoLocationResult {
                Country = Country,
                City = City,
                CountryCode = _loadedLocation.CountryCode,
                Latitude = ParseDouble(Latitude),
                Longitude = ParseDouble(Longitude)
            });
        }

        var countries = known
            .GroupBy(item => NormalizeName(item.Country))
            .Select(group => group.First())
            .Select(item => new PlaceOption(
                NormalizeName(item.Country),
                NormalizeName(item.City),
                item.Latitude,
                item.Longitude,
                true))
            .ToList();

        if (countries.Count == 0) {
            countries.Add(new PlaceOption(LocalizationManager.Translate("UnknownCountry"), string.Empty, 0, 0, true));
        }

        RunOnMainThread(() => {
            CountryOptions.Clear();
            foreach (var option in countries) {
                CountryOptions.Add(option);
            }

            _suspendPlaceSelection = true;
            SelectedCountry = CountryOptions.FirstOrDefault(option => string.Equals(option.Country, Country, StringComparison.OrdinalIgnoreCase))
                ?? CountryOptions.FirstOrDefault();
            _suspendPlaceSelection = false;

            UpdateCityOptions(Country);
        });
    }

    private void UpdateCityOptions(string? country) {
        var known = _geoLookupService.GetKnownPlaces()
            .Where(item => string.Equals(NormalizeName(item.Country), NormalizeName(country), StringComparison.OrdinalIgnoreCase))
            .ToList();

        var cities = known
            .Select(item => new PlaceOption(
                NormalizeName(item.Country),
                NormalizeName(item.City),
                item.Latitude,
                item.Longitude,
                false))
            .Where(item => !string.IsNullOrWhiteSpace(item.City))
            .Where(item => !string.Equals(item.City, item.Country, StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.City, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (cities.Count == 0 && !string.IsNullOrWhiteSpace(country)) {
            cities.Add(new PlaceOption(country, LocalizationManager.Translate("UnknownCity"), 0, 0, false));
        }

        RunOnMainThread(() => {
            CityOptions.Clear();
            foreach (var city in cities) {
                CityOptions.Add(city);
            }

            _suspendPlaceSelection = true;
            SelectedCity = CityOptions.FirstOrDefault(option => string.Equals(option.City, City, StringComparison.OrdinalIgnoreCase))
                ?? CityOptions.FirstOrDefault();
            _suspendPlaceSelection = false;
        });
    }

    private void ScheduleReverseLookup() {
        _geoVersion++;
        var version = _geoVersion;
        _ = DebounceReverseAsync(version);
    }

    private async Task DebounceReverseAsync(int version) {
        await Task.Delay(700).ConfigureAwait(false);
        if (version != _geoVersion) {
            return;
        }

        await ResolveCoordinatesAsync().ConfigureAwait(false);
    }

    private void StartGpsLoop() {
        StopGpsLoop();
        _gpsLoopCts = new CancellationTokenSource();
        _ = GpsLoopAsync(_gpsLoopCts.Token);
    }

    private void StopGpsLoop() {
        if (_gpsLoopCts == null) {
            return;
        }

        try {
            _gpsLoopCts.Cancel();
            _gpsLoopCts.Dispose();
        } catch {
        } finally {
            _gpsLoopCts = null;
        }
    }

    private async Task GpsLoopAsync(CancellationToken token) {
        while (!token.IsCancellationRequested) {
            try {
                await RefreshGpsAsync().ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromMinutes(15), token).ConfigureAwait(false);
            } catch (TaskCanceledException) {
                break;
            } catch (Exception ex) {
                _logger.LogException(ex, "LocationSetupViewModel.GpsLoopAsync");
            }
        }
    }

    private bool TryParseCoordinates(out double latitude, out double longitude) {
        var okLatitude = double.TryParse(Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out latitude);
        var okLongitude = double.TryParse(Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out longitude);
        return okLatitude && okLongitude;
    }

    private static double ParseDouble(string text) {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static string FormatCoordinate(double value) {
        return Math.Abs(value) < 0.000001
            ? string.Empty
            : value.ToString("F4", CultureInfo.InvariantCulture);
    }

    private static void RunOnMainThread(Action action) {
        if (MainThread.IsMainThread) {
            action();
        } else {
            MainThread.BeginInvokeOnMainThread(action);
        }
    }

    private static string NormalizeName(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        var trimmed = value.Trim();
        for (var len = 1; len <= trimmed.Length / 2; len++) {
            if (trimmed.Length % len != 0) {
                continue;
            }

            var segment = trimmed.Substring(0, len);
            var repeats = trimmed.Length / len;
            var allMatch = true;
            for (var i = 1; i < repeats; i++) {
                if (!string.Equals(segment, trimmed.Substring(i * len, len), StringComparison.Ordinal)) {
                    allMatch = false;
                    break;
                }
            }

            if (allMatch) {
                return segment.Trim();
            }
        }

        return trimmed;
    }
}
