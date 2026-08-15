using System.Diagnostics;
using System.Text.Json;
using PrayAdFree.Core.Contracts;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests.Web;

public sealed class WidgetRpcTests {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    [Fact]
    public void CrudResponsesContainConfirmedDocumentWithoutFollowUpRead() {
        var dispatcher = new WebCoreRpcDispatcher();
        var created = Element(dispatcher.Dispatch("widgets.createProfile", JsonSerializer.SerializeToElement(new {
            template = "NextPrayer", name = "RPC profile"
        })));
        var profile = created.GetProperty("profile");
        Assert.Equal("RPC profile", profile.GetProperty("name").GetString());
        Assert.True(created.GetProperty("document").GetProperty("profiles").GetArrayLength() >= 7);

        var updated = Element(dispatcher.Dispatch("widgets.updateProfile", JsonSerializer.SerializeToElement(new {
            id = profile.GetProperty("id").GetString(),
            patch = new { expectedRevision = profile.GetProperty("revision").GetInt64(), name = "Confirmed" }
        })));
        Assert.Equal("Confirmed", updated.GetProperty("profile").GetProperty("name").GetString());
        Assert.True(updated.TryGetProperty("document", out _));
    }

    [Fact]
    public void PreviewComesFromSharedCoreRenderTreeAndStaysBelowCeiling() {
        var dispatcher = new WebCoreRpcDispatcher();
        var profiles = Element(dispatcher.Dispatch("widgets.getProfiles", JsonSerializer.SerializeToElement(new { })));
        var id = profiles.GetProperty("profiles")[0].GetProperty("id").GetString();
        var watch = Stopwatch.StartNew();
        var preview = Element(dispatcher.Dispatch("widgets.getPreview", JsonSerializer.SerializeToElement(new {
            profileId = id,
            capabilities = new {
                platform = "Preview", surface = "Preview", family = "Medium",
                widthDp = 300, heightDp = 180, maxTextItems = 8, maxActions = 2,
                supportsBackgroundColor = true, supportsBackgroundOpacity = true,
                supportsFullColor = true, supportsLiveCountdown = true, isAuthenticated = true
            }
        })));
        watch.Stop();

        Assert.True(preview.TryGetProperty("projection", out _));
        Assert.True(preview.TryGetProperty("renderTree", out var tree));
        Assert.True(tree.TryGetProperty("texts", out _));
        Assert.True(watch.ElapsedMilliseconds < 300, $"preview took {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void ContractClassifiesWidgetReadsAndWritesCorrectly() {
        Assert.Equal(RpcOperationKind.Query, WebContractExporter.Classify("widgets.getPreview"));
        Assert.Equal(RpcOperationKind.Command, WebContractExporter.Classify("widgets.updateProfile"));
        Assert.Contains("widgets.assignProfile", WebContractExporter.RpcMethods);
    }

    private static JsonElement Element(object? value) => JsonSerializer.SerializeToElement(value, JsonOptions);
}
