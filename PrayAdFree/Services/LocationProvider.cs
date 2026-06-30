using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Services;

public sealed class LocationProvider : ILocationProvider {
    private readonly GeoService _geoService;
    private readonly IAppLogger _logger;

    public LocationProvider(GeoService geoService, IAppLogger logger) {
        _geoService = geoService;
        _logger = logger;
    }

    public async Task<LocationSettings> GetLocationAsync(LocationSettings current, CancellationToken cancellationToken) {
        return current.Mode == LocationMode.Gps
            ? await GetFromGpsAsync(current, cancellationToken).ConfigureAwait(false)
            : await GetFromManualAsync(current, cancellationToken).ConfigureAwait(false);
    }

    private async Task<LocationSettings> GetFromGpsAsync(LocationSettings current, CancellationToken cancellationToken) {
        try {
            var status = await CheckLocationPermissionAsync().ConfigureAwait(false);
            if (status != PermissionStatus.Granted) {
                return current;
            }

            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
            var location = MainThread.IsMainThread
                ? await Geolocation.GetLocationAsync(request, cancellationToken).ConfigureAwait(false)
                : await MainThread.InvokeOnMainThreadAsync(
                    () => Geolocation.GetLocationAsync(request, cancellationToken)
                ).ConfigureAwait(false);
            if (location == null) {
                return current;
            }

            var reverse = await _geoService.ReverseAsync(location.Latitude, location.Longitude, cancellationToken)
                .ConfigureAwait(false);
            var city = reverse?.City ?? LocalizationManager.Translate("UnknownCity");
            var country = reverse?.Country ?? LocalizationManager.Translate("UnknownCountry");
            return new LocationSettings {
                Mode = LocationMode.Gps,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                City = city,
                Country = country,
                CountryCode = reverse?.CountryCode ?? "",
                TimeZoneId = TimeZoneInfo.Local.Id,
                LastUpdatedUtc = DateTime.UtcNow
            };
        } catch (Exception ex) {
            _logger.LogException(ex, "LocationProvider.GetFromGpsAsync");
            return current;
        }
    }

    private async Task<LocationSettings> GetFromManualAsync(LocationSettings current, CancellationToken cancellationToken) {
        try {
            if (current.Latitude != 0 && current.Longitude != 0) {
                return current;
            }

            if (!string.IsNullOrWhiteSpace(current.City) && !string.IsNullOrWhiteSpace(current.Country)) {
                var result = await _geoService.ForwardAsync($"{current.City}, {current.Country}", cancellationToken)
                    .ConfigureAwait(false);
                if (result != null) {
                    return new LocationSettings {
                        Mode = LocationMode.Manual,
                        City = current.City,
                        Country = current.Country,
                        CountryCode = current.CountryCode,
                        Latitude = result.Latitude,
                        Longitude = result.Longitude,
                        TimeZoneId = TimeZoneInfo.Local.Id,
                        LastUpdatedUtc = DateTime.UtcNow
                    };
                }
            }

            return current;
        } catch (Exception ex) {
            _logger.LogException(ex, "LocationProvider.GetFromManualAsync");
            return current;
        }
    }

    private static Task<PermissionStatus> CheckLocationPermissionAsync() {
        if (MainThread.IsMainThread) {
            return Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        }

        return MainThread.InvokeOnMainThreadAsync(() => Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>());
    }
}
