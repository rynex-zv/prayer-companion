namespace Pray_Ad_Free.Models;

public sealed class PlaceOption {
    public PlaceOption(string country, string city, double latitude, double longitude, bool isCountry) {
        Country = country;
        City = city;
        Latitude = latitude;
        Longitude = longitude;
        IsCountry = isCountry;
    }

    public string Country { get; }
    public string City { get; }
    public double Latitude { get; }
    public double Longitude { get; }
    public bool IsCountry { get; }

    public string Label => IsCountry ? Country : City;
}
