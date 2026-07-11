using System.Text.Json;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests.Web;

public sealed class WebAlarmRpcTests {
    [Fact]
    public void ContractExportsAlarmMethods() {
        Assert.Contains("alarm.getSnapshot", WebContractExporter.RpcMethods);
        Assert.Contains("alarm.snooze", WebContractExporter.RpcMethods);
        Assert.Contains("alarm.stop", WebContractExporter.RpcMethods);
    }

    [Theory]
    [InlineData("alarm.getSnapshot")]
    [InlineData("alarm.snooze")]
    [InlineData("alarm.stop")]
    public void StandaloneWebReturnsInactiveAlarmSnapshot(string method) {
        using var payload = JsonDocument.Parse("{}");
        var result = new WebCoreRpcDispatcher().Dispatch(method, payload.RootElement);
        var snapshot = JsonSerializer.SerializeToElement(result);

        Assert.False(snapshot.GetProperty("isActive").GetBoolean());
        Assert.False(snapshot.GetProperty("canSnooze").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(
            snapshot.GetProperty("labels").GetProperty("noActiveAlarm").GetString()));
    }
}
