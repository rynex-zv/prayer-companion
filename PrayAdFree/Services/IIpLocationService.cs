namespace Pray_Ad_Free.Services;

public interface IIpLocationService {
    Task<GeoLocationResult?> GetCurrentLocationAsync(CancellationToken cancellationToken);
}
