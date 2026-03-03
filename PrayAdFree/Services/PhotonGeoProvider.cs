using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Pray_Ad_Free.Services;

public sealed class PhotonGeoProvider : IGeoProvider {
    private readonly HttpClient _httpClient;

    public PhotonGeoProvider(HttpClient httpClient) {
        _httpClient = httpClient;
    }

    public async Task<GeoLocationResult?> ReverseAsync(double latitude, double longitude, CancellationToken cancellationToken) {
        var url = $"reverse?lat={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lon={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&limit=1";
        var response = await _httpClient.GetFromJsonAsync<PhotonResponse>(url, cancellationToken).ConfigureAwait(false);
        return Map(response);
    }

    public async Task<GeoLocationResult?> ForwardAsync(string query, CancellationToken cancellationToken) {
        var url = $"api/?q={Uri.EscapeDataString(query)}&limit=1";
        var response = await _httpClient.GetFromJsonAsync<PhotonResponse>(url, cancellationToken).ConfigureAwait(false);
        return Map(response);
    }

    private static GeoLocationResult? Map(PhotonResponse? response) {
        var feature = response?.Features?.FirstOrDefault();
        if (feature?.Properties == null || feature.Geometry?.Coordinates?.Length != 2) {
            return null;
        }

        var props = feature.Properties;
        var city = props.City ?? props.Town ?? props.Village ?? props.Name ?? "";
        return new GeoLocationResult {
            City = city,
            Country = props.Country ?? "",
            CountryCode = props.CountryCode ?? "",
            Longitude = feature.Geometry.Coordinates[0],
            Latitude = feature.Geometry.Coordinates[1]
        };
    }

    private sealed class PhotonResponse {
        [JsonPropertyName("features")]
        public List<PhotonFeature>? Features { get; set; }
    }

    private sealed class PhotonFeature {
        [JsonPropertyName("properties")]
        public PhotonProperties? Properties { get; set; }

        [JsonPropertyName("geometry")]
        public PhotonGeometry? Geometry { get; set; }
    }

    private sealed class PhotonProperties {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("town")]
        public string? Town { get; set; }

        [JsonPropertyName("village")]
        public string? Village { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("countrycode")]
        public string? CountryCode { get; set; }
    }

    private sealed class PhotonGeometry {
        [JsonPropertyName("coordinates")]
        public double[]? Coordinates { get; set; }
    }
}
