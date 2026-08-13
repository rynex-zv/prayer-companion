using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui.Networking;

namespace Pray_Ad_Free.Services;

public sealed class IpLocationService : IIpLocationService {
    private readonly HttpClient _httpClient;
    private readonly IAppLogger _logger;

    public IpLocationService(HttpClient httpClient, IAppLogger logger) {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<GeoLocationResult?> GetCurrentLocationAsync(CancellationToken cancellationToken) {
        try {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) {
                return null;
            }

            var response = await _httpClient.GetFromJsonAsync<IpApiResponse>("json/", cancellationToken).ConfigureAwait(false);
            if (response == null || !HasValidCoordinates(response.Latitude, response.Longitude)) {
                return null;
            }

            return new GeoLocationResult {
                City = response.City ?? "",
                Country = response.CountryName ?? "",
                CountryCode = response.CountryCode ?? "",
                Latitude = response.Latitude,
                Longitude = response.Longitude,
                TimeZoneId = response.TimeZone ?? ""
            };
        } catch (Exception ex) {
            _logger.LogException(ex, "IpLocationService.GetCurrentLocationAsync");
            return null;
        }
    }

    private static bool HasValidCoordinates(double latitude, double longitude) {
        return latitude is >= -90 and <= 90
            && longitude is >= -180 and <= 180
            && (Math.Abs(latitude) > 0.000001 || Math.Abs(longitude) > 0.000001);
    }

    private sealed class IpApiResponse {
        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("country_name")]
        public string? CountryName { get; set; }

        [JsonPropertyName("country_code")]
        public string? CountryCode { get; set; }

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("timezone")]
        public string? TimeZone { get; set; }
    }
}
