namespace Pray_Ad_Free.Services;

public interface IWindowsBackgroundModeService {
    bool IsSupported { get; }
    bool IsEnabled();
    bool SetEnabled(bool enabled);
}

