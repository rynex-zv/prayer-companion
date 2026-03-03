using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Pray_Ad_Free.Services;

public sealed class NominatimGeoProvider : IGeoProvider {
    private readonly HttpClient _httpClient;

    public NominatimGeoProvider(HttpClient httpClient) {
        _httpClient = httpClient;
    }

    public async Task<GeoLocationResult?> ReverseAsync(double latitude, double longitude, CancellationToken cancellationToken) {
        var url = $"reverse?format=jsonv2&lat={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lon={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&zoom=10&addressdetails=1";
        try {
            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) {
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<NominatimReverseResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
            return MapReverse(payload);
        } catch {
            return null;
        }
    }

    public async Task<GeoLocationResult?> ForwardAsync(string query, CancellationToken cancellationToken) {
        var url = $"search?format=jsonv2&q={Uri.EscapeDataString(query)}&limit=1&addressdetails=1";
        try {
            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) {
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<List<NominatimSearchResponse>>(cancellationToken: cancellationToken).ConfigureAwait(false);
            var item = payload?.FirstOrDefault();
            return MapForward(item);
        } catch {
            return null;
        }
    }

    private static GeoLocationResult? MapReverse(NominatimReverseResponse? response) {
        if (response?.Address == null) {
            return null;
        }

        var address = response.Address;
        var city = address.City ?? address.Town ?? address.Village ?? address.State ?? "";
        return new GeoLocationResult {
            City = city,
            Country = address.Country ?? "",
            CountryCode = address.CountryCode ?? "",
            Latitude = response.Lat,
            Longitude = response.Lon
        };
    }

    private static GeoLocationResult? MapForward(NominatimSearchResponse? response) {
        if (response?.Address == null) {
            return null;
        }

        var address = response.Address;
        var city = address.City ?? address.Town ?? address.Village ?? address.State ?? "";
        return new GeoLocationResult {
            City = city,
            Country = address.Country ?? "",
            CountryCode = address.CountryCode ?? "",
            Latitude = response.Lat,
            Longitude = response.Lon
        };
    }

    private sealed class NominatimReverseResponse {
        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lon")]
        public double Lon { get; set; }

        [JsonPropertyName("address")]
        public NominatimAddress? Address { get; set; }
    }

    private sealed class NominatimSearchResponse {
        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lon")]
        public double Lon { get; set; }

        [JsonPropertyName("address")]
        public NominatimAddress? Address { get; set; }
    }

    private sealed class NominatimAddress {
        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("town")]
        public string? Town { get; set; }

        [JsonPropertyName("village")]
        public string? Village { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("country_code")]
        public string? CountryCode { get; set; }
    }
}
