using System.Text.Json.Serialization;

namespace MauiWebber;

public sealed class MauiWebberManifest {
    [JsonPropertyName("contractVersion")]
    public int ContractVersion { get; init; }

    [JsonPropertyName("version")]
    public string Version { get; init; } = "";

    [JsonPropertyName("entry")]
    public string Entry { get; init; } = "index.html";

    [JsonPropertyName("files")]
    public List<MauiWebberManifestFile> Files { get; init; } = [];
}

public sealed class MauiWebberManifestFile {
    [JsonPropertyName("path")]
    public string Path { get; init; } = "";

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; init; }
}
