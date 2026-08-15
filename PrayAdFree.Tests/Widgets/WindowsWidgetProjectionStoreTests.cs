using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.Tests.Widgets;

public sealed class WindowsWidgetProjectionStoreTests : IDisposable {
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"pray-widget-store-{Guid.NewGuid():N}");

    [Fact]
    public void WritesAndReplacesInstanceAtomicallyWithoutInventingAnotherSize() {
        var path = Path.Combine(_directory, "windows_widget_projections.json");
        var store = new WindowsWidgetProjectionStore(path);
        var instance = new WindowsWidgetInstanceProjection {
            InstanceId = "widget-1",
            ProfileId = "profile-1",
            ProfileRevision = 4,
            RenderTrees = new Dictionary<WidgetFamily, WidgetRenderTree> {
                [WidgetFamily.Small] = new() { ProfileId = "profile-1", ProfileRevision = 4, Family = WidgetFamily.Small, Texts = [new("name", "Fajr", "title", true, "Fajr")] }
            }
        };

        var saved = store.Put(instance);

        Assert.Equal(1, saved.Revision);
        Assert.Equal("Fajr", store.Resolve("widget-1", WidgetFamily.Small).Texts.Single().Text);
        Assert.Equal("error", store.Resolve("widget-1", WidgetFamily.Large).Status);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void CorruptBundleIsReportedAndNeverSilentlyReplaced() {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "windows_widget_projections.json");
        File.WriteAllText(path, "{broken");
        var store = new WindowsWidgetProjectionStore(path);

        Assert.Throws<InvalidDataException>(() => store.Load());
        Assert.Equal("{broken", File.ReadAllText(path));
    }

    public void Dispose() {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
