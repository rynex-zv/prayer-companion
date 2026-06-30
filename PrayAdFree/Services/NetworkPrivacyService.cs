#if ANDROID
using Android.Content;
using Android.Net;
#endif

namespace Pray_Ad_Free.Services;

public sealed class NetworkPrivacyService : INetworkPrivacyService {
    public bool IsVpnActive() {
#if ANDROID
        try {
            if (!OperatingSystem.IsAndroidVersionAtLeast(23)) {
                return false;
            }

            var context = global::Android.App.Application.Context;
            var manager = context?.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
            var network = manager?.ActiveNetwork;
            var capabilities = network == null ? null : manager?.GetNetworkCapabilities(network);
            return capabilities?.HasTransport(TransportType.Vpn) == true;
        } catch {
            return false;
        }
#else
        return false;
#endif
    }
}
