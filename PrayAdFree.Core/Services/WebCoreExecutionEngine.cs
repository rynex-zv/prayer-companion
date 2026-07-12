using System.Text.Json;
using PrayAdFree.Core.Contracts;
using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public sealed record WebExecutionState(WebState State, AppRevision Revision);
public sealed record WebExecutionResult(object? Data, IReadOnlyList<AppEvent> Events, string State);

/// <summary>Deterministic browser Core boundary: persisted state plus an operation produces data, events, and replacement state.</summary>
public static class WebCoreExecutionEngine {
    public static WebExecutionResult Execute(string? stateJson, string method, JsonElement payload) {
        var persisted = Restore(stateJson);
        var dispatcher = new WebCoreRpcDispatcher(persisted.State, persisted.Revision);
        var data = dispatcher.Dispatch(method, payload);
        var events = dispatcher.DrainEvents();
        var replacement = new WebExecutionState(dispatcher.CaptureState(), dispatcher.CaptureRevision());
        return new WebExecutionResult(data, events, JsonSerializer.Serialize(replacement, CoreJsonContext.Default.WebExecutionState));
    }

    private static WebExecutionState Restore(string? stateJson) {
        if (string.IsNullOrWhiteSpace(stateJson)) return Default();
        try {
            var envelope = JsonSerializer.Deserialize(stateJson, CoreJsonContext.Default.WebExecutionState);
            if (envelope?.State is not null && envelope.Revision is not null) {
                envelope.State.EnsureDefaults();
                return envelope;
            }
        } catch (JsonException) {
        }

        try {
            var legacy = JsonSerializer.Deserialize(stateJson, CoreJsonContext.Default.WebState) ?? WebState.Default();
            legacy.EnsureDefaults();
            return new WebExecutionState(legacy, new AppRevision(0, new Dictionary<string, long>(), 0));
        } catch (JsonException) {
            return Default();
        }
    }

    private static WebExecutionState Default() =>
        new(WebState.Default(), new AppRevision(0, new Dictionary<string, long>(), 0));
}
