using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Services;

public interface ILocationProvider {
    Task<LocationSettings> GetLocationAsync(LocationSettings current, CancellationToken cancellationToken);
}
