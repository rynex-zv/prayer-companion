using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Windows.Widgets;
using Microsoft.Windows.Widgets.Providers;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.WidgetProvider;

internal sealed class PrayerWidgetProvider : IWidgetProvider {
    private static readonly ConcurrentDictionary<string, string> Instances = new(StringComparer.Ordinal);
    internal static ManualResetEvent ExitEvent { get; } = new(false);
    private static readonly string SharedRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PrayAdFree");
    private readonly WindowsWidgetProjectionStore _store = new(Path.Combine(SharedRoot, "windows_widget_projections.json"));
    private readonly WidgetProfileService _profiles = new(new JsonFileWidgetProfileRepository(Path.Combine(SharedRoot, "widget_profiles.json")));
    private readonly WindowsAdaptiveCardWidgetRenderer _renderer = new();

    public PrayerWidgetProvider() {
        try {
            foreach (var info in WidgetManager.GetDefault().GetWidgetInfos()) {
                Instances[info.WidgetContext.Id] = info.WidgetContext.DefinitionId;
            }
        } catch (Exception exception) {
            Log("hydrate", "", "", exception, 0);
        }
    }

    public void CreateWidget(WidgetContext widgetContext) {
        Instances[widgetContext.Id] = widgetContext.DefinitionId;
        EnsureAssignment(widgetContext);
        Update(widgetContext);
    }

    public void DeleteWidget(string widgetId, string customState) {
        Instances.TryRemove(widgetId, out _);
        _store.Remove(widgetId);
        _profiles.RefreshFromStorage();
        _profiles.Unassign(widgetId);
        if (Instances.IsEmpty) ExitEvent.Set();
    }

    public void Activate(WidgetContext widgetContext) => Update(widgetContext);

    public void Deactivate(string widgetId) {
    }

    public void OnWidgetContextChanged(WidgetContextChangedArgs contextChangedArgs) {
        EnsureAssignment(contextChangedArgs.WidgetContext);
        Update(contextChangedArgs.WidgetContext);
    }

    public void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs) {
        Log("unsupported-action", actionInvokedArgs.WidgetContext.Id, actionInvokedArgs.Verb, null, 0);
    }

    private void Update(WidgetContext context) {
        var started = Stopwatch.GetTimestamp();
        var family = context.Size switch {
            WidgetSize.Small => WidgetFamily.Small,
            WidgetSize.Large => WidgetFamily.Large,
            _ => WidgetFamily.Medium
        };
        try {
            var tree = _store.Resolve(context.Id, family);
            var options = new WidgetUpdateRequestOptions(context.Id) {
                Template = _renderer.Render(tree),
                Data = "{}",
                CustomState = $"{tree.ProfileId}:{tree.ProfileRevision}:{family}"
            };
            WidgetManager.GetDefault().UpdateWidget(options);
            Log("render", context.Id, tree.ProfileId, null, Stopwatch.GetElapsedTime(started).TotalMilliseconds, family, tree.ProfileRevision);
        } catch (Exception exception) {
            Log("render", context.Id, "", exception, Stopwatch.GetElapsedTime(started).TotalMilliseconds, family);
        }
    }

    private void EnsureAssignment(WidgetContext context) {
        try {
            var document = _profiles.RefreshFromStorage();
            var existing = document.Assignments.FirstOrDefault(item => item.InstanceId == context.Id);
            var family = ToFamily(context.Size);
            var profileId = existing?.ProfileId ?? "default-next-prayer";
            _profiles.Assign(new WidgetInstanceAssignment {
                InstanceId = context.Id,
                ProfileId = profileId,
                Platform = WidgetPlatform.WindowsSystem,
                Surface = WidgetSurface.Board,
                Family = family,
                MinWidthDp = family == WidgetFamily.Small ? 160 : family == WidgetFamily.Medium ? 320 : 480,
                MaxWidthDp = family == WidgetFamily.Small ? 160 : family == WidgetFamily.Medium ? 320 : 480,
                MinHeightDp = family == WidgetFamily.Large ? 320 : 160,
                MaxHeightDp = family == WidgetFamily.Large ? 320 : 160
            });
        } catch (Exception exception) {
            Log("assignment", context.Id, "", exception, 0, ToFamily(context.Size));
        }
    }

    private static WidgetFamily ToFamily(WidgetSize size) => size switch {
        WidgetSize.Small => WidgetFamily.Small,
        WidgetSize.Large => WidgetFamily.Large,
        _ => WidgetFamily.Medium
    };

    private static void Log(string operation, string instanceId, string profileId, Exception? exception, double elapsedMs, WidgetFamily family = WidgetFamily.Medium, long revision = 0) {
        try {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrayAdFree", "logs");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "windows-widgets.log"),
                $"{DateTimeOffset.UtcNow:O}\tplatform=windows11\toperation={operation}\tinstance={instanceId}\tprofile={profileId}\tfamily={family}\trevision={revision}\trenderMs={elapsedMs:F2}\terror={exception?.GetType().Name}:{exception?.Message}{Environment.NewLine}");
        } catch {
        }
    }
}
