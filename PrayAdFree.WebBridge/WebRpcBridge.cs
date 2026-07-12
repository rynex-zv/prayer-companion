using System.Runtime.InteropServices.JavaScript;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using System.Text.Json;
using PrayAdFree.Core.Services;

namespace PrayAdFree.WebBridge;

public static partial class WebRpcBridge {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    [JSExport]
    [SupportedOSPlatform("browser")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The WASM connector serializes Core RPC envelopes after preserving PrayAdFree.Core as a trimmer root.")]
    public static string Call(string method, string payloadJson) {
        return CallWithState("", method, payloadJson);
    }

    [JSExport]
    [SupportedOSPlatform("browser")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The deterministic WASM connector serializes its explicit execution envelope.")]
    public static string CallWithState(string stateJson, string method, string payloadJson) {
        try {
            using var payloadDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson);
            var result = WebCoreExecutionEngine.Execute(stateJson, method, payloadDocument.RootElement);
            return JsonSerializer.Serialize(new { ok = true, data = result.Data, events = result.Events, state = result.State }, JsonOptions);
        } catch (Exception ex) {
            return JsonSerializer.Serialize(new { ok = false, error = WebRpcErrorFormatter.Clean(ex.Message) }, JsonOptions);
        }
    }
}
