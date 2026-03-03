namespace Pray_Ad_Free.Services;

public sealed class GeoLocationResult {
    public string City { get; init; } = "";
    public string Country { get; init; } = "";
    public string CountryCode { get; init; } = "";
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}
