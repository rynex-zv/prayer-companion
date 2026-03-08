namespace Pray_Ad_Free.Services;

public interface IAdhanPlaybackService {
    void Initialize();
    Task<bool> PlayPreviewAsync(string? soundKey);
    Task StopAsync();
}
