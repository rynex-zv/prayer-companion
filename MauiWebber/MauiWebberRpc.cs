using System.Text.Json;

namespace MauiWebber;

public sealed class MauiWebberRpcRequest {
    public string Id { get; init; } = "";
    public string Method { get; init; } = "";
    public JsonElement Payload { get; init; }
}

public interface IMauiWebberRpcHandler {
    Task<object?> HandleAsync(string method, JsonElement payload, CancellationToken cancellationToken);
}

internal sealed class MauiWebberRpcResponse {
    public bool Ok { get; init; }
    public object? Data { get; init; }
    public string? Error { get; init; }
}
