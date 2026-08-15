using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using MauiWebber;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
using Microsoft.Maui.Devices.Sensors;
#if ANDROID
using Pray_Ad_Free.Platforms.Android;
#endif

namespace Pray_Ad_Free.Services;

public sealed class NativeAppBackend {
    private readonly TodayWebRpcHandler _today;
    private readonly ICalendarProjectionSource _calendar;
    private readonly IQiblaProjectionSource _qibla;
    private readonly ITasbihProjectionSource _tasbih;
    private readonly ISettingsRepository _settingsService;
    private readonly PrayerDataService _dataService;
    private readonly IAppPermissionCenterService _permissionCenter;
    private readonly IGeoLookupService _geoLookupService;
    private readonly IIpLocationService _ipLocationService;
    private readonly IAdhanPlaybackService _adhanPlaybackService;
    private readonly INotificationBootstrapper _notificationBootstrapper;
    private readonly AndroidAlarmCapabilityService _alarmCapability;
    private readonly MauiWebberUpdater _webUpdater;
    private readonly IAppLogger _logger;
    private readonly AppRevisionCoordinator _revisions = new();
    private readonly ApplicationCoordinator _application;
    private readonly ApplicationOperationCoalescer _operations;
    private readonly WidgetProfileService _widgets;
    private readonly IWindowsWidgetProjectionPublisher _windowsWidgetPublisher;
    private readonly WidgetProjectionFactory _widgetProjectionFactory = new();
    private readonly WidgetLayoutResolver _widgetLayoutResolver = new();
    private readonly WebPrayerMonthFactory _widgetPrayerFactory = new();
    private readonly IslamicOccasionCatalog _islamicOccasions = new();
    private readonly object _bootstrapSync = new();
    private Task<object>? _bootstrapTask;
    private DateTime _calendarMonth = DateTime.Today;
    private bool _qiblaLoaded;
    private string _qiblaDisplayMode = "compass";
    private string _qiblaVisualFilter = "none";
    private bool _qiblaCompassSubscribed;

    public NativeAppBackend(
        TodayWebRpcHandler today,
        ICalendarProjectionSource calendar,
        IQiblaProjectionSource qibla,
        ITasbihProjectionSource tasbih,
        ISettingsRepository settingsService,
        PrayerDataService dataService,
        IAppPermissionCenterService permissionCenter,
        IGeoLookupService geoLookupService,
        IIpLocationService ipLocationService,
        IAdhanPlaybackService adhanPlaybackService,
        INotificationBootstrapper notificationBootstrapper,
        AndroidAlarmCapabilityService alarmCapability,
        MauiWebberUpdater webUpdater,
        IApplicationTransactionFactory transactionFactory,
        ApplicationOperationCoalescer operations,
        WidgetProfileService widgets,
        IWindowsWidgetProjectionPublisher windowsWidgetPublisher,
        IAppLogger logger) {
        _today = today;
        _calendar = calendar;
        _qibla = qibla;
        _tasbih = tasbih;
        _settingsService = settingsService;
        _dataService = dataService;
        _permissionCenter = permissionCenter;
        _geoLookupService = geoLookupService;
        _ipLocationService = ipLocationService;
        _adhanPlaybackService = adhanPlaybackService;
        _notificationBootstrapper = notificationBootstrapper;
        _alarmCapability = alarmCapability;
        _webUpdater = webUpdater;
        _logger = logger;
        _operations = operations;
        _widgets = widgets;
        _windowsWidgetPublisher = windowsWidgetPublisher;
        _application = new ApplicationCoordinator(
            transactionFactory,
            _revisions,
            (appEvent, _) => {
                MauiWebberEventHub.Publish(appEvent);
                return Task.CompletedTask;
            });
        _calendarMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        App.AppResumed += OnAppResumed;
    }

    private void OnAppResumed(object? sender, EventArgs args) {
        MauiWebberEventHub.Publish(_revisions.Changed("app", null, "backend.resumed", invalidationKey: "app.*"));
        MauiWebberEventHub.Publish(_revisions.Changed("today", null, "domain.invalidated", invalidationKey: "today.snapshot"));
    }

    public Task PreloadAsync() {
        var bootstrap = GetBootstrapTask();
        _ = WarmPlatformSnapshotsAsync();
        return bootstrap;
    }

    private async Task WarmPlatformSnapshotsAsync() {
        try {
            await _permissionCenter.GetSnapshotsAsync().ConfigureAwait(false);
        } catch (Exception exception) {
            _logger.LogException(exception, "NativeAppBackend.PlatformWarmup");
        }
    }

    private Task<object> GetBootstrapTask() {
        lock (_bootstrapSync) {
            return _bootstrapTask ??= BuildBootstrapAsync(CancellationToken.None);
        }
    }

    public async Task<object?> HandleAsync(NativeAppOperation operation, JsonElement payload, CancellationToken cancellationToken) {
            var method = operation.Method;
            var currentRevision = _revisions.Snapshot();
            if (method != "app.bootstrap" && operation.Kind == PrayAdFree.Core.Contracts.RpcOperationKind.Query && operation.IfRevision > 0 &&
                currentRevision.Domains.TryGetValue(operation.Domain, out var domainRevision) && domainRevision == operation.IfRevision) {
                return new { notModified = true, revision = domainRevision };
            }
            var execute = new Func<Task<object?>>(async () => method switch {
            "app.bootstrap" => await GetBootstrapTask().ConfigureAwait(false),
            "today.getSnapshot" => await _today.HandleAsync(method, payload, cancellationToken).ConfigureAwait(false),
            "today.refresh" => await AcknowledgeTodayRefreshAsync(operation.RequestId, cancellationToken).ConfigureAwait(false),
            "mauiWebber.trace" => new { ok = true },
            "app.getShellSnapshot" => BuildShellSnapshot(),
            "app.getLocalization" => BuildLabels(),
            "app.getLanguageObject" => BuildLanguageObject(ReadString(payload, "language")),
            "app.setLanguage" => SetLanguage(payload),
            "app.setTheme" => SetTheme(payload),
            "calendar.getSnapshot" => await GetCalendarAsync(payload).ConfigureAwait(false),
            "calendar.setMonth" => await SetCalendarMonthAsync(payload).ConfigureAwait(false),
            "calendar.today" => await MoveCalendarAsync(0, today: true).ConfigureAwait(false),
            "calendar.nextMonth" => await MoveCalendarAsync(1).ConfigureAwait(false),
            "calendar.previousMonth" => await MoveCalendarAsync(-1).ConfigureAwait(false),
            "qibla.getSnapshot" => await GetQiblaAsync().ConfigureAwait(false),
            "qibla.startSensor" => await StartQiblaSensorAsync().ConfigureAwait(false),
            "qibla.stopSensor" => await StopQiblaSensorAsync().ConfigureAwait(false),
            "qibla.setHeadingMode" => await SetQiblaHeadingModeAsync(payload).ConfigureAwait(false),
            "qibla.updateHeading" => await UpdateQiblaHeadingAsync(payload).ConfigureAwait(false),
            "qibla.adjustManualHeading" => AdjustQiblaManualHeading(payload),
            "qibla.commitManualHeading" => await CommitQiblaManualHeadingAsync().ConfigureAwait(false),
            "qibla.setDisplayMode" => await SetQiblaDisplayModeAsync(payload).ConfigureAwait(false),
            "qibla.setVisualFilter" => await SetQiblaVisualFilterAsync(payload).ConfigureAwait(false),
            "tasbih.getSnapshot" => BuildTasbihSnapshot(),
            "tasbih.increment" => RunTasbihCommand(_tasbih.Increment),
            "tasbih.reset" => RunTasbihCommand(_tasbih.Reset),
            "tasbih.selectPreset" => SelectTasbihPreset(payload),
            "tasbih.addPreset" => PatchTasbihAndSnapshot("addTasbihPreset", payload),
            "tasbih.updatePreset" => PatchTasbihAndSnapshot("updateTasbihPreset", payload),
            "tasbih.removePreset" => PatchTasbihAndSnapshot("removeTasbihPreset", payload),
            "tasbih.addItem" => PatchTasbihAndSnapshot("addTasbihItem", payload),
            "tasbih.updateItem" => PatchTasbihAndSnapshot("updateTasbihItem", payload),
            "tasbih.moveItem" => PatchTasbihAndSnapshot("moveTasbihItem", payload),
            "tasbih.removeItem" => PatchTasbihAndSnapshot("removeTasbihItem", payload),
            "alarm.getSnapshot" => await GetAlarmSnapshotAsync().ConfigureAwait(false),
            "alarm.snooze" => await SnoozeAlarmAsync(payload).ConfigureAwait(false),
            "alarm.stop" => await StopAlarmAsync().ConfigureAwait(false),
            "alarm.test" => QueuePlatformOperation(method, "alarm", payload, () => TestAdhanAlarmAsync(payload)),
            "notification.test" => QueuePlatformOperation(method, "notification", payload, () => TestAdhanNotificationAsync(payload)),
            "permissions.request" => QueuePlatformOperation(method, "permissions", payload, () => RequestPermissionAsync(payload)),
            "permissions.requestAll" => QueuePlatformOperation(method, "permissions", payload, RequestAllPermissionsAsync),
            "location.refresh" => QueuePlatformOperation(method, "location", payload, () => RefreshLocationAsync(payload)),
            "location.reverseGeocode" => QueuePlatformOperation(method, "location", payload, () => ReverseGeocodeLocationAsync(payload)),
            "adhan.sound.preview" => QueuePlatformOperation(method, "adhan", payload, () => PreviewAdhanSoundAsync(payload)),
            "adhan.sound.stopPreview" => QueuePlatformOperation(method, "adhan", payload, StopAdhanSoundPreviewAsync),
            "adhan.sound.addCustom" => QueuePlatformOperation(method, "adhan", payload, ImportCustomAdhanSoundAsync),
            "adhan.sound.removeCustom" => await RemoveCustomAdhanSoundAsync(payload).ConfigureAwait(false),
            "external.openEmail" => QueuePlatformOperation(method, "external", payload, () => OpenEmailAsync(payload)),
            "external.call" => QueuePlatformOperation(method, "external", payload, () => OpenPhoneAsync(payload)),
            "external.openUrl" => QueuePlatformOperation(method, "external", payload, () => OpenUrlAsync(payload)),
            "external.reportIssue" => QueuePlatformOperation(method, "external", payload, OpenIssueReportAsync),
            "settings.getSnapshot" => await GetSettingsSnapshotAsync(payload).ConfigureAwait(false),
            "settings.update" => await SetSettingsFieldAsync(payload).ConfigureAwait(false),
            "widgets.getCatalog" => WidgetProfileService.Catalog,
            "widgets.getProfiles" => _widgets.Snapshot(),
            "widgets.createProfile" => CreateWidgetProfile(payload),
            "widgets.updateProfile" => UpdateWidgetProfile(payload),
            "widgets.duplicateProfile" => DuplicateWidgetProfile(payload),
            "widgets.deleteProfile" => DeleteWidgetProfile(payload),
            "widgets.getPreview" => BuildWidgetPreview(payload),
            "widgets.getInstalledInstances" => _widgets.Snapshot().Assignments,
            "widgets.assignProfile" => AssignWidgetProfile(payload),
            "onboarding.getSnapshot" => await BuildOnboardingSnapshotAsync().ConfigureAwait(false),
            "onboarding.complete" => CompleteOnboarding(),
            "automation.writeReports" => WriteAutomationReports(payload),
                _ => throw new InvalidOperationException($"Unknown MauiWebber RPC method: {method}")
            });
            if (operation.Kind is PrayAdFree.Core.Contracts.RpcOperationKind.Command or PrayAdFree.Core.Contracts.RpcOperationKind.CompatibilityAdapter) {
                var coordinated = await _application.CommandAsync(
                    new ApplicationCommandRequest(
                        operation.RequestId,
                        operation.CommandId ?? operation.RequestId,
                        method,
                        operation.Domain,
                        operation.ExpectedRevision),
                    _ => execute(),
                    cancellationToken).ConfigureAwait(false);
                return coordinated.Data;
            } else {
                return await _operations.RunAsync(
                    BuildOperationKey(method, payload),
                    currentRevision.Global,
                    _ => execute(),
                    cancellationToken).ConfigureAwait(false);
            }
    }

    private async Task<object> BuildBootstrapAsync(CancellationToken cancellationToken) {
        // Bootstrap only cheap projections needed before React can safely route.
        // Alarm is included because Android alarm launches can arrive before the
        // WebView navigation bridge is ready; the first React render must know
        // whether /alarm is the authoritative startup route.
#if ANDROID
        if (_adhanPlaybackService is AdhanPlaybackService androidPlayback &&
            AndroidAlarmLaunchCoordinator.TryGetPendingPayload(out var pendingAlarm)) {
            await androidPlayback.HandleAndroidAlarmLaunchAsync(
                pendingAlarm,
                source: "Bootstrap",
                presentationMode: AlarmPresentationMode.FullscreenActivity,
                showAlarmScreen: false).ConfigureAwait(false);
        }
#endif
        var today = await _today.HandleAsync("today.getSnapshot", default, cancellationToken).ConfigureAwait(false);
        var alarm = await GetAlarmSnapshotStateAsync().ConfigureAwait(false);
        return new {
            contractVersion = PrayAdFree.Core.Contracts.AppProtocol.ContractVersion,
            persistenceSchemaVersion = PrayAdFree.Core.Contracts.AppProtocol.PersistenceSchemaVersion,
            revisions = _revisions.Snapshot(),
            startup = new {
                route = alarm.IsActive ? "/alarm" : "/",
                intent = alarm.IsActive ? "alarm" : (string?)null
            },
            projections = new {
                shell = BuildBootstrapShellSnapshot(),
                today,
                alarm = alarm.Snapshot,
                capabilities = new { platform = DeviceInfo.Platform.ToString().ToLowerInvariant(), native = true, events = true }
            }
        };
    }

    private object BuildBootstrapShellSnapshot() {
        var settings = _settingsService.Load();
        var language = ResolveLanguage(settings.Language);
        return new {
            route = "/",
            language,
            isRtl = string.Equals(language, "ar", StringComparison.OrdinalIgnoreCase),
            themeMode = ResolveTheme(settings.ThemeMode),
            accentColor = AccentFromIndex(settings.AccentIndex),
            textSize = settings.TextScale == 0 ? 100 : settings.TextScale,
            languages = WebCatalog.Languages.Select(item => new {
                code = item.Code,
                name = item.Name,
                direction = item.Direction
            }).ToList(),
            onboardingCompleted = settings.OnboardingCompleted
        };
    }

    private async Task<object?> AcknowledgeTodayRefreshAsync(string requestId, CancellationToken cancellationToken) {
        var current = await _today.HandleAsync("today.getSnapshot", default, cancellationToken).ConfigureAwait(false);
        _ = Task.Run(async () => {
            try {
                var refreshed = await _today.HandleAsync("today.refresh", default, CancellationToken.None).ConfigureAwait(false);
                MauiWebberEventHub.Publish(_revisions.Changed(
                    "today",
                    requestId,
                    "projection.updated",
                    payload: new { projectionKey = "today.snapshot", data = refreshed }));
            } catch (Exception exception) {
                _logger.LogException(exception, "NativeAppBackend.TodayRefresh");
            }
        });
        return current;
    }

    private static string BuildOperationKey(string method, JsonElement payload) {
        if (payload.ValueKind != JsonValueKind.Object) return method;
        var body = JsonNode.Parse(payload.GetRawText()) as JsonObject;
        body?.Remove("_rpc");
        body?.Remove("_query");
        return body is null || body.Count == 0 ? method : $"{method}|{body.ToJsonString()}";
    }

    private object BuildShellSnapshot() {
        var settings = _settingsService.Load();
        var language = ResolveLanguage(settings.Language);
        return new {
            route = "/",
            language,
            isRtl = IsRtl(),
            themeMode = ResolveTheme(settings.ThemeMode),
            accentColor = AccentFromIndex(settings.AccentIndex),
            textSize = settings.TextScale == 0 ? 100 : settings.TextScale,
            languageObject = BuildLanguageObject(language),
            languages = WebCatalog.Languages.Select(item => new {
                code = item.Code,
                name = item.Name,
                direction = item.Direction
            }).ToList(),
            tabs = WebCatalog.LocalizedShellTabs(language),
            onboardingCompleted = settings.OnboardingCompleted
        };
    }

    private async Task<(object Snapshot, bool IsActive)> GetAlarmSnapshotStateAsync() {
        var settings = _settingsService.Load();
        var language = ResolveLanguage(settings.Language);
        var model = await _adhanPlaybackService.GetActiveAlarmPresentationModelAsync().ConfigureAwait(false);
        if (model == null) {
            return (WebAlarmSnapshotFactory.Inactive(language), false);
        }

        return (WebAlarmSnapshotFactory.Active(
                language,
                model.PrayerClock,
                model.DelayFromBase,
                model.PrayerName,
                model.ReminderText,
                model.CanSnooze,
                model.MinDelayMinutes,
                model.MaxDelayMinutes,
                model.InitialDelayMinutes), true);
    }

    private async Task<object> GetAlarmSnapshotAsync() {
        var alarm = await GetAlarmSnapshotStateAsync().ConfigureAwait(false);
        return alarm.Snapshot;
    }

    private async Task<object> SnoozeAlarmAsync(JsonElement payload) {
        var scheduled = await _adhanPlaybackService
            .SnoozeActiveAlarmAsync(ReadInt(payload, "minutes", 0))
            .ConfigureAwait(false);
        if (scheduled) {
            await CloseAlarmHostAsync().ConfigureAwait(false);
        }

        return await GetAlarmSnapshotAsync().ConfigureAwait(false);
    }

    private async Task<object> StopAlarmAsync() {
        await _adhanPlaybackService.StopAsync().ConfigureAwait(false);
        await CloseAlarmHostAsync().ConfigureAwait(false);
        return await GetAlarmSnapshotAsync().ConfigureAwait(false);
    }

    private static async Task CloseAlarmHostAsync() {
        var shell = Shell.Current;
        var sectionStack = shell?.CurrentItem?.CurrentItem?.Navigation?.NavigationStack;
        var webPage = sectionStack?.LastOrDefault() as MauiWebberPage
            ?? shell?.Navigation?.NavigationStack.LastOrDefault() as MauiWebberPage
            ?? shell?.CurrentPage as MauiWebberPage
            ?? Application.Current?.Windows.FirstOrDefault()?.Page as MauiWebberPage;
        if (webPage != null) await webPage.NavigateToRouteAsync("/", TimeSpan.FromSeconds(3)).ConfigureAwait(false);
    }

    private static object BuildLanguageObject(string? language) {
        var requested = ResolveLanguage(language ?? LocalizationManager.CurrentLanguage);
        LocalizationManager.SetLanguage(requested);
        return new {
            code = requested,
            direction = IsRtl() ? "rtl" : "ltr",
            labels = BuildLabels(),
            updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    private static IReadOnlyDictionary<string, string> BuildLabels() {
        return WebCatalog.Labels(ResolveLanguage(LocalizationManager.CurrentLanguage));
    }

    private object SetLanguage(JsonElement payload) {
        var language = ReadString(payload, "language");
        if (!string.IsNullOrWhiteSpace(language)) {
            LocalizationManager.SetLanguage(language);
            var settings = _settingsService.Load();
            SaveSettings(CopySettings(
                settings,
                language: language,
                languageSelected: true));
        }

        return BuildShellSnapshot();
    }

    private object SetTheme(JsonElement payload) {
        var theme = ReadString(payload, "theme") ?? ReadString(payload, "themeMode");
        var settings = _settingsService.Load();
        var next = CopySettings(settings, themeMode: ParseThemeMode(theme, settings.ThemeMode));
        SaveSettings(next);
        ThemeManager.ApplyTheme(next);
        return BuildShellSnapshot();
    }

    private async Task<object> GetCalendarAsync(JsonElement payload) {
        var requestedMonth = ReadString(payload, "month");
        if (!string.IsNullOrWhiteSpace(requestedMonth) &&
            DateTime.TryParse($"{requestedMonth}-01", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) {
            _calendarMonth = new DateTime(parsed.Year, parsed.Month, 1);
        }

        await LoadCalendarMonthAsync().ConfigureAwait(false);
        return BuildCalendarSnapshot();
    }

    private Task<object> SetCalendarMonthAsync(JsonElement payload) {
        return GetCalendarAsync(payload);
    }

    private async Task<object> MoveCalendarAsync(int offset, bool today = false) {
        _calendarMonth = today
            ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
            : _calendarMonth.AddMonths(offset);
        await LoadCalendarMonthAsync().ConfigureAwait(false);
        return BuildCalendarSnapshot();
    }

    private async Task LoadCalendarMonthAsync() {
        var changed = _calendar.SelectedMonth.Year != _calendarMonth.Year ||
                      _calendar.SelectedMonth.Month != _calendarMonth.Month;
        if (changed) {
            _calendar.SelectedMonth = _calendarMonth;
        } else {
            await _calendar.LoadAsync().ConfigureAwait(false);
        }

        while (_calendar.IsBusy) {
            await Task.Delay(20).ConfigureAwait(false);
        }

        // Do not return a new header paired with stale days from the previous month.
        if (_calendar.Days.Count == 0 ||
            _calendar.Days[0].SourceDate.Year != _calendarMonth.Year ||
            _calendar.Days[0].SourceDate.Month != _calendarMonth.Month) {
            await _calendar.LoadAsync().ConfigureAwait(false);
            while (_calendar.IsBusy) await Task.Delay(20).ConfigureAwait(false);
        }
    }

    private object BuildCalendarSnapshot() {
        var settings = _dataService.LoadSettings();
        var occasions = _islamicOccasions.ForMadhhab(settings.Madhhab)
            .GroupBy(item => (item.HijriMonth, item.HijriDay))
            .ToDictionary(group => group.Key, group => group.First());
        var enrichedDays = _calendar.Days.Select(day => {
            var hijri = ParseHijri(day.Hijri);
            occasions.TryGetValue((hijri.Month, hijri.Day), out var occasion);
            return new {
                sourceDate = day.SourceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                weekday = (int)day.SourceDate.ToDateTime(TimeOnly.MinValue).DayOfWeek,
                dayNumber = day.SourceDate.Day,
                date = day.Date,
                hijri = LocalizeHijriDate(day.Hijri),
                hijriDay = hijri.Day,
                hijriMonth = hijri.Month,
                hijriMonthName = LocalizeHijriMonth(hijri.MonthName),
                hijriYear = hijri.Year,
                fajr = day.Fajr,
                sunrise = day.Sunrise,
                dhuhr = day.Dhuhr,
                asr = day.Asr,
                maghrib = day.Maghrib,
                isha = day.Isha,
                isToday = IsToday(day),
                occasionKey = occasion?.LabelKey,
                occasionColor = occasion?.Color,
                occasionImportance = occasion?.Importance
            };
        }).ToList();
        var firstHijri = enrichedDays.FirstOrDefault();
        var lastHijri = enrichedDays.LastOrDefault();
        var hijriMonthLabel = firstHijri is null ? "" :
            firstHijri.hijriMonth == lastHijri!.hijriMonth
                ? $"{firstHijri.hijriMonthName} {firstHijri.hijriYear}"
                : $"{firstHijri.hijriMonthName} – {lastHijri.hijriMonthName} {lastHijri.hijriYear}";
        return new {
            selectedMonth = _calendarMonth.ToString("MMMM yyyy", CultureInfo.CurrentUICulture),
            selectedMonthValue = _calendarMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            monthName = _calendarMonth.ToString("MMMM", CultureInfo.CurrentUICulture),
            yearNumber = _calendarMonth.Year,
            monthNumber = _calendarMonth.Month,
            hijriMonthLabel,
            statusMessage = _calendar.StatusMessage,
            days = enrichedDays
        };
    }

    private static (int Day, int Month, string MonthName, int Year) ParseHijri(string value) {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return (0, 0, "", 0);
        _ = int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var day);
        _ = int.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var year);
        var monthName = string.Join(" ", parts[1..^1]);
        return (day, HijriMonthNumber(monthName), monthName, year);
    }

    private static int HijriMonthNumber(string name) {
        var normalized = name.ToLowerInvariant().Replace("ḥ", "h").Replace("ḍ", "d").Replace("ṭ", "t").Replace("ʿ", "").Replace("ā", "a").Replace("ī", "i").Replace("ū", "u");
        if (normalized.Contains("muharram")) return 1;
        if (normalized.Contains("safar")) return 2;
        if (normalized.Contains("rabi") && (normalized.Contains("awwal") || normalized.Contains("first"))) return 3;
        if (normalized.Contains("rabi")) return 4;
        if (normalized.Contains("jumad") && (normalized.Contains("ula") || normalized.Contains("awwal") || normalized.Contains("first"))) return 5;
        if (normalized.Contains("jumad")) return 6;
        if (normalized.Contains("rajab")) return 7;
        if (normalized.Contains("shaban")) return 8;
        if (normalized.Contains("ramadan")) return 9;
        if (normalized.Contains("shawwal")) return 10;
        if (normalized.Contains("qadah") || normalized.Contains("qida")) return 11;
        if (normalized.Contains("hijjah") || normalized.Contains("hijja")) return 12;
        return 0;
    }

    private static string LocalizeHijriMonth(string value) {
        return LocalizeHijriDate($"1 {value} 1").Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).SkipLast(1).Aggregate("", (a, b) => string.IsNullOrEmpty(a) ? b : $"{a} {b}");
    }

    private static bool IsToday(CalendarDayRow day) {
        return day.SourceDate == DateOnly.FromDateTime(DateTime.Today);
    }

    private static string LocalizeHijriDate(string value) {
        if (!string.Equals(LocalizationManager.CurrentLanguage, "ar", StringComparison.OrdinalIgnoreCase)) {
            return value;
        }

        return value
            .Replace("Muḥarram", "محرم", StringComparison.OrdinalIgnoreCase)
            .Replace("Safar", "صفر", StringComparison.OrdinalIgnoreCase)
            .Replace("Rabīʿ al-awwal", "ربيع الأول", StringComparison.OrdinalIgnoreCase)
            .Replace("Rabi' al-Awwal", "ربيع الأول", StringComparison.OrdinalIgnoreCase)
            .Replace("Rabīʿ al-thānī", "ربيع الآخر", StringComparison.OrdinalIgnoreCase)
            .Replace("Jumādá al-ūlá", "جمادى الأولى", StringComparison.OrdinalIgnoreCase)
            .Replace("Jumādá al-ākhirah", "جمادى الآخرة", StringComparison.OrdinalIgnoreCase)
            .Replace("Rajab", "رجب", StringComparison.OrdinalIgnoreCase)
            .Replace("Shaʿbān", "شعبان", StringComparison.OrdinalIgnoreCase)
            .Replace("Ramaḍān", "رمضان", StringComparison.OrdinalIgnoreCase)
            .Replace("Shawwāl", "شوال", StringComparison.OrdinalIgnoreCase)
            .Replace("Dhū al-Qaʿdah", "ذو القعدة", StringComparison.OrdinalIgnoreCase)
            .Replace("Dhū al-Ḥijjah", "ذو الحجة", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<object> GetQiblaAsync() {
        if (!_qiblaLoaded) {
            await _qibla.LoadAsync().ConfigureAwait(false);
            _qiblaLoaded = true;
        }

        var state = string.Equals(_qiblaDisplayMode, "map", StringComparison.OrdinalIgnoreCase)
            ? "map"
            : _qibla.IsManualHeadingMode ? "manual" : "sensor";
        var isAligned = Math.Abs(NormalizeAngle(_qibla.NeedleRotation)) <= 5;
        if (isAligned && !string.Equals(state, "map", StringComparison.OrdinalIgnoreCase)) {
            state = "aligned";
        }

        return new {
            bearing = _qibla.Bearing,
            heading = _qibla.Heading,
            latitude = _qibla.Location?.Latitude ?? 0,
            longitude = _qibla.Location?.Longitude ?? 0,
            needleRotation = _qibla.NeedleRotation,
            compassRotation = _qibla.CompassRotation,
            directionLabel = _qibla.DirectionLabel,
            locationTitle = _qibla.LocationTitle,
            statusMessage = _qibla.StatusMessage,
            selectedHeadingMode = _qibla.IsManualHeadingMode ? "manual" : "auto",
            selectedReadingMode = _qiblaDisplayMode,
            selectedFilterMode = _qiblaVisualFilter,
            displayMode = string.Equals(_qiblaDisplayMode, "map", StringComparison.OrdinalIgnoreCase) ? "Map" : "Compass",
            visualFilter = _qiblaVisualFilter switch {
                "night" => "Night",
                "contrast" => "Contrast",
                _ => "None"
            },
            state,
            isAligned,
            headingModes = new[] {
                new { id = "auto", label = T("QiblaHeadingMode_Auto") },
                new { id = "manual", label = T("QiblaHeadingMode_Manual") }
            },
            readingModes = new[] {
                new { id = "compass", label = T("QiblaModeCompass") },
                new { id = "map", label = T("QiblaModeMap") }
            },
            filterModes = new[] {
                new { id = "none", label = T("QiblaVisualFilter_None") },
                new { id = "night", label = T("QiblaVisualFilter_Night") },
                new { id = "contrast", label = T("QiblaVisualFilter_Contrast") }
            },
            labels = BuildLabels()
        };
    }

    private async Task<object> SetQiblaHeadingModeAsync(JsonElement payload) {
        var mode = ReadString(payload, "mode");
        var option = _qibla.HeadingModes.FirstOrDefault(item =>
            string.Equals(mode, "manual", StringComparison.OrdinalIgnoreCase)
                ? item.Value == QiblaHeadingMode.Manual
                : item.Value == QiblaHeadingMode.Sensor);
        if (option != null) {
            _qibla.SelectedHeadingMode = option;
        }

        return await GetQiblaAsync().ConfigureAwait(false);
    }

    private async Task<object> UpdateQiblaHeadingAsync(JsonElement payload) {
        var heading = ReadDouble(payload, "heading");
        _qibla.UpdateHeading(heading);
        return await GetQiblaAsync().ConfigureAwait(false);
    }

    private object AdjustQiblaManualHeading(JsonElement payload) {
        var delta = ReadDouble(payload, "delta");
        _qibla.AdjustManualHeading(delta);
        return GetQiblaAsync().GetAwaiter().GetResult();
    }

    private async Task<object> CommitQiblaManualHeadingAsync() {
        _qibla.CommitManualHeading();
        return await GetQiblaAsync().ConfigureAwait(false);
    }

    private async Task<object> SetQiblaDisplayModeAsync(JsonElement payload) {
        _qiblaDisplayMode = AppInputContract.RequiredChoice(
            ReadString(payload, "mode"), "qibla.displayMode", "compass", "map");
        return await GetQiblaAsync().ConfigureAwait(false);
    }

    private async Task<object> StartQiblaSensorAsync() {
        if (!Compass.IsSupported) {
            throw new InvalidOperationException("A compass sensor is not available on this device.");
        }

        await MainThread.InvokeOnMainThreadAsync(() => {
            if (!_qiblaCompassSubscribed) {
                Compass.ReadingChanged += OnQiblaCompassReadingChanged;
                _qiblaCompassSubscribed = true;
            }
            if (!Compass.IsMonitoring) {
                Compass.Start(SensorSpeed.UI, applyLowPassFilter: true);
            }
        });
        return await GetQiblaAsync().ConfigureAwait(false);
    }

    private async Task<object> StopQiblaSensorAsync() {
        await MainThread.InvokeOnMainThreadAsync(() => {
            if (_qiblaCompassSubscribed) {
                Compass.ReadingChanged -= OnQiblaCompassReadingChanged;
                _qiblaCompassSubscribed = false;
            }
            if (Compass.IsMonitoring) {
                Compass.Stop();
            }
        });
        return new { stopped = true };
    }

    private void OnQiblaCompassReadingChanged(object? sender, CompassChangedEventArgs args) {
        var heading = args.Reading.HeadingMagneticNorth;
#if ANDROID
        if (_qibla.Location is { } location) {
            var magneticField = new Android.Hardware.GeomagneticField(
                (float)location.Latitude,
                (float)location.Longitude,
                0,
                Java.Lang.JavaSystem.CurrentTimeMillis());
            heading += magneticField.Declination;
        }
#endif
        _qibla.UpdateHeading(heading);
    }

    private async Task<object> SetQiblaVisualFilterAsync(JsonElement payload) {
        _qiblaVisualFilter = AppInputContract.RequiredChoice(
            ReadString(payload, "mode"), "qibla.visualFilter", "none", "night", "contrast");
        return await GetQiblaAsync().ConfigureAwait(false);
    }

    private object RunTasbihCommand(Action command) {
        command();
        return BuildTasbihSnapshot();
    }

    private object SelectTasbihPreset(JsonElement payload) {
        var id = ReadString(payload, "id");
        var presetCount = _settingsService.Load().Tasbih.Presets.Count;
        var index = AppInputContract.RequiredIndex(id, presetCount, "tasbih.presetId");
        _tasbih.SelectPreset(index);

        return BuildTasbihSnapshot();
    }

    private object BuildTasbihSnapshot() {
        var settings = _settingsService.Load();
        var presets = settings.Tasbih.Presets;
        var selectedIndex = Math.Clamp(
            settings.Tasbih.SelectedPresetIndex,
            0,
            Math.Max(0, presets.Count - 1));
        var selectedPreset = presets.Count > 0 ? presets[selectedIndex] : null;
        var selectedItem = selectedPreset?.Items.FirstOrDefault();
        var count = _tasbih.Count;

        return new {
            count,
            currentPhrase = LocalizeTasbihText(selectedItem?.Text ?? _tasbih.CurrentPhrase),
            progressText = selectedItem is null
                ? _tasbih.ProgressText
                : $"{count} / {selectedItem.TargetCount}",
            isPresetSelectionEnabled = presets.Count > 1,
            selectedPresetId = selectedIndex.ToString(CultureInfo.InvariantCulture),
            presets = presets.Select((preset, index) => new {
                id = index.ToString(CultureInfo.InvariantCulture),
                name = LocalizeTasbihText(preset.Name),
                repeatMode = ToWebTasbihRepeatMode(preset.RepeatMode),
                items = preset.Items.Select(item => new {
                    text = LocalizeTasbihText(item.Text),
                    targetCount = item.TargetCount
                }).ToList()
            }).ToList()
        };
    }

    private static string LocalizeTasbihText(string value) =>
        value.StartsWith("Tasbih_", StringComparison.Ordinal) || value.StartsWith("TasbihPreset_", StringComparison.Ordinal)
            ? LocalizationManager.Translate(value)
            : value;

    private async Task<object?> GetSettingsSnapshotAsync(JsonElement payload) {
        var section = ReadString(payload, "section");
        var settings = _settingsService.Load();
        return section switch {
            "locations" => BuildLocationsSettings(settings),
            "theme" => BuildThemeSettings(settings),
            "adhan" => BuildAdhanSettings(settings),
            "notifications" => BuildNotificationSettings(settings),
            "permissions" => await BuildPermissionsSettingsAsync().ConfigureAwait(false),
            "alarmReminders" => BuildAlarmReminderSettings(settings),
            "about" => BuildAboutSettings(),
            null or "" => new {
                locations = BuildLocationsSettings(settings),
                theme = BuildThemeSettings(settings),
                adhan = BuildAdhanSettings(settings),
                notifications = BuildNotificationSettings(settings),
                permissions = await BuildPermissionsSettingsAsync().ConfigureAwait(false),
                alarmReminders = BuildAlarmReminderSettings(settings)
            },
            _ => throw new ArgumentException($"Unknown settings section '{section}'.", nameof(section))
        };
    }

    private object BuildAboutSettings() => new {
        name = WebCatalog.AboutInfo.Name,
        tagline = T("tagline"),
        privacy = T("privacy"),
        source = T("source"),
        maintainer = WebCatalog.AboutInfo.Maintainer,
        contact = T("contact"),
        email = WebCatalog.AboutInfo.Email,
        phone = WebCatalog.AboutInfo.Phone,
        website = WebCatalog.AboutInfo.Website,
        websiteNote = T("websiteNote"),
        report = T("report"),
        remoteWebUrl = _webUpdater.RemoteBaseUrl.AbsoluteUri,
        defaultRemoteWebUrl = WebCatalog.AboutInfo.RemoteWebUrl
    };

    private async Task<object?> PatchSettingsAsync(JsonElement payload) {
        var settings = _settingsService.Load();
        var next = settings;
        string? changedSection = null;

        if (TryGetObject(payload, "locations", out var locations)) {
            changedSection = "locations";
            next = CopySettings(
                next,
                location: PatchLocationConfirmed(next.Location, locations),
                qibla: PatchQibla(next.Qibla, locations));
        }

        if (TryGetObject(payload, "theme", out var theme)) {
            changedSection = "theme";
            var language = ReadString(theme, "language");
            var themeMode = ReadString(theme, "themeMode");
            if (!string.IsNullOrWhiteSpace(language)) {
                LocalizationManager.SetLanguage(language);
            }

            next = CopySettings(
                next,
                language: string.IsNullOrWhiteSpace(language) ? next.Language : language,
                languageSelected: string.IsNullOrWhiteSpace(language) ? next.LanguageSelected : true,
                themeMode: ParseThemeMode(themeMode, next.ThemeMode),
                textScale: ReadInt(theme, "textSize", next.TextScale == 0 ? 100 : next.TextScale),
                accentIndex: AccentToIndex(ReadString(theme, "accentColor"), next.AccentIndex));
        }

        if (TryGetObject(payload, "adhan", out var adhan)) {
            changedSection = "adhan";
            next = PatchAdhan(next, adhan);
        }

        if (TryGetObject(payload, "notifications", out var notifications)) {
            changedSection = "notifications";
            next = CopySettings(next, notifications: PatchNotifications(next.Notifications, notifications));
        }

        if (TryGetObject(payload, "alarmReminders", out var alarmReminders)) {
            changedSection = "alarmReminders";
            next = CopySettings(next, alarmReminders: PatchAlarmReminders(next.AlarmReminders, alarmReminders));
        }

        SaveSettings(next);
        ThemeManager.ApplyTheme(next);
        var snapshot = changedSection == null
            ? await GetSettingsSnapshotAsync(payload).ConfigureAwait(false)
            : await GetSettingsSnapshotAsync(BuildSectionPayload(changedSection)).ConfigureAwait(false);
        if (changedSection is not ("notifications" or "adhan" or "locations")) return snapshot;
        _ = Task.Run(async () => {
            try {
                await _notificationBootstrapper
                    .EnsureScheduledAsync($"WebSettings:{changedSection}", requestPermissions: false, force: true)
                    .ConfigureAwait(false);
            } catch (Exception exception) {
                _logger.LogException(exception, $"NativeAppBackend.Reconcile.{changedSection}");
            }
        });
        return snapshot;
    }

    private async Task<object> SetSettingsFieldAsync(JsonElement payload) {
        var section = ReadString(payload, "section") ?? "";
        var field = ReadString(payload, "field") ?? "";
        if (!payload.TryGetProperty("value", out var value)) {
            return new { ok = false, section, field, error = "Missing value" };
        }

        if (string.Equals(section, "theme", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(field, "value", StringComparison.OrdinalIgnoreCase)) {
            var themeNode = new JsonObject {
                [field] = JsonNode.Parse(value.GetRawText())
            };
            var root = new JsonObject {
                ["theme"] = themeNode
            };
            var patchResult = await PatchSettingsAsync(JsonSerializer.SerializeToElement(root)).ConfigureAwait(false);
            if (string.Equals(field, "language", StringComparison.OrdinalIgnoreCase)) {
                return PreserveAfterCommit(patchResult, new { ok = true, section, field, value = value.GetString(), languageObject = BuildLanguageObject(value.GetString()) });
            }

            return PreserveAfterCommit(patchResult, new { ok = true, section, field, value });
        }

        var patchSection = PatchSectionName(section);
        if (string.IsNullOrWhiteSpace(patchSection)) {
            return new { ok = false, section, field, value, error = $"Unsupported section: {section}" };
        }

        var sectionNode = string.Equals(field, "value", StringComparison.OrdinalIgnoreCase)
            ? JsonNode.Parse(value.GetRawText())
            : new JsonObject {
                [field] = JsonNode.Parse(value.GetRawText())
            };
        var payloadNode = new JsonObject {
            [patchSection] = sectionNode
        };
        var projection = await PatchSettingsAsync(JsonSerializer.SerializeToElement(payloadNode)).ConfigureAwait(false);
        var calculated = string.Equals(section, "locations", StringComparison.OrdinalIgnoreCase) ? projection : null;
        return new { ok = true, section, field, value, calculated, projection };
    }

    private static object PreserveAfterCommit(object? execution, object data) =>
        execution is ApplicationCommandExecution coordinated
            ? new ApplicationCommandExecution(data, coordinated.AfterCommit)
            : data;

    private static string PatchSectionName(string section) {
        return section switch {
            "locations" => "locations",
            "theme" => "theme",
            "adhan" => "adhan",
            "notifications" => "notifications",
            "alarmReminders" => "alarmReminders",
            _ => ""
        };
    }

    private object PatchTasbihAndSnapshot(string action, JsonElement payload) {
        PatchTasbih(action, payload);
        return BuildTasbihSnapshot();
    }

    private async Task<PlatformOperationCompletion> RequestAllPermissionsAsync() {
        if (!AutomationRuntimeEnabled()) await ResolveAllPermissionsAsync().ConfigureAwait(false);
        return new PlatformOperationCompletion(await BuildPermissionsSettingsAsync().ConfigureAwait(false), "settings.permissions");
    }

    private async Task<PlatformOperationCompletion> RequestPermissionAsync(JsonElement payload) {
        ValidatePermission(payload);
        if (!AutomationRuntimeEnabled()) await ResolvePermissionAsync(payload).ConfigureAwait(false);
        return new PlatformOperationCompletion(await BuildPermissionsSettingsAsync().ConfigureAwait(false), "settings.permissions");
    }

    private async Task<PlatformOperationCompletion> PreviewAdhanSoundAsync(JsonElement payload) {
        var id = ReadString(payload, "id") ?? _settingsService.Load().Notifications.SoundKey;
        var started = AutomationRuntimeEnabled() || await _adhanPlaybackService.PlayPreviewAsync(id).ConfigureAwait(false);
        return new PlatformOperationCompletion(new { ok = started, simulated = AutomationRuntimeEnabled(), action = "previewSound", id }, null);
    }

    private async Task<PlatformOperationCompletion> StopAdhanSoundPreviewAsync() {
        if (!AutomationRuntimeEnabled()) await _adhanPlaybackService.StopAsync().ConfigureAwait(false);
        return new PlatformOperationCompletion(new { ok = true, simulated = AutomationRuntimeEnabled(), action = "stopPreview" }, null);
    }

    private async Task<PlatformOperationCompletion> TestAdhanAlarmAsync(JsonElement payload) {
        var id = ReadString(payload, "id") ?? _settingsService.Load().Notifications.SoundKey;
        var scheduled = AutomationRuntimeEnabled() || await _adhanPlaybackService.ScheduleTestAlarmAsync(id, TimeSpan.FromSeconds(12)).ConfigureAwait(false);
        return new PlatformOperationCompletion(new { ok = scheduled, simulated = AutomationRuntimeEnabled(), action = "testAlarm", id }, null);
    }

    private async Task<PlatformOperationCompletion> TestAdhanNotificationAsync(JsonElement payload) {
        var id = ReadString(payload, "id") ?? _settingsService.Load().Notifications.SoundKey;
        var started = AutomationRuntimeEnabled() || await _adhanPlaybackService.PlayPreviewAsync(id).ConfigureAwait(false);
        return new PlatformOperationCompletion(new { ok = started, simulated = AutomationRuntimeEnabled(), action = "testNotification", id }, null);
    }

    private object QueuePlatformOperation(
        string method,
        string domain,
        JsonElement payload,
        Func<Task<PlatformOperationCompletion>> execute) {
        var operationId = ReadString(payload, "operationId") ?? Guid.NewGuid().ToString("N");
        _ = Task.Run(async () => {
            // Give the transport time to deliver the acknowledgement before an
            // interactive picker or external application can block the UI thread.
            await Task.Delay(25).ConfigureAwait(false);
            try {
                _logger.LogEvent("PlatformOperation.Start", $"operation={method};operationId={operationId}");
                var completion = await MainThread.InvokeOnMainThreadAsync(execute).ConfigureAwait(false);
                MauiWebberEventHub.Publish(_revisions.Changed(
                    domain,
                    operationId,
                    "platform.operation.completed",
                    payload: new {
                        operationId,
                        operation = method,
                        ok = true,
                        projectionKey = completion.ProjectionKey,
                        data = completion.Data
                    }));
                _logger.LogEvent("PlatformOperation.Completed", $"operation={method};operationId={operationId}");
            } catch (Exception exception) {
                _logger.LogException(exception, $"NativeAppBackend.{method}");
                MauiWebberEventHub.Publish(_revisions.Changed(
                    domain,
                    operationId,
                    "platform.operation.failed",
                    payload: new {
                        operationId,
                        operation = method,
                        ok = false,
                        error = exception.Message
                    }));
            }
        });
        return new { accepted = true, status = "pending", operationId };
    }

    private async Task<PlatformOperationCompletion> ImportCustomAdhanSoundAsync() {
        if (AutomationRuntimeEnabled()) {
            return new PlatformOperationCompletion(BuildAdhanSettings(_settingsService.Load()), "settings.adhan");
        }

        var pick = await FilePicker.Default.PickAsync(new PickOptions {
            PickerTitle = T("PickAdhanSound"),
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>> {
                { DevicePlatform.Android, new[] { "audio/*" } },
                { DevicePlatform.iOS, new[] { "public.audio" } },
                { DevicePlatform.MacCatalyst, new[] { "public.audio" } },
                { DevicePlatform.WinUI, new[] { ".mp3", ".wav", ".m4a", ".aac", ".ogg", ".flac", ".wma", ".opus", ".amr", ".3gp", ".mp4", ".aiff", ".aif", ".caf" } }
            })
        });
        if (pick == null) {
            return new PlatformOperationCompletion(new { cancelled = true }, null);
        }

        var suggestedName = Path.GetFileNameWithoutExtension(pick.FileName)?.Trim() ?? string.Empty;
        var customName = await PromptForCustomAdhanSoundNameAsync(suggestedName).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(customName)) {
            return new PlatformOperationCompletion(new { cancelled = true }, null);
        }

        var key = $"adhan_custom_{Guid.NewGuid():N}";
        var directory = AdhanSoundLibrary.GetCustomSoundsDirectory();
        Directory.CreateDirectory(directory);
        var header = new byte[64];
        string? targetPath = null;
        try {
            await using var source = await pick.OpenReadAsync();
            var headerLength = await source.ReadAsync(header.AsMemory());
            if (headerLength <= 0) throw new InvalidDataException("Selected audio file is empty.");
            var extension = AudioFileTypeDetector.ResolveExtension(pick.FileName, header.AsSpan(0, headerLength));
            var fileName = $"{key}{extension}";
            targetPath = Path.Combine(directory, fileName);
            await using (var target = File.Create(targetPath)) {
                await target.WriteAsync(header.AsMemory(0, headerLength));
                await source.CopyToAsync(target);
            }

            var settings = _settingsService.Load();
            var sounds = settings.Notifications.CustomSounds.ToList();
            sounds.Add(new CustomAdhanSound {
                Key = key,
                Name = customName.Trim(),
                FileName = fileName
            });
            var notifications = CopyNotifications(settings.Notifications, soundKey: key, customSounds: sounds);
            var updated = CopySettings(settings, notifications: notifications);
            SaveSettings(updated);
            return new PlatformOperationCompletion(BuildAdhanSettings(updated), "settings.adhan");
        } catch {
            if (!string.IsNullOrWhiteSpace(targetPath) && File.Exists(targetPath)) File.Delete(targetPath);
            throw;
        }
    }

    private static async Task<string?> PromptForCustomAdhanSoundNameAsync(string suggestedName) {
        return await MainThread.InvokeOnMainThreadAsync(async () => {
            Page? page = Shell.Current
                ?? Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page == null) {
                throw new InvalidOperationException("The app window is not ready to name the selected sound.");
            }

            return await page.DisplayPromptAsync(
                T("AddCustomAdhanSound"),
                T("CustomAdhanNamePrompt"),
                accept: T("add"),
                cancel: T("Cancel"),
                placeholder: T("CustomAdhanNamePlaceholder"),
                maxLength: 80,
                initialValue: suggestedName);
        }).ConfigureAwait(false);
    }

    private async Task<object> RemoveCustomAdhanSoundAsync(JsonElement payload) {
        var id = ReadString(payload, "id") ?? throw new ArgumentException("Custom sound id is required.", nameof(payload));
        var settings = _settingsService.Load();
        var existing = settings.Notifications.CustomSounds.FirstOrDefault(item =>
            string.Equals(item.Key, id, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Custom sound '{id}' was not found.");
        await _adhanPlaybackService.StopAsync().ConfigureAwait(false);
        var sounds = settings.Notifications.CustomSounds.Where(item =>
            !string.Equals(item.Key, id, StringComparison.OrdinalIgnoreCase)).ToList();
        var overrides = settings.Notifications.PrayerOverrides
            .Select(item => string.Equals(item.SoundKey, id, StringComparison.OrdinalIgnoreCase)
                ? new AdhanPrayerOverride { Prayer = item.Prayer, EnableVibration = item.EnableVibration }
                : item)
            .Where(item => item.SoundKey != null || item.EnableVibration != null)
            .ToList();
        var nextSoundKey = string.Equals(settings.Notifications.SoundKey, id, StringComparison.OrdinalIgnoreCase)
            ? "adhan_default"
            : settings.Notifications.SoundKey;
        var updated = CopySettings(settings, notifications: CopyNotifications(
            settings.Notifications,
            soundKey: nextSoundKey,
            customSounds: sounds,
            prayerOverrides: overrides));
        SaveSettings(updated);
        var path = Path.Combine(AdhanSoundLibrary.GetCustomSoundsDirectory(), existing.FileName);
        if (File.Exists(path)) File.Delete(path);
        return BuildAdhanSettings(updated);
    }

    private static async Task<PlatformOperationCompletion> OpenEmailAsync(JsonElement payload) {
        var to = ReadString(payload, "to")?.Trim();
        if (string.IsNullOrWhiteSpace(to) || !to.Contains('@')) throw new ArgumentException("A valid email address is required.");
        if (!AutomationRuntimeEnabled()) {
            await Email.Default.ComposeAsync(new EmailMessage { To = new List<string> { to } });
        }
        return new PlatformOperationCompletion(new { opened = !AutomationRuntimeEnabled(), simulated = AutomationRuntimeEnabled(), target = to }, null);
    }

    private static Task<PlatformOperationCompletion> OpenPhoneAsync(JsonElement payload) {
        var number = ReadString(payload, "number")?.Trim();
        if (string.IsNullOrWhiteSpace(number)) throw new ArgumentException("A phone number is required.");
        if (!AutomationRuntimeEnabled()) PhoneDialer.Default.Open(number);
        return Task.FromResult(new PlatformOperationCompletion(new { opened = !AutomationRuntimeEnabled(), simulated = AutomationRuntimeEnabled(), target = number }, null));
    }

    private static async Task<PlatformOperationCompletion> OpenUrlAsync(JsonElement payload) {
        var value = ReadString(payload, "url")?.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")) {
            throw new ArgumentException("A valid HTTP(S) URL is required.");
        }
        var opened = AutomationRuntimeEnabled() || await Launcher.Default.OpenAsync(uri);
        if (!opened) throw new InvalidOperationException("Windows did not accept the external URL.");
        return new PlatformOperationCompletion(new { opened = !AutomationRuntimeEnabled(), simulated = AutomationRuntimeEnabled(), target = uri.AbsoluteUri }, null);
    }

    private static async Task<PlatformOperationCompletion> OpenIssueReportAsync() {
        var target = WebCatalog.AboutInfo.Email;
        if (!AutomationRuntimeEnabled()) {
            await Email.Default.ComposeAsync(new EmailMessage {
                Subject = "Pray Ad Free issue report",
                To = new List<string> { target }
            });
        }
        return new PlatformOperationCompletion(new { opened = !AutomationRuntimeEnabled(), simulated = AutomationRuntimeEnabled(), target }, null);
    }

    private static NotificationSettings CopyNotifications(
        NotificationSettings current,
        string? soundKey = null,
        IReadOnlyList<CustomAdhanSound>? customSounds = null,
        IReadOnlyList<AdhanPrayerOverride>? prayerOverrides = null) => new() {
        EnableAdhan = current.EnableAdhan,
        MobilePrimaryAdhanType = current.MobilePrimaryAdhanType,
        EnableVibration = current.EnableVibration,
        HideOnCloseOnWindows = current.HideOnCloseOnWindows,
        RunBackgroundServiceOnWindows = current.RunBackgroundServiceOnWindows,
        MinutesBefore = current.MinutesBefore,
        AdhanVolume = current.AdhanVolume,
        SoundKey = soundKey ?? current.SoundKey,
        CustomSounds = customSounds ?? current.CustomSounds,
        PrayerOverrides = prayerOverrides ?? current.PrayerOverrides,
        VibrationStrength = current.VibrationStrength,
        VibrationPattern = current.VibrationPattern,
        ReminderScope = current.ReminderScope,
        ReminderPrayer = current.ReminderPrayer,
        ReminderItems = current.ReminderItems.ToList(),
        ReminderOffsetsMinutes = current.ReminderOffsetsMinutes.ToList(),
        PendingDeferredReminder = current.PendingDeferredReminder
    };

    private sealed record PlatformOperationCompletion(object Data, string? ProjectionKey);

    private static bool AutomationRuntimeEnabled() => AutomationRuntime.IsEnabled;

    private async Task ResolveAllPermissionsAsync() {
        var snapshots = await _permissionCenter.GetSnapshotsAsync().ConfigureAwait(false);
        foreach (var snapshot in snapshots.Where(item => item.IsSupported && !item.IsGranted)) {
            await _permissionCenter.ResolveAsync(snapshot.Kind).ConfigureAwait(false);
        }
    }

    private async Task ResolvePermissionAsync(JsonElement payload) {
        var kind = ValidatePermission(payload);
        await _permissionCenter.ResolveAsync(kind).ConfigureAwait(false);
    }

    private static AppPermissionKind ValidatePermission(JsonElement payload) {
        var id = ReadString(payload, "id");
        if (string.IsNullOrWhiteSpace(id) || !Enum.TryParse<AppPermissionKind>(id, ignoreCase: true, out var kind)) {
            throw new ArgumentException($"Unknown permission id '{id ?? "<missing>"}'.", nameof(payload));
        }
        return kind;
    }

    private async Task<PlatformOperationCompletion> RefreshLocationAsync(JsonElement payload) {
        var settings = _settingsService.Load();
        var requestedSource = ReadString(payload, "source")?.Trim().ToLowerInvariant() ?? "auto";
        var locationPermissionGranted = await IsLocationPermissionGrantedAsync().ConfigureAwait(false);
        if (requestedSource == "gps" && !locationPermissionGranted) {
            throw new InvalidOperationException(T("webLocationPermissionDenied"));
        }
        var useGps = requestedSource == "gps" || (requestedSource == "auto" && locationPermissionGranted);
        if (AutomationRuntimeEnabled()) {
            return new PlatformOperationCompletion(new { location = BuildLocationsSettings(settings), changed = false }, "settings.locations");
        }
        if (!useGps) return await RefreshIpLocationAsync(settings).ConfigureAwait(false);

        var gpsSettings = CopySettings(
            settings,
            location: new LocationSettings {
                Mode = LocationMode.Gps,
                City = settings.Location.City,
                Country = settings.Location.Country,
                CountryCode = settings.Location.CountryCode,
                Latitude = settings.Location.Latitude,
                Longitude = settings.Location.Longitude,
                TimeZoneId = settings.Location.TimeZoneId,
                LastUpdatedUtc = settings.Location.LastUpdatedUtc,
                Source = "gps"
            });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try {
            var updated = await _dataService.UpdateLocationAsync(gpsSettings, timeout.Token, forceRefresh: true)
                .ConfigureAwait(false);
            if (!HasUsableCoordinates(updated.Location.Latitude, updated.Location.Longitude)) {
                throw new InvalidOperationException(T("webGpsUnavailable"));
            }

            var changed = LocationChanged(settings.Location, updated.Location, "gps");
            if (changed) SaveSettings(updated);
            return new PlatformOperationCompletion(new { location = BuildLocationsSettings(updated), changed }, "settings.locations");
        } catch (OperationCanceledException) {
            throw new InvalidOperationException(T("webGpsTimedOut"));
        }
    }

    private async Task<PlatformOperationCompletion> RefreshIpLocationAsync(AppSettings settings) {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var result = await _ipLocationService.GetCurrentLocationAsync(timeout.Token).ConfigureAwait(false)
            ?? throw new InvalidOperationException(T("webIpLocationUnavailable"));
        var location = new LocationSettings {
            Mode = LocationMode.Manual,
            City = result.City,
            Country = result.Country,
            CountryCode = result.CountryCode,
            Latitude = result.Latitude,
            Longitude = result.Longitude,
            TimeZoneId = string.IsNullOrWhiteSpace(result.TimeZoneId) ? settings.Location.TimeZoneId : result.TimeZoneId,
            LastUpdatedUtc = DateTime.UtcNow,
            Source = "ip"
        };
        var changed = LocationChanged(settings.Location, location, "ip");
        var updated = changed ? CopySettings(settings, location: location) : settings;
        if (changed) SaveSettings(updated);
        return new PlatformOperationCompletion(new { location = BuildLocationsSettings(updated), changed }, "settings.locations");
    }

    private static Task<bool> IsLocationPermissionGrantedAsync() {
        return Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(async () =>
            await Microsoft.Maui.ApplicationModel.Permissions.CheckStatusAsync<Microsoft.Maui.ApplicationModel.Permissions.LocationWhenInUse>()
                == Microsoft.Maui.ApplicationModel.PermissionStatus.Granted);
    }

    private static bool LocationChanged(LocationSettings current, LocationSettings next, string source) {
        var currentSource = string.IsNullOrWhiteSpace(current.Source)
            ? current.Mode == LocationMode.Gps ? "gps" : "manual"
            : current.Source;
        return !string.Equals(currentSource, source, StringComparison.OrdinalIgnoreCase)
            || Math.Abs(current.Latitude - next.Latitude) >= 0.00001
            || Math.Abs(current.Longitude - next.Longitude) >= 0.00001
            || !string.Equals(current.CountryCode, next.CountryCode, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(current.City, next.City, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<PlatformOperationCompletion> ReverseGeocodeLocationAsync(JsonElement payload) {
        var settings = _settingsService.Load();
        var latitude = ReadDouble(payload, "latitude", settings.Location.Latitude);
        var longitude = ReadDouble(payload, "longitude", settings.Location.Longitude);
        if (!HasUsableCoordinates(latitude, longitude)) {
            throw new ArgumentException("Valid latitude and longitude are required.", nameof(payload));
        }

        var reverse = await _geoLookupService.ReverseAsync(latitude, longitude, CancellationToken.None)
            .ConfigureAwait(false);
        var location = new LocationSettings {
            Mode = LocationMode.Manual,
            City = string.IsNullOrWhiteSpace(reverse?.City) ? T("UnknownCity") : reverse.City,
            Country = string.IsNullOrWhiteSpace(reverse?.Country) ? T("UnknownCountry") : reverse.Country,
            CountryCode = reverse?.CountryCode ?? string.Empty,
            Latitude = latitude,
            Longitude = longitude,
            TimeZoneId = settings.Location.TimeZoneId,
            LastUpdatedUtc = DateTime.UtcNow,
            Source = "manual"
        };
        var updated = CopySettings(settings, location: location);
        SaveSettings(updated);
        return new PlatformOperationCompletion(BuildLocationsSettings(updated), "settings.locations");
    }

    private object CompleteOnboarding() {
        var settings = _settingsService.Load();
        SaveSettings(CopySettings(settings, onboardingCompleted: true));
        return BuildShellSnapshot();
    }

    private void SaveSettings(AppSettings settings) {
        _dataService.SaveSettings(settings);
    }

    private LocationSettings PatchLocationConfirmed(LocationSettings current, JsonElement payload) {
        var patched = PatchLocation(current, payload);
        if (patched.Mode == LocationMode.Gps) {
            return patched;
        }

        // The web projection sends the complete confirmed section. Coordinates
        // being present does not mean that the user edited them; treating every
        // country/city selection as a coordinate edit caused a needless remote
        // reverse-geocode and then overwrote the selected place.
        var latitudeChanged = payload.TryGetProperty("latitude", out _) &&
                              Math.Abs(patched.Latitude - current.Latitude) > 0.000001;
        var longitudeChanged = payload.TryGetProperty("longitude", out _) &&
                               Math.Abs(patched.Longitude - current.Longitude) > 0.000001;
        // Coordinate persistence is a local mutation. Reverse geocoding is an
        // explicit external intent issued by the web route after confirmation,
        // so it must never hold this same-device data call open.
        if ((latitudeChanged || longitudeChanged) && HasUsableCoordinates(patched.Latitude, patched.Longitude)) return patched;

        var placeChanged = payload.TryGetProperty("country", out _) ||
                           payload.TryGetProperty("countryName", out _) ||
                           payload.TryGetProperty("city", out _);
        if (placeChanged) {
            var known = FindKnownPlace(patched.CountryCode, patched.Country, patched.City);
            if (known != null) {
                return new LocationSettings {
                    Mode = LocationMode.Manual,
                    City = string.IsNullOrWhiteSpace(known.City) ? patched.City : known.City,
                    Country = string.IsNullOrWhiteSpace(known.Country) ? patched.Country : known.Country,
                    CountryCode = string.IsNullOrWhiteSpace(known.CountryCode) ? patched.CountryCode : known.CountryCode,
                    Latitude = known.Latitude,
                    Longitude = known.Longitude,
                    TimeZoneId = patched.TimeZoneId,
                    LastUpdatedUtc = DateTime.UtcNow,
                    Source = "manual"
                };
            }
        }

        return patched;
    }

    private GeoLocationResult? FindKnownPlace(string? countryCode, string? country, string? city) {
        return _geoLookupService.GetKnownPlaces().FirstOrDefault(item => {
            // A stable ISO code wins over the localized display name
            // (for example NL + "Nederland" versus catalog "Netherlands").
            var countryMatches = !string.IsNullOrWhiteSpace(countryCode)
                ? string.Equals(item.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase)
                : string.IsNullOrWhiteSpace(country) ||
                  string.Equals(item.Country, country, StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(item.CountryCode, country, StringComparison.OrdinalIgnoreCase);
            var cityMatches = string.IsNullOrWhiteSpace(city) ||
                              string.Equals(item.City, city, StringComparison.OrdinalIgnoreCase);
            return countryMatches && cityMatches;
        });
    }

    private static bool HasUsableCoordinates(double latitude, double longitude) {
        return Math.Abs(latitude) <= 90 &&
               Math.Abs(longitude) <= 180 &&
               (Math.Abs(latitude) > 0.000001 || Math.Abs(longitude) > 0.000001);
    }

    private static LocationSettings PatchLocation(LocationSettings current, JsonElement payload) {
        var useGps = ReadBool(payload, "useGps", current.Mode == LocationMode.Gps);
        var countryCode = ReadString(payload, "country") ?? current.CountryCode;
        return new LocationSettings {
            Mode = useGps ? LocationMode.Gps : LocationMode.Manual,
            City = ReadString(payload, "city") ?? current.City,
            Country = ReadString(payload, "countryName") ?? current.Country,
            CountryCode = countryCode,
            Latitude = ReadDouble(payload, "latitude", current.Latitude),
            Longitude = ReadDouble(payload, "longitude", current.Longitude),
            TimeZoneId = current.TimeZoneId,
            LastUpdatedUtc = DateTime.UtcNow,
            Source = useGps ? "gps" : ReadString(payload, "locationSource") ?? "manual"
        };
    }

    private static QiblaPreferences PatchQibla(QiblaPreferences current, JsonElement payload) {
        return new QiblaPreferences {
            ReadingMode = ParseEnum(ReadString(payload, "qiblaReadingMode"), current.ReadingMode),
            FilterMode = ParseEnum(ReadString(payload, "qiblaFilterMode"), current.FilterMode),
            DirectionMode = current.DirectionMode,
            HeadingMode = current.HeadingMode,
            ManualHeading = current.ManualHeading
        };
    }

    private static AppSettings PatchAdhan(AppSettings settings, JsonElement payload) {
        var selectedSound = ReadSelectedSound(payload) ?? settings.Notifications.SoundKey;
        var notifications = new NotificationSettings {
            EnableAdhan = settings.Notifications.EnableAdhan,
            MobilePrimaryAdhanType = settings.Notifications.MobilePrimaryAdhanType,
            EnableVibration = settings.Notifications.EnableVibration,
            HideOnCloseOnWindows = settings.Notifications.HideOnCloseOnWindows,
            RunBackgroundServiceOnWindows = settings.Notifications.RunBackgroundServiceOnWindows,
            MinutesBefore = settings.Notifications.MinutesBefore,
            AdhanVolume = Math.Clamp(ReadInt(payload, "volume", (int)Math.Round(settings.Notifications.AdhanVolume * 100)) / 100.0, 0, 1),
            SoundKey = selectedSound,
            CustomSounds = settings.Notifications.CustomSounds,
            PrayerOverrides = PatchPrayerOverrides(settings.Notifications.PrayerOverrides, payload),
            VibrationStrength = settings.Notifications.VibrationStrength,
            VibrationPattern = settings.Notifications.VibrationPattern,
            ReminderScope = settings.Notifications.ReminderScope,
            ReminderPrayer = settings.Notifications.ReminderPrayer,
            ReminderItems = settings.Notifications.ReminderItems,
            ReminderOffsetsMinutes = settings.Notifications.ReminderOffsetsMinutes,
            PendingDeferredReminder = settings.Notifications.PendingDeferredReminder
        };

        return CopySettings(
            settings,
            method: ParseEnum(ReadString(payload, "calculationMethod"), settings.Method),
            madhhab: ParseEnum(ReadString(payload, "madhhab"), settings.Madhhab),
            highLatitudeRule: ParseEnum(ReadString(payload, "highLatitudeRule"), settings.HighLatitudeRule),
            sunAngles: new SunAngleSettings {
                Fajr = ReadDouble(payload, "fajrAngle", settings.SunAngles.Fajr),
                Isha = ReadDouble(payload, "ishaAngle", settings.SunAngles.Isha)
            },
            offsets: TryGetObject(payload, "offsets", out var offsets) ? PatchOffsets(settings.Offsets, offsets) : settings.Offsets,
            fastingOffsets: TryGetObject(payload, "fasting", out var fasting) ? new FastingOffsets {
                IftarDelayMinutes = ReadInt(fasting, "iftarDelay", settings.FastingOffsets.IftarDelayMinutes),
                ImsakAdvanceMinutes = ReadInt(fasting, "imsakAdvance", settings.FastingOffsets.ImsakAdvanceMinutes)
            } : settings.FastingOffsets,
            fastingReminders: new FastingReminderSettings {
                ImsakRemindersMinutes = ReadReminderMinutes(payload, "imsakReminders", settings.FastingReminders.ImsakRemindersMinutes),
                IftarRemindersMinutes = ReadReminderMinutes(payload, "iftarReminders", settings.FastingReminders.IftarRemindersMinutes)
            },
            notifications: notifications,
            clockFormat: ParseClockFormat(ReadString(payload, "clockFormat"), settings.ClockFormat));
    }

    private static NotificationSettings PatchNotifications(NotificationSettings current, JsonElement payload) {
        var reminderItems = ReadAdhanReminderItems(payload, current.ReminderItems);
        return new NotificationSettings {
            EnableAdhan = ReadBool(payload, "enableAdhan", current.EnableAdhan),
            MobilePrimaryAdhanType = ParseMobilePrimaryAdhanType(ReadString(payload, "mobilePrimaryAdhanType"), current.MobilePrimaryAdhanType),
            EnableVibration = ReadBool(payload, "vibration", current.EnableVibration),
            HideOnCloseOnWindows = ReadBool(payload, "hideOnCloseWindows", current.HideOnCloseOnWindows),
            RunBackgroundServiceOnWindows = ReadBool(payload, "runBackgroundServiceWindows", current.RunBackgroundServiceOnWindows),
            MinutesBefore = ReadInt(payload, "minutesBefore", current.MinutesBefore),
            AdhanVolume = current.AdhanVolume,
            SoundKey = current.SoundKey,
            CustomSounds = current.CustomSounds,
            PrayerOverrides = current.PrayerOverrides,
            VibrationStrength = ParseWebVibrationStrength(ReadString(payload, "vibrationStrength"), current.VibrationStrength),
            VibrationPattern = ParseWebVibrationPattern(ReadString(payload, "vibrationPattern"), current.VibrationPattern),
            ReminderScope = ParseEnum(ReadString(payload, "reminderScope"), current.ReminderScope),
            ReminderPrayer = ParseEnum(ReadString(payload, "reminderPrayer"), current.ReminderPrayer),
            ReminderItems = reminderItems,
            ReminderOffsetsMinutes = reminderItems.Select(item => item.OffsetMinutes).ToList(),
            PendingDeferredReminder = current.PendingDeferredReminder
        };
    }

    private static AlarmRemindersSettings PatchAlarmReminders(AlarmRemindersSettings current, JsonElement payload) {
        var disabledBuiltIns = new List<string>();
        if (payload.TryGetProperty("builtIn", out var builtIn) && builtIn.ValueKind == JsonValueKind.Array) {
            foreach (var item in builtIn.EnumerateArray()) {
                var id = ReadString(item, "id");
                if (!string.IsNullOrWhiteSpace(id) && !ReadBool(item, "enabled", true)) {
                    disabledBuiltIns.Add(id);
                }
            }
        } else {
            disabledBuiltIns.AddRange(current.DisabledBuiltInIds);
        }

        var userItems = new List<AlarmUserReminderItem>();
        if (payload.TryGetProperty("userReminders", out var userReminders) && userReminders.ValueKind == JsonValueKind.Array) {
            foreach (var item in userReminders.EnumerateArray()) {
                var text = ReadString(item, "text");
                if (string.IsNullOrWhiteSpace(text)) {
                    continue;
                }

                userItems.Add(new AlarmUserReminderItem {
                    Id = ReadString(item, "id") ?? Guid.NewGuid().ToString("N"),
                    Text = text,
                    IsEnabled = ReadBool(item, "enabled", true)
                });
            }
        } else {
            userItems.AddRange(current.UserItems);
        }

        return new AlarmRemindersSettings {
            DisabledBuiltInIds = disabledBuiltIns,
            UserItems = userItems
        };
    }

    private void PatchTasbih(string action, JsonElement payload) {
        var settings = _settingsService.Load();
        var presets = settings.Tasbih.Presets.Select(preset => new TasbihPresetSettings {
            Name = preset.Name,
            RepeatMode = preset.RepeatMode,
            Items = preset.Items.Select(item => new TasbihItemSettings {
                Text = item.Text,
                TargetCount = item.TargetCount
            }).ToList()
        }).ToList();

        var selectedIndex = Math.Clamp(settings.Tasbih.SelectedPresetIndex, 0, Math.Max(0, presets.Count - 1));
        switch (action) {
            case "addTasbihPreset":
                presets.Add(new TasbihPresetSettings {
                    Name = ReadString(payload, "name") ?? T("tasbih"),
                    RepeatMode = TasbihRepeatMode.None,
                    Items = new List<TasbihItemSettings> {
                        new() { Text = WebStateDefaults.DefaultTasbihItemText, TargetCount = WebStateDefaults.DefaultTasbihTargetCount }
                    }
                });
                selectedIndex = presets.Count - 1;
                break;
            case "updateTasbihPreset": {
                var index = ReadIndex(payload, "id");
                if (index < 0 || index >= presets.Count) {
                    throw new ArgumentOutOfRangeException("id", index, "Unknown Tasbih preset index.");
                }
                presets[index] = new TasbihPresetSettings {
                    Name = ReadString(payload, "name") ?? presets[index].Name,
                    RepeatMode = ParseTasbihRepeatMode(ReadString(payload, "repeatMode"), presets[index].RepeatMode),
                    Items = presets[index].Items
                };
                break;
            }
            case "removeTasbihPreset": {
                var index = ReadIndex(payload, "id");
                if (presets.Count <= 1) {
                    throw new InvalidOperationException("The last Tasbih preset cannot be removed.");
                }
                if (index < 0 || index >= presets.Count) {
                    throw new ArgumentOutOfRangeException("id", index, "Unknown Tasbih preset index.");
                }
                presets.RemoveAt(index);
                selectedIndex = index < selectedIndex ? selectedIndex - 1 : Math.Min(selectedIndex, presets.Count - 1);
                break;
            }
            case "addTasbihItem": {
                var index = ReadIndex(payload, "presetId");
                var text = ReadString(payload, "text");
                if (index < 0 || index >= presets.Count) {
                    throw new ArgumentOutOfRangeException("presetId", index, "Unknown Tasbih preset index.");
                }
                if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Tasbih item text is required.", "text");
                presets[index].Items.Add(new TasbihItemSettings {
                    Text = text,
                    TargetCount = RequirePositive(ReadInt(payload, "targetCount", 33), "targetCount")
                });
                break;
            }
            case "updateTasbihItem": {
                var presetIndex = ReadIndex(payload, "presetId");
                var itemIndex = ReadInt(payload, "index", -1);
                RequireTasbihItem(presets, presetIndex, itemIndex);
                var item = presets[presetIndex].Items[itemIndex];
                presets[presetIndex].Items[itemIndex] = new TasbihItemSettings {
                    Text = ReadString(payload, "text") ?? item.Text,
                    TargetCount = RequirePositive(ReadInt(payload, "targetCount", item.TargetCount), "targetCount")
                };
                break;
            }
            case "moveTasbihItem": {
                var presetIndex = ReadIndex(payload, "presetId");
                var itemIndex = ReadInt(payload, "index", -1);
                var direction = ReadString(payload, "direction");
                RequireTasbihItem(presets, presetIndex, itemIndex);
                if (!string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(direction, "down", StringComparison.OrdinalIgnoreCase)) {
                    throw new ArgumentException($"Invalid Tasbih move direction: '{direction ?? "<missing>"}'.", "direction");
                }
                var target = string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase) ? itemIndex - 1 : itemIndex + 1;
                if (target < 0 || target >= presets[presetIndex].Items.Count) {
                    throw new InvalidOperationException("Tasbih item cannot move beyond the collection boundary.");
                }
                (presets[presetIndex].Items[itemIndex], presets[presetIndex].Items[target]) =
                    (presets[presetIndex].Items[target], presets[presetIndex].Items[itemIndex]);
                break;
            }
            case "removeTasbihItem": {
                var presetIndex = ReadIndex(payload, "presetId");
                var itemIndex = ReadInt(payload, "index", -1);
                RequireTasbihItem(presets, presetIndex, itemIndex);
                if (presets[presetIndex].Items.Count <= 1) {
                    throw new InvalidOperationException("The last item in a Tasbih preset cannot be removed.");
                }
                presets[presetIndex].Items.RemoveAt(itemIndex);
                break;
            }
            default:
                throw new ArgumentException($"Unknown Tasbih action: '{action}'.", nameof(action));
        }

        SaveSettings(CopySettings(settings, tasbih: new TasbihSettings {
            Presets = presets,
            SelectedPresetIndex = Math.Clamp(selectedIndex, 0, Math.Max(0, presets.Count - 1))
        }));
    }

    private static void RequireTasbihItem(IReadOnlyList<TasbihPresetSettings> presets, int presetIndex, int itemIndex) {
        if (presetIndex < 0 || presetIndex >= presets.Count) {
            throw new ArgumentOutOfRangeException(nameof(presetIndex), presetIndex, "Unknown Tasbih preset index.");
        }
        if (itemIndex < 0 || itemIndex >= presets[presetIndex].Items.Count) {
            throw new ArgumentOutOfRangeException(nameof(itemIndex), itemIndex, "Unknown Tasbih item index.");
        }
    }

    private static int RequirePositive(int value, string field) => value > 0
        ? value
        : throw new ArgumentOutOfRangeException(field, value, $"{field} must be positive.");

    private static object WriteAutomationReports(JsonElement payload) {
#if DEBUG && PRAY_AUTOMATION
        if (!AutomationRuntime.IsEnabled) {
            throw new InvalidOperationException("Automation report output is disabled. Build Debug with PrayAutomation=true and set PRAY_AUTOMATION=true.");
        }

        var runId = SanitizeAutomationFileName(ReadString(payload, "runId") ?? DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        var root = Path.Combine(FileSystem.AppDataDirectory, "AutomationReports", runId);
        Directory.CreateDirectory(root);
        var passedPath = Path.Combine(root, "passed.md");
        var failedPath = Path.Combine(root, "failed.md");
        File.WriteAllText(passedPath, ReadString(payload, "passedMarkdown") ?? "# Passed scenarios\n\n0 passed.\n");
        File.WriteAllText(failedPath, ReadString(payload, "failedMarkdown") ?? "# Failed scenarios\n\n0 failed.\n");
        return new { ok = true, runId, passedPath, failedPath };
#else
        throw new InvalidOperationException("Automation report output is disabled. Build Debug with PrayAutomation=true and set PRAY_AUTOMATION=true.");
#endif
    }

    private static string SanitizeAutomationFileName(string value) {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "automation" : sanitized;
    }

    private static TasbihRepeatMode ParseTasbihRepeatMode(string? value, TasbihRepeatMode fallback) =>
        value?.ToLowerInvariant() switch {
            "continue" or "loop" or "repeatcontinue" => TasbihRepeatMode.RepeatContinue,
            "reset" or "sequence" or "repeatreset" => TasbihRepeatMode.RepeatReset,
            "none" => TasbihRepeatMode.None,
            null or "" => fallback,
            _ => throw new ArgumentException($"Invalid Tasbih repeat mode: '{value}'.", nameof(value))
        };

    private static string ToWebTasbihRepeatMode(TasbihRepeatMode value) => value switch {
        TasbihRepeatMode.RepeatContinue => "Continue",
        TasbihRepeatMode.RepeatReset => "Reset",
        _ => "None"
    };

    private static PrayerOffsets PatchOffsets(PrayerOffsets current, JsonElement payload) {
        return new PrayerOffsets {
            Fajr = ReadInt(payload, "fajr", current.Fajr),
            Sunrise = ReadInt(payload, "sunrise", current.Sunrise),
            Dhuhr = ReadInt(payload, "dhuhr", current.Dhuhr),
            Asr = ReadInt(payload, "asr", current.Asr),
            Maghrib = ReadInt(payload, "maghrib", current.Maghrib),
            Isha = ReadInt(payload, "isha", current.Isha),
            Imsak = ReadInt(payload, "imsak", current.Imsak)
        };
    }

    private static IReadOnlyList<AdhanPrayerOverride> PatchPrayerOverrides(IReadOnlyList<AdhanPrayerOverride> current, JsonElement payload) {
        if (!TryGetArray(payload, "perPrayerOverrides", out var overrides)) {
            return current;
        }

        var result = new List<AdhanPrayerOverride>();
        foreach (var item in overrides.EnumerateArray()) {
            var prayerName = ReadString(item, "prayer") ?? ReadString(item, "id") ?? "";
            if (!TryParsePrayer(prayerName, out var prayer)) {
                throw new ArgumentException($"Invalid prayer override target: '{prayerName}'.", "perPrayerOverrides");
            }

            var vibration = ReadString(item, "vibration");
            if (vibration is not null && vibration is not ("none" or "default" or "custom" or "enabled")) {
                throw new ArgumentException($"Invalid prayer override vibration: '{vibration}'.", "perPrayerOverrides");
            }

            result.Add(new AdhanPrayerOverride {
                Prayer = prayer,
                SoundKey = string.Equals(ReadString(item, "soundId"), "default", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : ReadString(item, "soundId"),
                EnableVibration = vibration switch {
                    "none" => false,
                    "default" => null,
                    null => null,
                    _ => true
                }
            });
        }

        return result;
    }

    private static List<AdhanReminderItem> ReadAdhanReminderItems(JsonElement payload, IReadOnlyList<AdhanReminderItem> fallback) {
        if (!TryGetArray(payload, "reminders", out var reminders)) {
            return fallback.ToList();
        }

        var result = new List<AdhanReminderItem>();
        foreach (var reminder in reminders.EnumerateArray()) {
            var value = Math.Max(0, ReadInt(reminder, "value", ReadInt(reminder, "offsetMinutes", 0)));
            if (value <= 0) {
                continue;
            }

            var unit = ReadString(reminder, "unit");
            if (unit is not null && !string.Equals(unit, "minute", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(unit, "hour", StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException($"Invalid reminder unit: '{unit}'.", "reminders");
            }
            var minutes = string.Equals(unit, "hour", StringComparison.OrdinalIgnoreCase) ? value * 60 : value;
            var direction = ReadString(reminder, "direction");
            if (direction is not null && !string.Equals(direction, "before", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(direction, "after", StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException($"Invalid reminder direction: '{direction}'.", "reminders");
            }
            if (string.Equals(direction, "after", StringComparison.OrdinalIgnoreCase)) {
                minutes = -minutes;
            }

            result.Add(new AdhanReminderItem {
                OffsetMinutes = minutes,
                AlertType = ParseEnum(ReadString(reminder, "alertType"), AdhanReminderAlertType.Adhan)
            });
        }

        return result;
    }

    private static string? ReadSelectedSound(JsonElement payload) {
        if (!TryGetArray(payload, "sounds", out var sounds)) {
            return null;
        }

        foreach (var sound in sounds.EnumerateArray()) {
            if (ReadBool(sound, "selected", false)) {
                return ReadString(sound, "id");
            }
        }

        return null;
    }

    private static List<int> ReadReminderMinutes(JsonElement payload, string propertyName, IReadOnlyList<int> fallback) {
        if (!TryGetArray(payload, propertyName, out var reminders)) {
            return fallback.ToList();
        }

        var result = new List<int>();
        foreach (var reminder in reminders.EnumerateArray()) {
            var value = Math.Max(0, ReadInt(reminder, "value", 0));
            var unit = ReadString(reminder, "unit");
            if (unit is not null && !string.Equals(unit, "minute", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(unit, "hour", StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException($"Invalid reminder unit: '{unit}'.", propertyName);
            }
            var minutes = string.Equals(unit, "hour", StringComparison.OrdinalIgnoreCase) ? value * 60 : value;
            if (minutes > 0) {
                var direction = ReadString(reminder, "direction");
                if (direction is not null && !string.Equals(direction, "before", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(direction, "after", StringComparison.OrdinalIgnoreCase)) {
                    throw new ArgumentException($"Invalid reminder direction: '{direction}'.", propertyName);
                }
                if (string.Equals(direction, "after", StringComparison.OrdinalIgnoreCase)) {
                    minutes = -minutes;
                }
                result.Add(minutes);
            }
        }

        return result;
    }

    private static JsonElement BuildSectionPayload(string section) {
        return JsonSerializer.SerializeToElement(new { section });
    }

    private object BuildLocationsSettings(AppSettings settings) {
        var knownPlaces = _geoLookupService.GetKnownPlaces()
            .Where(item => !string.IsNullOrWhiteSpace(item.Country))
            .Select(item => new {
                country = string.IsNullOrWhiteSpace(item.Country) ? T("UnknownCountry") : item.Country.Trim(),
                countryCode = string.IsNullOrWhiteSpace(item.CountryCode) ? item.Country.Trim() : item.CountryCode.Trim(),
                city = string.IsNullOrWhiteSpace(item.City) ? T("UnknownCity") : item.City.Trim(),
                latitude = item.Latitude,
                longitude = item.Longitude
            })
            .ToList();

        if (!string.IsNullOrWhiteSpace(settings.Location.Country) ||
            !string.IsNullOrWhiteSpace(settings.Location.CountryCode) ||
            !string.IsNullOrWhiteSpace(settings.Location.City)) {
            var currentCountry = string.IsNullOrWhiteSpace(settings.Location.Country)
                ? settings.Location.CountryCode
                : settings.Location.Country;
            currentCountry = string.IsNullOrWhiteSpace(currentCountry) ? T("UnknownCountry") : currentCountry.Trim();
            var currentCountryCode = string.IsNullOrWhiteSpace(settings.Location.CountryCode)
                ? currentCountry
                : settings.Location.CountryCode;
            var currentCity = string.IsNullOrWhiteSpace(settings.Location.City)
                ? T("UnknownCity")
                : settings.Location.City;
            if (!knownPlaces.Any(item =>
                    string.Equals(item.countryCode, currentCountryCode, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(item.city, currentCity, StringComparison.OrdinalIgnoreCase))) {
                knownPlaces.Insert(0, new {
                    country = currentCountry,
                    countryCode = currentCountryCode.Trim(),
                    city = currentCity.Trim(),
                    latitude = settings.Location.Latitude,
                    longitude = settings.Location.Longitude
                });
            }
        }

        var countries = knownPlaces
            .GroupBy(item => string.IsNullOrWhiteSpace(item.countryCode) ? item.country : item.countryCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => {
                var first = group.First();
                return new {
                    code = first.countryCode,
                    name = first.country,
                    cities = group
                        .Where(item => !string.IsNullOrWhiteSpace(item.city))
                        .GroupBy(item => item.city, StringComparer.OrdinalIgnoreCase)
                        .Select(cityGroup => cityGroup.First().city)
                        .ToList()
                };
            })
            .ToList();

        var countryCode = string.IsNullOrWhiteSpace(settings.Location.CountryCode)
            ? countries.FirstOrDefault()?.code ?? ""
            : settings.Location.CountryCode;
        countryCode = countries.FirstOrDefault(item =>
            string.Equals(item.code, countryCode, StringComparison.OrdinalIgnoreCase))?.code ?? countryCode;
        var countryName = string.IsNullOrWhiteSpace(settings.Location.Country)
            ? countries.FirstOrDefault()?.name ?? ""
            : settings.Location.Country;
        var city = string.IsNullOrWhiteSpace(settings.Location.City)
            ? countries.FirstOrDefault()?.cities.FirstOrDefault() ?? ""
            : settings.Location.City;
        var latitude = settings.Location.Latitude;
        var longitude = settings.Location.Longitude;
        if (!HasUsableCoordinates(latitude, longitude)) {
            var known = knownPlaces.FirstOrDefault(item =>
                (string.Equals(item.countryCode, countryCode, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(item.country, countryName, StringComparison.OrdinalIgnoreCase)) &&
                string.Equals(item.city, city, StringComparison.OrdinalIgnoreCase));
            if (known != null) {
                latitude = known.latitude;
                longitude = known.longitude;
            }
        }

        var locationSource = string.IsNullOrWhiteSpace(settings.Location.Source)
            ? settings.Location.Mode == LocationMode.Gps ? "gps" : "manual"
            : settings.Location.Source.ToLowerInvariant();
        return new {
            useGps = settings.Location.Mode == LocationMode.Gps,
            latitude,
            longitude,
            country = countryCode,
            countryName,
            city,
            locationSource,
            vpnWarning = locationSource == "ip",
            qiblaReadingMode = settings.Qibla.ReadingMode.ToString(),
            qiblaFilterMode = settings.Qibla.FilterMode.ToString(),
            qiblaReadingModes = new[] {
                new { id = QiblaReadingMode.Smooth.ToString(), label = T("CompassReading_Smooth") },
                new { id = QiblaReadingMode.Balanced.ToString(), label = T("CompassReading_Balanced") },
                new { id = QiblaReadingMode.Fast.ToString(), label = T("CompassReading_Fast") },
                new { id = QiblaReadingMode.Raw.ToString(), label = T("CompassReading_Raw") }
            },
            qiblaFilterModes = new[] {
                new { id = QiblaFilterMode.Off.ToString(), label = T("CompassFilter_Off") },
                new { id = QiblaFilterMode.Normal.ToString(), label = T("CompassFilter_Normal") },
                new { id = QiblaFilterMode.Strict.ToString(), label = T("CompassFilter_Strict") }
            },
            countries,
            places = knownPlaces
        };
    }

    private static object BuildThemeSettings(AppSettings settings) {
        return new {
            language = ResolveLanguage(settings.Language),
            themeMode = ResolveTheme(settings.ThemeMode),
            accentColor = AccentFromIndex(settings.AccentIndex),
            textSize = settings.TextScale == 0 ? 100 : settings.TextScale,
            diagnostics = new {
                bridgeReady = true,
                lastSync = DateTime.Now.ToString("t", CultureInfo.CurrentUICulture)
            },
            languages = LocalizationManager.GetAvailableLanguages().Select(item => new { code = item.Code, name = item.Name }).ToList(),
            accentColors = new[] { "teal", "green", "blue", "amber", "rose" }
        };
    }

    private static object BuildAdhanSettings(AppSettings settings) {
        var selectedSound = string.IsNullOrWhiteSpace(settings.Notifications.SoundKey)
            ? "adhan_default"
            : settings.Notifications.SoundKey;
        var sounds = AdhanSoundLibrary.BuildOptions(settings.Notifications, includeUseGlobal: false)
            .Select(option => new {
                id = option.Value,
                label = option.Label,
                selected = string.Equals(option.Value, selectedSound, StringComparison.OrdinalIgnoreCase),
                isCustom = AdhanSoundLibrary.IsCustomSound(settings.Notifications, option.Value),
                canPreview = !AdhanSoundLibrary.IsSilent(option.Value)
            })
            .ToList();

        if (!sounds.Any(sound => sound.selected)) {
            var defaultSound = sounds.FirstOrDefault(sound => string.Equals(sound.id, "adhan_default", StringComparison.OrdinalIgnoreCase));
            if (defaultSound != null) {
                sounds = sounds.Select(sound => new {
                    sound.id,
                    sound.label,
                    selected = string.Equals(sound.id, defaultSound.id, StringComparison.OrdinalIgnoreCase),
                    sound.isCustom,
                    sound.canPreview
                }).ToList();
            }
        }

        return new {
            sounds,
            volume = (int)Math.Round(settings.Notifications.AdhanVolume * 100),
            calculationEngine = WebPrayerMonthFactory.EngineId,
            calculationEngines = new[] {
                new { id = WebPrayerMonthFactory.EngineId, label = T("calculationEngine_SharedCoreAdhan") }
            },
            calculationMethod = settings.Method.ToString(),
            calculationMethods = BuildCalculationMethodOptions(),
            madhhab = settings.Madhhab.ToString(),
            madhhabs = BuildMadhhabOptions(),
            highLatitudeRule = settings.HighLatitudeRule.ToString(),
            highLatitudeRules = BuildHighLatitudeRuleOptions(),
            fajrAngle = settings.SunAngles.Fajr,
            ishaAngle = settings.SunAngles.Isha,
            isCustomMethod = settings.Method == CalculationMethod.Custom,
            offsets = new {
                fajr = settings.Offsets.Fajr,
                sunrise = settings.Offsets.Sunrise,
                dhuhr = settings.Offsets.Dhuhr,
                asr = settings.Offsets.Asr,
                maghrib = settings.Offsets.Maghrib,
                isha = settings.Offsets.Isha,
                imsak = settings.Offsets.Imsak
            },
            clockFormat = settings.ClockFormat switch {
                ClockFormat.TwentyFourHour => "24h",
                ClockFormat.TwelveHour => "12h",
                _ => "auto"
            },
            clockFormats = BuildClockFormatOptions(),
            fasting = new {
                iftarDelay = settings.FastingOffsets.IftarDelayMinutes,
                imsakAdvance = settings.FastingOffsets.ImsakAdvanceMinutes
            },
            imsakReminders = BuildMinuteReminderOptions(settings.FastingReminders.ImsakRemindersMinutes),
            iftarReminders = BuildMinuteReminderOptions(settings.FastingReminders.IftarRemindersMinutes),
            reminderUnits = BuildReminderUnits(),
            reminderDirections = BuildReminderDirections(),
            perPrayerOverrides = new[] {
                BuildPrayerOverride(settings, PrayerId.Fajr),
                BuildPrayerOverride(settings, PrayerId.Dhuhr),
                BuildPrayerOverride(settings, PrayerId.Asr),
                BuildPrayerOverride(settings, PrayerId.Maghrib),
                BuildPrayerOverride(settings, PrayerId.Isha)
            },
            vibrationOverrideOptions = new[] {
                new { id = "default", label = T("useGlobal") },
                new { id = "enabled", label = T("PermissionStatus_Enabled") },
                new { id = "none", label = T("PermissionStatus_Disabled") }
            }
        };
    }

    private static object[] BuildCalculationMethodOptions() {
        return CalculationMethodPresetCatalog.SupportedMethods.Select(method => new {
            id = method.ToString(),
            label = T($"method_{method}")
        }).ToArray();
    }

    private static object[] BuildMadhhabOptions() {
        return new[] {
            Madhhab.Shafi,
            Madhhab.Maliki,
            Madhhab.Hanbali,
            Madhhab.Hanafi
        }.Select(madhhab => new {
            id = madhhab.ToString(),
            label = T($"madhhab_{madhhab}")
        }).ToArray();
    }

    private static object[] BuildHighLatitudeRuleOptions() {
        return new[] {
            HighLatitudeRule.MiddleOfTheNight,
            HighLatitudeRule.SeventhOfTheNight,
            HighLatitudeRule.TwilightAngle
        }.Select(rule => new {
            id = rule.ToString(),
            label = T($"highLatitude_{rule}")
        }).ToArray();
    }

    private static object[] BuildClockFormatOptions() {
        return new[] {
            new { id = "auto", label = T("Clock_Auto") },
            new { id = "12h", label = T("Clock_12h") },
            new { id = "24h", label = T("Clock_24h") }
        };
    }

    private static object BuildNotificationSettings(AppSettings settings) {
        var reminderItems = (settings.Notifications.ReminderItems.Count > 0
                ? settings.Notifications.ReminderItems
                : settings.Notifications.ReminderOffsetsMinutes.Select(minutes => new AdhanReminderItem { OffsetMinutes = minutes }))
            .ToList();
        return new {
            showWindowsControls = DeviceInfo.Platform == DevicePlatform.WinUI,
            enableAdhan = settings.Notifications.EnableAdhan,
            mobilePrimaryAdhanType = settings.Notifications.MobilePrimaryAdhanType.ToString(),
            hideOnCloseWindows = settings.Notifications.HideOnCloseOnWindows,
            runBackgroundServiceWindows = settings.Notifications.RunBackgroundServiceOnWindows,
            vibration = settings.Notifications.EnableVibration,
            vibrationStrength = settings.Notifications.VibrationStrength switch {
                VibrationStrength.Low => "Light",
                VibrationStrength.High => "Strong",
                _ => "Medium"
            },
            vibrationPattern = settings.Notifications.VibrationPattern switch {
                VibrationPattern.Short => "Default",
                VibrationPattern.Long => "Heartbeat",
                _ => "Pulse"
            },
            minutesBefore = settings.Notifications.MinutesBefore,
            reminderScope = settings.Notifications.ReminderScope.ToString(),
            reminderPrayer = settings.Notifications.ReminderPrayer.ToString(),
            reminderScopes = new[] {
                new { id = AdhanReminderScope.All.ToString(), label = T("reminder_All") },
                new { id = AdhanReminderScope.SpecificPrayer.ToString(), label = T("Reminder_Specific") }
            },
            reminderPrayers = new[] {
                new { id = PrayerId.Fajr.ToString(), label = T("prayer_Fajr") },
                new { id = PrayerId.Dhuhr.ToString(), label = T("prayer_Dhuhr") },
                new { id = PrayerId.Asr.ToString(), label = T("prayer_Asr") },
                new { id = PrayerId.Maghrib.ToString(), label = T("prayer_Maghrib") },
                new { id = PrayerId.Isha.ToString(), label = T("prayer_Isha") }
            },
            reminderAlertTypes = new[] {
                new { id = AdhanReminderAlertType.Adhan.ToString(), label = T("reminderType_Adhan") },
                new { id = AdhanReminderAlertType.Notification.ToString(), label = T("reminderType_Notification") },
                new { id = AdhanReminderAlertType.Silent.ToString(), label = T("reminderType_Silent") },
                new { id = AdhanReminderAlertType.Alarm.ToString(), label = T("reminderType_Alarm") }
            },
            reminderUnits = BuildReminderUnits(),
            reminderDirections = BuildReminderDirections(),
            reminders = reminderItems.Select((item, index) => new {
                id = index.ToString(CultureInfo.InvariantCulture),
                offsetMinutes = item.OffsetMinutes,
                value = ReminderDisplayValue(item.OffsetMinutes),
                unit = ReminderDisplayUnit(item.OffsetMinutes),
                direction = item.OffsetMinutes < 0 ? "after" : "before",
                alertType = item.AlertType.ToString(),
                label = FormatReminderLabel(item.OffsetMinutes, item.AlertType)
            }).ToList(),
            pendingDeferredReminder = settings.Notifications.PendingDeferredReminder is { } pending
                ? new {
                    prayer = T($"prayer_{pending.Prayer}"),
                    notifyTime = pending.NotifyTime.ToString("g", CultureInfo.CurrentUICulture),
                    openAlarmScreen = pending.OpenAlarmScreen,
                    label = string.Format(
                        CultureInfo.CurrentUICulture,
                        T("PendingDeferredReminderFormat"),
                        T($"prayer_{pending.Prayer}"),
                        pending.NotifyTime.ToString("g", CultureInfo.CurrentUICulture))
                }
                : null
        };
    }

    private static object BuildPrayerOverride(AppSettings settings, PrayerId prayer) {
        var configured = settings.Notifications.PrayerOverrides.FirstOrDefault(item => item.Prayer == prayer);
        return new {
            prayer = prayer.ToString(),
            label = T($"prayer_{prayer}"),
            soundId = configured?.SoundKey ?? "default",
            vibration = configured?.EnableVibration switch {
                false => "none",
                true => "enabled",
                _ => "default"
            }
        };
    }

    private static object[] BuildMinuteReminderOptions(IReadOnlyList<int> minutes) {
        return minutes.Select((minute, index) => new {
            id = index.ToString(CultureInfo.InvariantCulture),
            offsetMinutes = minute,
            value = ReminderDisplayValue(minute),
            unit = ReminderDisplayUnit(minute),
            direction = minute < 0 ? "after" : "before",
            label = FormatReminderLabel(minute, null)
        }).Cast<object>().ToArray();
    }

    private static object[] BuildReminderUnits() {
        return new object[] {
            new { id = "minute", label = T("minutes") },
            new { id = "hour", label = T("hours") }
        };
    }

    private static object[] BuildReminderDirections() {
        return new object[] {
            new { id = "before", label = T("before") },
            new { id = "after", label = T("after") }
        };
    }

    private static int ReminderDisplayValue(int minutes) {
        var abs = Math.Abs(minutes);
        return abs >= 60 && abs % 60 == 0 ? abs / 60 : abs;
    }

    private static string ReminderDisplayUnit(int minutes) {
        var abs = Math.Abs(minutes);
        return abs >= 60 && abs % 60 == 0 ? "hour" : "minute";
    }

    private static string FormatReminderLabel(int minutes, AdhanReminderAlertType? alertType) {
        var value = ReminderDisplayValue(minutes);
        var unit = ReminderDisplayUnit(minutes) == "hour" ? T("hours") : T("minutes");
        var direction = minutes < 0 ? T("after") : T("before");
        return alertType is { } type
            ? $"{value} {unit} {direction} - {T($"reminderType_{type}")}"
            : $"{value} {unit} {direction}";
    }

    private async Task<object> BuildPermissionsSettingsAsync() {
        IReadOnlyList<AppPermissionSnapshot> snapshots;
        try {
            snapshots = await _permissionCenter.GetSnapshotsAsync().ConfigureAwait(false);
        } catch (Exception exception) {
            _logger.LogException(exception, "NativeAppBackend.BuildPermissionsSettings");
            snapshots = Array.Empty<AppPermissionSnapshot>();
        }

        string alarmStatus;
        try {
            var alarm = await _alarmCapability.GetCurrentDecisionAsync().ConfigureAwait(false);
            alarmStatus = T($"PermissionsAlarmMode_{alarm.SupportStatus}");
        } catch (Exception exception) {
            _logger.LogException(exception, "NativeAppBackend.BuildAlarmPermissionSettings");
            alarmStatus = T("status_error");
        }

        return new {
            alarmMode = new {
                title = T("PermissionsAlarmModeTitle"),
                status = alarmStatus,
                description = T("PermissionsSubtitle")
            },
            items = snapshots.Where(item => item.IsSupported).Select(item => new {
                id = item.Kind.ToString(),
                isGranted = item.IsGranted,
                title = T(PermissionTitleKey(item.Kind)),
                role = item.IsCritical ? "critical" : "optional",
                description = T(PermissionDescriptionKey(item.Kind)),
                fallback = T(PermissionFallbackKey(item.Kind)),
                status = item.IsGranted ? T("PermissionStatus_Enabled") : T("PermissionStatus_Disabled"),
                action = item.IsGranted ? T("PermissionAction_OpenSettings") : T("PermissionAction_Request")
            }).ToList()
        };
    }

    private static object BuildAlarmReminderSettings(AppSettings settings) {
        return new {
            builtIn = new[] {
                new { id = "wudu", text = T("AlarmReminder_Wudu"), enabled = !settings.AlarmReminders.DisabledBuiltInIds.Contains("wudu") },
                new { id = "qibla", text = T("AlarmReminder_Qibla"), enabled = !settings.AlarmReminders.DisabledBuiltInIds.Contains("qibla") }
            },
            userRemindersEnabled = settings.AlarmReminders.UserItems.Any(item => item.IsEnabled),
            userReminders = settings.AlarmReminders.UserItems.Select(item => new {
                id = item.Id,
                text = item.Text,
                enabled = item.IsEnabled
            }).ToList()
        };
    }

    private static string PermissionTitleKey(AppPermissionKind kind) {
        return kind switch {
            AppPermissionKind.Notifications => "PermissionsNotificationsTitle",
            AppPermissionKind.FullScreenIntents => "PermissionsFullScreenIntentTitle",
            AppPermissionKind.DisplayOverApps => "PermissionsOverlayTitle",
            AppPermissionKind.ExactAlarms => "PermissionsExactAlarmTitle",
            AppPermissionKind.Location => "PermissionsLocationTitle",
            _ => "PermissionsTitle"
        };
    }

    private static string PermissionDescriptionKey(AppPermissionKind kind) {
        return kind switch {
            AppPermissionKind.Notifications => "PermissionsNotificationsDescription",
            AppPermissionKind.FullScreenIntents => "PermissionsFullScreenIntentDescription",
            AppPermissionKind.DisplayOverApps => "PermissionsOverlayDescription",
            AppPermissionKind.ExactAlarms => "PermissionsExactAlarmDescription",
            AppPermissionKind.Location => "PermissionsLocationDescription",
            _ => "PermissionsSubtitle"
        };
    }

    private static string PermissionFallbackKey(AppPermissionKind kind) {
        return kind switch {
            AppPermissionKind.Notifications => "PermissionsNotificationsFallback",
            AppPermissionKind.FullScreenIntents => "PermissionsFullScreenIntentFallback",
            AppPermissionKind.DisplayOverApps => "PermissionsOverlayFallback",
            AppPermissionKind.ExactAlarms => "PermissionsExactAlarmFallback",
            AppPermissionKind.Location => "PermissionsLocationFallback",
            _ => "PermissionsSubtitle"
        };
    }

    private async Task<object> BuildOnboardingSnapshotAsync() {
        var permissionSettings = await BuildPermissionsSettingsAsync().ConfigureAwait(false);
        var settings = _settingsService.Load();
        return new {
            language = ResolveLanguage(settings.Language),
            languages = LocalizationManager.GetAvailableLanguages().Select(item => new { code = item.Code, name = item.Name }).ToList(),
            steps = new[] { T("language"), T("PermissionsTitle"), T("Location") },
            step = "location",
            title = T("OnboardingLocationTitle"),
            subtitle = T("OnboardingManualLocationHint"),
            permissions = permissionSettings,
            location = BuildLocationsSettings(settings)
        };
    }

    private static string T(string key) {
        return WebCatalog.Translate(ResolveLanguage(LocalizationManager.CurrentLanguage), key);
    }

    private static bool IsRtl() {
        return string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "ar", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(LocalizationManager.CurrentLanguage, "ar", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveLanguage(string language) {
        return string.IsNullOrWhiteSpace(language) || string.Equals(language, "auto", StringComparison.OrdinalIgnoreCase)
            ? LocalizationManager.CurrentLanguage
            : language;
    }

    private static string ResolveTheme(ThemeMode theme) {
        return theme switch {
            ThemeMode.Dark => "dark",
            ThemeMode.Light => "light",
            _ => "system"
        };
    }

    private object CreateWidgetProfile(JsonElement payload) {
        var templateText = RequireString(payload, "template");
        if (!Enum.TryParse<WidgetTemplateKind>(templateText, true, out var template)) {
            throw new ArgumentException($"Unknown widget template '{templateText}'.", "template");
        }
        var profile = _widgets.Create(template, ReadString(payload, "name"));
        QueueWindowsWidgetRefresh("create-profile");
        var preview = TryBuildRequestedWidgetPreview(payload, profile);
        return new { profile, document = _widgets.Snapshot(), preview };
    }

    private object UpdateWidgetProfile(JsonElement payload) {
        var id = RequireString(payload, "id");
        if (!TryGetObject(payload, "patch", out var value)) throw new ArgumentException("Widget profile patch is required.", "patch");
        var patch = JsonSerializer.Deserialize<WidgetProfilePatch>(value.GetRawText())
            ?? throw new ArgumentException("Widget profile patch is required.", "patch");
        var profile = _widgets.Update(id, patch);
        QueueWindowsWidgetRefresh("update-profile");
        var preview = TryBuildRequestedWidgetPreview(payload, profile);
        return new { profile, document = _widgets.Snapshot(), preview };
    }

    private object DuplicateWidgetProfile(JsonElement payload) {
        var profile = _widgets.Duplicate(RequireString(payload, "id"), ReadString(payload, "name"));
        QueueWindowsWidgetRefresh("duplicate-profile");
        return new { profile, document = _widgets.Snapshot() };
    }

    private object DeleteWidgetProfile(JsonElement payload) {
        var document = _widgets.Delete(RequireString(payload, "id"));
        QueueWindowsWidgetRefresh("delete-profile");
        return new { document };
    }

    private object AssignWidgetProfile(JsonElement payload) {
        if (!TryGetObject(payload, "assignment", out var value)) throw new ArgumentException("Widget assignment is required.", "assignment");
        var assignment = JsonSerializer.Deserialize<WidgetInstanceAssignment>(value.GetRawText())
            ?? throw new ArgumentException("Widget assignment is required.", "assignment");
        var confirmed = _widgets.Assign(assignment);
        QueueWindowsWidgetRefresh("assign-profile");
        return new { assignment = confirmed, document = _widgets.Snapshot() };
    }

    private void QueueWindowsWidgetRefresh(string reason) {
        if (!OperatingSystem.IsWindows()) return;
        _ = _windowsWidgetPublisher.RefreshAsync(reason);
    }

    private object BuildWidgetPreview(JsonElement payload) {
        var profile = TryGetObject(payload, "profile", out var profileValue)
            ? _widgets.ValidatePreview(JsonSerializer.Deserialize<WidgetProfile>(profileValue.GetRawText())
                ?? throw new ArgumentException("Widget preview profile is required.", "profile"))
            : _widgets.Find(RequireString(payload, "profileId"));
        if (!TryGetObject(payload, "capabilities", out var value)) throw new ArgumentException("Widget capabilities are required.", "capabilities");
        var capabilities = JsonSerializer.Deserialize<WidgetHostCapabilities>(value.GetRawText())
            ?? throw new ArgumentException("Widget capabilities are required.", "capabilities");
        return BuildWidgetPreview(profile, capabilities, NormalizeWidgetLanguage(ReadString(payload, "language") ?? ResolveLanguage(LocalizationManager.CurrentLanguage)));
    }

    private object? TryBuildRequestedWidgetPreview(JsonElement payload, WidgetProfile profile) {
        if (!TryGetObject(payload, "previewCapabilities", out var value)) return null;
        var capabilities = JsonSerializer.Deserialize<WidgetHostCapabilities>(value.GetRawText())
            ?? throw new ArgumentException("Widget preview capabilities are invalid.", "previewCapabilities");
        return BuildWidgetPreview(profile, capabilities, NormalizeWidgetLanguage(ReadString(payload, "previewLanguage") ?? ResolveLanguage(LocalizationManager.CurrentLanguage)));
    }

    private object BuildWidgetPreview(WidgetProfile profile, WidgetHostCapabilities capabilities, string language) {
        WidgetProjection projection;
        try {
            var settings = _dataService.LoadSettings();
            var now = DateTime.Now;
            var today = _widgetPrayerFactory.BuildDay(settings, DateOnly.FromDateTime(now));
            var tomorrow = _widgetPrayerFactory.BuildDay(settings, DateOnly.FromDateTime(now.AddDays(1)));
            var selected = _tasbih.SelectedPreset;
            var selectedItem = selected?.Items.FirstOrDefault();
            projection = _widgetProjectionFactory.Build(
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
            projection = _widgetProjectionFactory.Error(exception.Message, ResolveLanguage(LocalizationManager.CurrentLanguage));
        }
        return new {
            profile,
            projection,
            renderTree = _widgetLayoutResolver.Resolve(profile, projection, capabilities)
        };
    }

    private static string RequireString(JsonElement payload, string propertyName) =>
        ReadString(payload, propertyName) is { } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"Missing '{propertyName}'.", propertyName);

    private static string NormalizeWidgetLanguage(string language) => language switch {
        "ar" => "ar",
        "en" => "en",
        _ => throw new ArgumentException($"Unsupported widget preview language '{language}'.", "language")
    };

    private static string? ReadString(JsonElement payload, string propertyName) {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty(propertyName, out var property) ||
            property.ValueKind == JsonValueKind.Null) {
            return null;
        }

        if (property.ValueKind == JsonValueKind.String) {
            return property.GetString();
        }

        throw new ArgumentException($"Invalid '{propertyName}': expected a string.", propertyName);
    }

    private static double ReadDouble(JsonElement payload, string propertyName) {
        return ReadDouble(payload, propertyName, 0);
    }

    private static double ReadDouble(JsonElement payload, string propertyName, double fallback) {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty(propertyName, out var property)) {
            return fallback;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var value) && double.IsFinite(value)) {
            return value;
        }

        throw new ArgumentException($"Invalid '{propertyName}': expected a finite number.", propertyName);
    }

    private static int ReadInt(JsonElement payload, string propertyName, int fallback) {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty(propertyName, out var property)) {
            return fallback;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value)) {
            return value;
        }

        if (property.ValueKind == JsonValueKind.String &&
            int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) {
            return value;
        }

        throw new ArgumentException($"Invalid '{propertyName}': expected an integer.", propertyName);
    }

    private static bool ReadBool(JsonElement payload, string propertyName, bool fallback) {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty(propertyName, out var property)) {
            return fallback;
        }

        return property.ValueKind switch {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var value) => value,
            _ => throw new ArgumentException($"Invalid '{propertyName}': expected a boolean.", propertyName)
        };
    }

    private static bool TryGetObject(JsonElement payload, string propertyName, out JsonElement property) {
        if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty(propertyName, out property)) {
            if (property.ValueKind == JsonValueKind.Object) return true;
            throw new ArgumentException($"Invalid '{propertyName}': expected an object.", propertyName);
        }

        property = default;
        return false;
    }

    private static bool TryGetArray(JsonElement payload, string propertyName, out JsonElement property) {
        if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty(propertyName, out property)) {
            if (property.ValueKind == JsonValueKind.Array) return true;
            throw new ArgumentException($"Invalid '{propertyName}': expected an array.", propertyName);
        }

        property = default;
        return false;
    }

    private static int ReadIndex(JsonElement payload, string propertyName) {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty(propertyName, out var property)) {
            return -1;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var numericIndex)) {
            return numericIndex;
        }

        if (property.ValueKind == JsonValueKind.String &&
            int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var stringIndex)) {
            return stringIndex;
        }

        throw new ArgumentException($"Invalid '{propertyName}': expected an integer index.", propertyName);
    }

    private static VibrationStrength ParseWebVibrationStrength(string? value, VibrationStrength fallback) => value switch {
        "Light" => VibrationStrength.Low,
        "Strong" => VibrationStrength.High,
        _ => ParseEnum(value, fallback)
    };

    private static VibrationPattern ParseWebVibrationPattern(string? value, VibrationPattern fallback) => value switch {
        "Default" => VibrationPattern.Short,
        "Heartbeat" => VibrationPattern.Long,
        _ => ParseEnum(value, fallback)
    };

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum {
        if (string.IsNullOrWhiteSpace(value)) {
            return fallback;
        }

        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)) {
            return parsed;
        }

        throw new ArgumentException($"Invalid {typeof(TEnum).Name}: '{value}'.", typeof(TEnum).Name);
    }

    private static ThemeMode ParseThemeMode(string? value, ThemeMode fallback) {
        return value?.ToLowerInvariant() switch {
            "system" or "auto" => ThemeMode.Auto,
            "light" => ThemeMode.Light,
            "dark" => ThemeMode.Dark,
            null or "" => fallback,
            _ => throw new ArgumentException($"Invalid theme mode: '{value}'.", nameof(value))
        };
    }

    private static ClockFormat ParseClockFormat(string? value, ClockFormat fallback) {
        return value?.ToLowerInvariant() switch {
            "12h" => ClockFormat.TwelveHour,
            "24h" => ClockFormat.TwentyFourHour,
            "auto" => ClockFormat.Auto,
            null or "" => fallback,
            _ => throw new ArgumentException($"Invalid clock format: '{value}'.", nameof(value))
        };
    }

    private static MobilePrimaryAdhanType ParseMobilePrimaryAdhanType(string? value, MobilePrimaryAdhanType fallback) {
        return value?.ToLowerInvariant() switch {
            "full" or "alarm" => MobilePrimaryAdhanType.Alarm,
            "notification" or "adhannotification" => MobilePrimaryAdhanType.AdhanNotification,
            null or "" => fallback,
            _ => throw new ArgumentException($"Invalid primary Adhan type: '{value}'.", nameof(value))
        };
    }

    private static int AccentToIndex(string? value, int fallback) {
        return value?.ToLowerInvariant() switch {
            "amber" => 0,
            "green" => 5,
            "teal" => 6,
            "blue" => 4,
            "rose" => 12,
            null or "" => fallback,
            _ => throw new ArgumentException($"Invalid accent color: '{value}'.", nameof(value))
        };
    }

    private static string AccentFromIndex(int index) {
        return index switch {
            0 => "amber",
            4 => "blue",
            5 => "green",
            6 => "teal",
            12 => "rose",
            _ => "teal"
        };
    }

    private static bool TryParsePrayer(string value, out PrayerId prayer) {
        if (Enum.TryParse(value, ignoreCase: true, out prayer)) {
            return true;
        }

        foreach (var id in Enum.GetValues<PrayerId>()) {
            if (string.Equals(LocalizationManager.TranslatePrayer(id), value, StringComparison.CurrentCultureIgnoreCase)) {
                prayer = id;
                return true;
            }
        }

        prayer = PrayerId.Fajr;
        return false;
    }

    private static AppSettings CopySettings(
        AppSettings current,
        LocationSettings? location = null,
        CalculationMethod? method = null,
        Madhhab? madhhab = null,
        HighLatitudeRule? highLatitudeRule = null,
        SunAngleSettings? sunAngles = null,
        PrayerOffsets? offsets = null,
        FastingOffsets? fastingOffsets = null,
        FastingReminderSettings? fastingReminders = null,
        NotificationSettings? notifications = null,
        AlarmRemindersSettings? alarmReminders = null,
        QiblaPreferences? qibla = null,
        ClockFormat? clockFormat = null,
        int? textScale = null,
        TasbihSettings? tasbih = null,
        string? language = null,
        bool? languageSelected = null,
        ThemeMode? themeMode = null,
        int? accentIndex = null,
        bool? onboardingCompleted = null) {
        return new AppSettings {
            Location = location ?? current.Location,
            Method = method ?? current.Method,
            Madhhab = madhhab ?? current.Madhhab,
            HighLatitudeRule = highLatitudeRule ?? current.HighLatitudeRule,
            SunAngles = sunAngles ?? current.SunAngles,
            Offsets = offsets ?? current.Offsets,
            FastingOffsets = fastingOffsets ?? current.FastingOffsets,
            FastingReminders = fastingReminders ?? current.FastingReminders,
            Notifications = notifications ?? current.Notifications,
            AlarmReminders = alarmReminders ?? current.AlarmReminders,
            Qibla = qibla ?? current.Qibla,
            ClockFormat = clockFormat ?? current.ClockFormat,
            TextScale = textScale ?? current.TextScale,
            Tasbih = tasbih ?? current.Tasbih,
            Language = language ?? current.Language,
            LanguageSelected = languageSelected ?? current.LanguageSelected,
            ThemeMode = themeMode ?? current.ThemeMode,
            AccentIndex = accentIndex ?? current.AccentIndex,
            OnboardingCompleted = onboardingCompleted ?? current.OnboardingCompleted
        };
    }

    private static double NormalizeAngle(double angle) {
        var normalized = (angle % 360 + 360) % 360;
        return normalized > 180 ? 360 - normalized : normalized;
    }
}
