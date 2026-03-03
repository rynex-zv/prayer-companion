using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Services;

public sealed class LocationProvider : ILocationProvider {
    private readonly GeoService _geoService;

    public LocationProvider(GeoService geoService) {
        _geoService = geoService;
    }

    public async Task<LocationSettings> GetLocationAsync(LocationSettings current, CancellationToken cancellationToken) {
        return current.Mode == LocationMode.Gps
            ? await GetFromGpsAsync(current, cancellationToken).ConfigureAwait(false)
            : await GetFromManualAsync(current, cancellationToken).ConfigureAwait(false);
    }

    private async Task<LocationSettings> GetFromGpsAsync(LocationSettings current, CancellationToken cancellationToken) {
        var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted) {
            return current;
        }

        var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
        var location = await Geolocation.GetLocationAsync(request, cancellationToken).ConfigureAwait(false);
        if (location == null) {
            return current;
        }

        var reverse = await _geoService.ReverseAsync(location.Latitude, location.Longitude, cancellationToken).ConfigureAwait(false);
        return new LocationSettings {
            Mode = LocationMode.Gps,
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            City = reverse?.City ?? current.City,
            Country = reverse?.Country ?? current.Country,
            CountryCode = reverse?.CountryCode ?? current.CountryCode,
            TimeZoneId = TimeZoneInfo.Local.Id,
            LastUpdatedUtc = DateTime.UtcNow
        };
    }

    private async Task<LocationSettings> GetFromManualAsync(LocationSettings current, CancellationToken cancellationToken) {
        if (current.Latitude != 0 && current.Longitude != 0) {
            return current;
        }

        if (!string.IsNullOrWhiteSpace(current.City) && !string.IsNullOrWhiteSpace(current.Country)) {
            var result = await _geoService.ForwardAsync($"{current.City}, {current.Country}", cancellationToken).ConfigureAwait(false);
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
    }
}
