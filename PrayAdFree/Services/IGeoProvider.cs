namespace Pray_Ad_Free.Services;

public interface IGeoProvider {
    Task<GeoLocationResult?> ReverseAsync(double latitude, double longitude, CancellationToken cancellationToken);
    Task<GeoLocationResult?> ForwardAsync(string query, CancellationToken cancellationToken);
}
