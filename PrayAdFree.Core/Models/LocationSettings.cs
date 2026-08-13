namespace PrayAdFree.Core.Models;

public enum LocationMode {
    Gps,
    Manual
}

public sealed class LocationSettings {
    public LocationMode Mode { get; init; } = LocationMode.Gps;
    public string City { get; init; } = "";
    public string Country { get; init; } = "";
    public string CountryCode { get; init; } = "";
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string TimeZoneId { get; init; } = "";
    public DateTime? LastUpdatedUtc { get; init; }
    public string Source { get; init; } = "";
}
