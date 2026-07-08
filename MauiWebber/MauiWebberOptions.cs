namespace MauiWebber;

public enum MauiWebberUpdatePolicy {
    LocalFirst
}

public enum MauiWebberIntegrityMode {
    OptionalHash
}

public sealed class MauiWebberOptions {
    public string AppId { get; init; } = "app";
    public string EmbeddedRoot { get; init; } = "web";
    public Uri RemoteBaseUrl { get; init; } = new("https://example.com/");
    public Uri ManifestUrl { get; init; } = new("https://example.com/webber-manifest.json");
    public string StorageFolderName { get; init; } = "MauiWebber";
    public string StartupFile { get; init; } = "index.html";
    public MauiWebberUpdatePolicy UpdatePolicy { get; init; } = MauiWebberUpdatePolicy.LocalFirst;
    public bool RollbackEnabled { get; init; } = true;
    public MauiWebberIntegrityMode IntegrityMode { get; init; } = MauiWebberIntegrityMode.OptionalHash;
    public bool AppendJsLog { get; init; } = true;
    public TimeSpan UpdateGateWaitTimeout { get; init; } = TimeSpan.FromSeconds(3);
    public TimeSpan UpdateCheckTimeout { get; init; } = TimeSpan.FromSeconds(30);
}
