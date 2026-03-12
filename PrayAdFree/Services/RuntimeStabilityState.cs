namespace Pray_Ad_Free.Services;

public static class RuntimeStabilityState {
    private static volatile bool _windowsSafeStartupMode;

    public static bool IsWindowsSafeStartupMode => _windowsSafeStartupMode;

    public static void SetWindowsSafeStartupMode(bool enabled) {
        _windowsSafeStartupMode = enabled;
    }
}
