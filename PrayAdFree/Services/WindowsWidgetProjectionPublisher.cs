using System.Diagnostics;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace Pray_Ad_Free.Services;

public interface IWindowsWidgetProjectionPublisher {
    Task RefreshAsync(string reason, CancellationToken cancellationToken = default);
}

public sealed class WindowsWidgetProjectionPublisher : IWindowsWidgetProjectionPublisher {
    private readonly WidgetProfileService _profiles;
    private readonly SettingsService _settings;
    private readonly ITasbihProjectionSource _tasbih;
    private readonly IAppLogger _logger;
    private readonly WidgetProjectionFactory _projectionFactory = new();
    private readonly WidgetLayoutResolver _layoutResolver = new();
    private readonly WebPrayerMonthFactory _prayerFactory = new();
    private readonly WindowsWidgetProjectionStore _store;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public WindowsWidgetProjectionPublisher(
        WidgetProfileService profiles,
        SettingsService settings,
        ITasbihProjectionSource tasbih,
        IAppLogger logger) {
        _profiles = profiles;
        _settings = settings;
        _tasbih = tasbih;
        _logger = logger;
        _store = new WindowsWidgetProjectionStore(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrayAdFree",
            "windows_widget_projections.json"));
    }

    public async Task RefreshAsync(string reason, CancellationToken cancellationToken = default) {
        if (!OperatingSystem.IsWindows()) return;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var started = Stopwatch.GetTimestamp();
            var document = _profiles.RefreshFromStorage();
            var assignments = document.Assignments
                .Where(item => item.Platform == WidgetPlatform.WindowsSystem)
                .ToArray();
            if (assignments.Length == 0) {
                _logger.LogEvent("WindowsWidgetProjection", $"reason={reason};instances=0");
                return;
            }

            var settings = _settings.Load();
            var language = string.Equals(settings.Language, "ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "en";
            var now = DateTime.Now;
            WidgetProjection projection;
            try {
                var today = _prayerFactory.BuildDay(settings, DateOnly.FromDateTime(now));
                var tomorrow = _prayerFactory.BuildDay(settings, DateOnly.FromDateTime(now.AddDays(1)));
                var selected = _tasbih.SelectedPreset;
                var selectedItem = selected?.Items.FirstOrDefault();
                projection = _projectionFactory.Build(
                    today,
                    tomorrow,
                    settings,
                    now,
                    language,
                    settings.Location.Source,
                    selected?.Name,
                    selectedItem?.Text,
                    _tasbih.Count,
                    selectedItem?.TargetCount ?? 0);
            } catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) {
                projection = _projectionFactory.Error(exception.Message, language);
            }

            var projectionMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            foreach (var assignment in assignments) {
                cancellationToken.ThrowIfCancellationRequested();
                var profile = document.Profiles.FirstOrDefault(item => item.Id == assignment.ProfileId);
                if (profile is null) {
                    _logger.LogEvent("WindowsWidgetProjection", $"reason={reason};instance={assignment.InstanceId};error=profile-missing");
                    continue;
                }

                var renderStarted = Stopwatch.GetTimestamp();
                var families = new[] { WidgetFamily.Small, WidgetFamily.Medium, WidgetFamily.Large }
                    .Append(assignment.Family)
                    .Distinct();
                var trees = families.ToDictionary(
                    family => family,
                    family => _layoutResolver.Resolve(profile, projection, Capabilities(family)));
                _store.Put(new WindowsWidgetInstanceProjection {
                    InstanceId = assignment.InstanceId,
                    ProfileId = profile.Id,
                    ProfileRevision = profile.Revision,
                    UpdatedAtUnixMilliseconds = projection.GeneratedAtUnixMilliseconds,
                    RenderTrees = trees
                });
                _logger.LogEvent("WindowsWidgetProjection",
                    $"reason={reason};instance={assignment.InstanceId};profile={profile.Id};family={assignment.Family};revision={profile.Revision};projectionMs={projectionMs:F2};renderMs={Stopwatch.GetElapsedTime(renderStarted).TotalMilliseconds:F2}");
            }
        } catch (Exception exception) {
            _logger.LogException(exception, $"WindowsWidgetProjectionPublisher.Refresh:{reason}");
        } finally {
            _gate.Release();
        }
    }

    private static WidgetHostCapabilities Capabilities(WidgetFamily family) => new() {
        Platform = WidgetPlatform.WindowsSystem,
        Surface = WidgetSurface.Board,
        Family = family,
        WidthDp = family == WidgetFamily.Small ? 160 : family == WidgetFamily.Medium ? 320 : 480,
        HeightDp = family == WidgetFamily.Large ? 320 : 160,
        MaxTextItems = family == WidgetFamily.Small ? 4 : family == WidgetFamily.Medium ? 7 : 12,
        MaxActions = family == WidgetFamily.Small ? 1 : 2,
        SupportsBackgroundColor = false,
        SupportsBackgroundOpacity = false,
        SupportsLiveCountdown = false,
        IsAuthenticated = true
    };
}
