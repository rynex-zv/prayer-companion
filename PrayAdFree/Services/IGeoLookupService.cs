namespace Pray_Ad_Free.Services;

public interface IGeoLookupService {
    Task<GeoLocationResult?> ReverseAsync(double latitude, double longitude, CancellationToken cancellationToken);
    Task<GeoLocationResult?> ForwardAsync(string query, CancellationToken cancellationToken);
    IReadOnlyList<GeoLocationResult> GetKnownPlaces();
}
