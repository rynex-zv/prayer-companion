using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrayAdFree.Core.Contracts;

public static class AppProtocol {
    public const int ContractVersion = 2;
    public const int PersistenceSchemaVersion = 1;
}

[JsonConverter(typeof(JsonStringEnumConverter<RpcOperationKind>))]
public enum RpcOperationKind { Command, Query, PlatformOperation, CompatibilityAdapter, Obsolete }

public sealed record AppCommandEnvelope(
    int ContractVersion,
    string RequestId,
    string CommandId,
    string Name,
    long? ExpectedRevision,
    JsonElement Payload);

public sealed record AppQueryEnvelope(
    int ContractVersion,
    string RequestId,
    string Name,
    long? IfRevision,
    JsonElement Payload);

public sealed record AppError(string Code, string Message, bool Retryable = false, object? Details = null);
public sealed record AppRevision(long Global, IReadOnlyDictionary<string, long> Domains, long EventSequence);
public sealed record AppEvent(long Sequence, string EventId, DateTimeOffset Timestamp, string Domain, string Type, long Revision, string? CauseRequestId, object? Payload = null, string? InvalidationKey = null);
public sealed record AppCommandResult(bool Ok, string RequestId, long Revision, IReadOnlyList<string> ChangedDomains, object? Data, IReadOnlyList<AppEvent> Events, AppError? Error = null);
public sealed record AppQueryResult(bool Ok, string RequestId, long Revision, object? Data, bool NotModified = false, AppError? Error = null);
public sealed record RpcContract(string Name, RpcOperationKind Kind, string Domain);

