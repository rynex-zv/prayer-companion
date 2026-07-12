using System.Diagnostics;
using System.Text.Json;
using MauiWebber;
using PrayAdFree.Core.Contracts;
using PrayAdFree.Core.Services;

namespace Pray_Ad_Free.Services;

/// <summary>Native RPC transport adapter. Application workflows live in <see cref="NativeAppBackend"/>.</summary>
public sealed record NativeAppOperation(
    string Method,
    string RequestId,
    string? CommandId,
    string Domain,
    RpcOperationKind Kind,
    long IfRevision,
    long? ExpectedRevision);

public sealed class WebAppRpcHandler(NativeAppBackend backend, IAppLogger logger) : IMauiWebberRpcHandler {
    public async Task<object?> HandleAsync(string method, JsonElement payload, CancellationToken cancellationToken) {
        var requestId = ReadMetadataString(payload, "requestId") ?? Guid.NewGuid().ToString("D");
        var commandId = ReadMetadataString(payload, "commandId");
        var operation = new NativeAppOperation(
            method,
            requestId,
            commandId,
            ReadMetadataString(payload, "domain") ?? method.Split('.')[0],
            WebContractExporter.Classify(method),
            ReadIfRevision(payload),
            ReadExpectedRevision(payload));
        var stopwatch = Stopwatch.StartNew();
        using var metricsScope = RpcObservability.Begin();
        try {
            var result = await backend.HandleAsync(operation, payload, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            var metrics = RpcObservability.Capture();
            logger.LogEvent("rpc.completed", JsonSerializer.Serialize(new {
                requestId,
                commandId,
                method,
                kind = operation.Kind.ToString(),
                durationMs = stopwatch.ElapsedMilliseconds,
                responseBytes = JsonSerializer.SerializeToUtf8Bytes(result).Length,
                metrics.PersistenceWrites,
                cache = new { hits = metrics.CacheHits, misses = metrics.CacheMisses }
            }));
            return result;
        } catch (Exception exception) {
            stopwatch.Stop();
            logger.LogEvent("rpc.failed", JsonSerializer.Serialize(new {
                requestId,
                commandId,
                method,
                durationMs = stopwatch.ElapsedMilliseconds,
                errorType = exception.GetType().Name
            }));
            throw;
        }
    }

    public Task PreloadAsync() => backend.PreloadAsync();

    private static string? ReadMetadataString(JsonElement payload, string name) {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("_rpc", out var metadata) ||
            metadata.ValueKind != JsonValueKind.Object ||
            !metadata.TryGetProperty(name, out var value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static long ReadIfRevision(JsonElement payload) {
        if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("_query", out var query) &&
            query.ValueKind == JsonValueKind.Object && query.TryGetProperty("ifRevision", out var value) && value.TryGetInt64(out var revision)) return revision;
        return 0;
    }

    private static long? ReadExpectedRevision(JsonElement payload) {
        if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("_rpc", out var metadata) &&
            metadata.ValueKind == JsonValueKind.Object && metadata.TryGetProperty("expectedRevision", out var value) &&
            value.TryGetInt64(out var revision)) return revision;
        return null;
    }
}
