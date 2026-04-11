using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Services;

public interface IAppPermissionCenterService {
    Task<IReadOnlyList<AppPermissionSnapshot>> GetSnapshotsAsync();
    Task<AlarmPermissionState> GetAlarmPermissionStateAsync();
    Task ResolveAsync(AppPermissionKind kind);
}
