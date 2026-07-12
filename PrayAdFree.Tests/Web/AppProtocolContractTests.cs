using System.Text.Json;
using PrayAdFree.Core.Contracts;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests.Web;

public sealed class AppProtocolContractTests {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Command_envelope_matches_shared_fixture() {
        using var payload = JsonDocument.Parse("{\"theme\":\"dark\"}");
        var value = new AppCommandEnvelope(2, "request-1", "command-1", "app.setTheme", 41, payload.RootElement.Clone());
        Assert.Equal(
            "{\"contractVersion\":2,\"requestId\":\"request-1\",\"commandId\":\"command-1\",\"name\":\"app.setTheme\",\"expectedRevision\":41,\"payload\":{\"theme\":\"dark\"}}",
            JsonSerializer.Serialize(value, JsonOptions));
    }

    [Fact]
    public void Query_and_typed_error_round_trip() {
        using var payload = JsonDocument.Parse("{\"sections\":[\"location\"]}");
        var query = new AppQueryEnvelope(2, "request-2", "settings.snapshot", 42, payload.RootElement.Clone());
        var copy = JsonSerializer.Deserialize<AppQueryEnvelope>(JsonSerializer.Serialize(query, JsonOptions), JsonOptions);
        Assert.NotNull(copy);
        Assert.Equal(query.ContractVersion, copy.ContractVersion);
        Assert.Equal(query.RequestId, copy.RequestId);
        Assert.Equal(query.Name, copy.Name);
        Assert.Equal(query.IfRevision, copy.IfRevision);
        Assert.Equal(query.Payload.GetRawText(), copy.Payload.GetRawText());

        var error = new AppError("revision_conflict", "The expected revision is stale.", true);
        var errorCopy = JsonSerializer.Deserialize<AppError>(JsonSerializer.Serialize(error, JsonOptions), JsonOptions);
        Assert.Equal(error, errorCopy);
    }

    [Fact]
    public void Every_legacy_rpc_has_an_explicit_classification() {
        Assert.Equal(WebContractExporter.RpcMethods.Count, WebContractExporter.RpcContracts.Count);
        Assert.All(WebContractExporter.RpcContracts, item => Assert.False(string.IsNullOrWhiteSpace(item.Domain)));
        Assert.DoesNotContain("app.navigate", WebContractExporter.RpcMethods);
        Assert.DoesNotContain("app.importState", WebContractExporter.RpcMethods);
        Assert.DoesNotContain("app.exportState", WebContractExporter.RpcMethods);
        Assert.DoesNotContain("settings.invoke", WebContractExporter.RpcMethods);
        Assert.DoesNotContain("settings.setField", WebContractExporter.RpcMethods);
        Assert.DoesNotContain("settings.patch", WebContractExporter.RpcMethods);
        Assert.Contains("permissions.request", WebContractExporter.RpcMethods);
        Assert.Contains("location.refresh", WebContractExporter.RpcMethods);
        Assert.Contains("tasbih.updateItem", WebContractExporter.RpcMethods);
    }

    [Fact]
    public void Protocol_versions_are_stable() {
        Assert.Equal(2, AppProtocol.ContractVersion);
        Assert.True(AppProtocol.PersistenceSchemaVersion >= 1);
    }

    [Fact]
    public void Browser_bootstrap_contains_grouped_startup_projections() {
        var dispatcher = new WebCoreRpcDispatcher();
        using var payload = JsonDocument.Parse("{}");
        var json = JsonSerializer.SerializeToElement(dispatcher.Dispatch("app.bootstrap", payload.RootElement), JsonOptions);

        Assert.Equal(AppProtocol.ContractVersion, json.GetProperty("contractVersion").GetInt32());
        Assert.True(json.GetProperty("projections").TryGetProperty("shell", out _));
        Assert.True(json.GetProperty("projections").TryGetProperty("today", out _));
        Assert.True(json.GetProperty("projections").TryGetProperty("alarm", out _));
        Assert.True(json.GetProperty("projections").TryGetProperty("onboarding", out _));
        Assert.True(json.GetProperty("projections").TryGetProperty("permissions", out _));
        Assert.True(json.GetProperty("projections").TryGetProperty("capabilities", out _));
    }

    [Fact]
    public void Browser_events_are_sequenced_and_queries_are_revision_aware() {
        var dispatcher = new WebCoreRpcDispatcher();
        using var commandPayload = JsonDocument.Parse("{\"theme\":\"dark\",\"_rpc\":{\"requestId\":\"request-7\"}}");
        dispatcher.Dispatch("app.setTheme", commandPayload.RootElement);
        var events = dispatcher.DrainEvents();
        var appEvent = Assert.Single(events);
        Assert.Equal(1, appEvent.Sequence);
        Assert.Equal("request-7", appEvent.CauseRequestId);
        Assert.Equal("app", appEvent.Domain);

        using var queryPayload = JsonDocument.Parse($"{{\"_query\":{{\"ifRevision\":{appEvent.Revision}}}}}");
        var result = JsonSerializer.SerializeToElement(dispatcher.Dispatch("app.getShellSnapshot", queryPayload.RootElement), JsonOptions);
        Assert.True(result.GetProperty("notModified").GetBoolean());
        Assert.Equal(appEvent.Revision, result.GetProperty("revision").GetInt64());
    }
}
