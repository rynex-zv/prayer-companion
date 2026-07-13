using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using MauiWebber;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

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
    private readonly IAdhanPlaybackService _adhanPlaybackService;
    private readonly INotificationBootstrapper _notificationBootstrapper;
    private readonly AndroidAlarmCapabilityService _alarmCapability;
    private readonly MauiWebberUpdater _webUpdater;
    private readonly IAppLogger _logger;
    private readonly AppRevisionCoordinator _revisions = new();
    private readonly ApplicationCoordinator _application;
    private readonly ApplicationOperationCoalescer _operations;
    private readonly IslamicOccasionCatalog _islamicOccasions = new();
    private DateTime _calendarMonth = DateTime.Today;
    private bool _qiblaLoaded;
    private string _qiblaDisplayMode = "compass";
    private string _qiblaVisualFilter = "none";

    public NativeAppBackend(
        TodayWebRpcHandler today,
        ICalendarProjectionSource calendar,
        IQiblaProjectionSource qibla,
        ITasbihProjectionSource tasbih,
        ISettingsRepository settingsService,
        PrayerDataService dataService,
        IAppPermissionCenterService permissionCenter,
        IGeoLookupService geoLookupService,
        IAdhanPlaybackService adhanPlaybackService,
        INotificationBootstrapper notificationBootstrapper,
        AndroidAlarmCapabilityService alarmCapability,
        MauiWebberUpdater webUpdater,
        IApplicationTransactionFactory transactionFactory,
        ApplicationOperationCoalescer operations,
        IAppLogger logger) {
        _today = today;
        _calendar = calendar;
        _qibla = qibla;
        _tasbih = tasbih;
        _settingsService = settingsService;
        _dataService = dataService;
        _permissionCenter = permissionCenter;
        _geoLookupService = geoLookupService;
        _adhanPlaybackService = adhanPlaybackService;
        _notificationBootstrapper = notificationBootstrapper;
        _alarmCapability = alarmCapability;
        _webUpdater = webUpdater;
        _logger = logger;
        _operations = operations;
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
        return _today.PreloadAsync();
    }

    public async Task<object?> HandleAsync(NativeAppOperation operation, JsonElement payload, CancellationToken cancellationToken) {
            var method = operation.Method;
            var currentRevision = _revisions.Snapshot();
            if (method != "app.bootstrap" && operation.Kind == PrayAdFree.Core.Contracts.RpcOperationKind.Query && operation.IfRevision > 0 &&
                currentRevision.Domains.TryGetValue(operation.Domain, out var domainRevision) && domainRevision == operation.IfRevision) {
                return new { notModified = true, revision = domainRevision };
            }
            var execute = new Func<Task<object?>>(async () => method switch {
            "app.bootstrap" => await BuildBootstrapAsync(cancellationToken).ConfigureAwait(false),
            "today.getSnapshot" or "today.refresh" => await _today.HandleAsync(method, payload, cancellationToken).ConfigureAwait(false),
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
            "tasbih.addItem" => PatchTasbihAndSnapshot("addTasbihItem", payload),
            "tasbih.updateItem" => PatchTasbihAndSnapshot("updateTasbihItem", payload),
            "tasbih.moveItem" => PatchTasbihAndSnapshot("moveTasbihItem", payload),
            "tasbih.removeItem" => PatchTasbihAndSnapshot("removeTasbihItem", payload),
            "alarm.getSnapshot" => await GetAlarmSnapshotAsync().ConfigureAwait(false),
            "alarm.snooze" => await SnoozeAlarmAsync(payload).ConfigureAwait(false),
            "alarm.stop" => await StopAlarmAsync().ConfigureAwait(false),
            "alarm.test" => await TestAdhanAlarmAsync(payload).ConfigureAwait(false),
            "notification.test" => await TestAdhanNotificationAsync(payload).ConfigureAwait(false),
            "permissions.request" => await RequestPermissionAsync(payload).ConfigureAwait(false),
            "permissions.requestAll" => await RequestAllPermissionsAsync().ConfigureAwait(false),
            "location.refresh" => await RefreshGpsLocationAsync().ConfigureAwait(false),
            "location.reverseGeocode" => await ReverseGeocodeLocationAsync(payload).ConfigureAwait(false),
            "adhan.sound.preview" => await PreviewAdhanSoundAsync(payload).ConfigureAwait(false),
            "adhan.sound.addCustom" or "adhan.sound.removeCustom" or "external.openEmail" or "external.call" or "external.openUrl" or "external.reportIssue" => new { ok = true },
            "settings.getSnapshot" => await GetSettingsSnapshotAsync(payload).ConfigureAwait(false),
            "settings.update" => await SetSettingsFieldAsync(payload).ConfigureAwait(false),
            "onboarding.getSnapshot" => await BuildOnboardingSnapshotAsync().ConfigureAwait(false),
            "onboarding.complete" => CompleteOnboarding(),
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
        var today = await _today.HandleAsync("today.getSnapshot", default, cancellationToken).ConfigureAwait(false);
        var alarm = await GetAlarmSnapshotAsync().ConfigureAwait(false);
        var onboarding = await BuildOnboardingSnapshotAsync().ConfigureAwait(false);
        var permissionsPayload = JsonSerializer.SerializeToElement(new { section = "permissions" });
        var permissions = await GetSettingsSnapshotAsync(permissionsPayload).ConfigureAwait(false);
        return new {
            contractVersion = PrayAdFree.Core.Contracts.AppProtocol.ContractVersion,
            persistenceSchemaVersion = PrayAdFree.Core.Contracts.AppProtocol.PersistenceSchemaVersion,
            revisions = _revisions.Snapshot(),
            startup = new { route = "/", intent = (string?)null },
            projections = new {
                shell = BuildShellSnapshot(),
                today,
                alarm,
                onboarding,
                permissions,
                capabilities = new { platform = DeviceInfo.Platform.ToString().ToLowerInvariant(), native = true, events = true }
            }
        };
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
            labels = BuildLabels(),
            onboardingCompleted = settings.OnboardingCompleted
        };
    }

    private async Task<object> GetAlarmSnapshotAsync() {
        var settings = _settingsService.Load();
        var language = ResolveLanguage(settings.Language);
        var model = await _adhanPlaybackService.GetActiveAlarmPresentationModelAsync().ConfigureAwait(false);
        return model == null
            ? WebAlarmSnapshotFactory.Inactive(language)
            : WebAlarmSnapshotFactory.Active(
                language,
                model.PrayerClock,
                model.DelayFromBase,
                model.PrayerName,
                model.ReminderText,
                model.CanSnooze,
                model.MinDelayMinutes,
                model.MaxDelayMinutes,
                model.InitialDelayMinutes);
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

    private static Task CloseAlarmHostAsync() => MainThread.InvokeOnMainThreadAsync(async () => {
        var navigation = Shell.Current?.Navigation ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
        if (navigation?.ModalStack.LastOrDefault() is Pages.AdhanSnoozePage) {
            await navigation.PopModalAsync().ConfigureAwait(true);
        }
    });

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
        var mode = ReadString(payload, "mode");
        _qiblaDisplayMode = string.Equals(mode, "map", StringComparison.OrdinalIgnoreCase) ? "map" : "compass";
        return await GetQiblaAsync().ConfigureAwait(false);
    }

    private async Task<object> SetQiblaVisualFilterAsync(JsonElement payload) {
        var mode = ReadString(payload, "mode");
        _qiblaVisualFilter = mode?.ToLowerInvariant() switch {
            "night" => "night",
            "contrast" => "contrast",
            _ => "none"
        };
        return await GetQiblaAsync().ConfigureAwait(false);
    }

    private object RunTasbihCommand(Action command) {
        command();
        return BuildTasbihSnapshot();
    }

    private object SelectTasbihPreset(JsonElement payload) {
        var id = ReadString(payload, "id");
        if (int.TryParse(id, CultureInfo.InvariantCulture, out var index)) _tasbih.SelectPreset(index);

        return BuildTasbihSnapshot();
    }

    private object BuildTasbihSnapshot() {
        return new {
            count = _tasbih.Count,
            currentPhrase = _tasbih.CurrentPhrase,
            progressText = _tasbih.ProgressText,
            isPresetSelectionEnabled = _tasbih.IsPresetSelectionEnabled,
            selectedPresetId = Math.Max(0, _tasbih.Presets.ToList().IndexOf(_tasbih.SelectedPreset!)).ToString(CultureInfo.InvariantCulture),
            presets = _tasbih.Presets.Select((preset, index) => new {
                id = index.ToString(CultureInfo.InvariantCulture),
                name = preset.Name,
                repeatMode = preset.RepeatMode.ToString(),
                items = preset.Items.Select(item => new {
                    text = item.Text,
                    targetCount = item.TargetCount
                }).ToList()
            }).ToList()
        };
    }

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
            _ => new {
                locations = BuildLocationsSettings(settings),
                theme = BuildThemeSettings(settings),
                adhan = BuildAdhanSettings(settings),
                notifications = BuildNotificationSettings(settings),
                permissions = await BuildPermissionsSettingsAsync().ConfigureAwait(false),
                alarmReminders = BuildAlarmReminderSettings(settings)
            }
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
                location: await PatchLocationAsync(next.Location, locations).ConfigureAwait(false),
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
        return new ApplicationCommandExecution(snapshot, [async _ => {
            try {
                await _notificationBootstrapper
                    .EnsureScheduledAsync($"WebSettings:{changedSection}", requestPermissions: false, force: true)
                    .ConfigureAwait(false);
            } catch (Exception exception) {
                _logger.LogException(exception, $"NativeAppBackend.Reconcile.{changedSection}");
            }
        }]);
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
        var sectionPatchResult = await PatchSettingsAsync(JsonSerializer.SerializeToElement(payloadNode)).ConfigureAwait(false);
        var calculated = string.Equals(section, "locations", StringComparison.OrdinalIgnoreCase)
            ? BuildLocationsSettings(_settingsService.Load())
            : null;
        return PreserveAfterCommit(sectionPatchResult, new { ok = true, section, field, value, calculated });
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

    private async Task<object> RequestAllPermissionsAsync() {
        await ResolveAllPermissionsAsync().ConfigureAwait(false);
        return await BuildPermissionsSettingsAsync().ConfigureAwait(false);
    }

    private async Task<object> RequestPermissionAsync(JsonElement payload) {
        await ResolvePermissionAsync(payload).ConfigureAwait(false);
        return await BuildPermissionsSettingsAsync().ConfigureAwait(false);
    }

    private async Task<object> PreviewAdhanSoundAsync(JsonElement payload) {
        var id = ReadString(payload, "id") ?? _settingsService.Load().Notifications.SoundKey;
        var started = await _adhanPlaybackService.PlayPreviewAsync(id).ConfigureAwait(false);
        return new { ok = started, action = "previewSound", id };
    }

    private async Task<object> TestAdhanAlarmAsync(JsonElement payload) {
        var id = ReadString(payload, "id") ?? _settingsService.Load().Notifications.SoundKey;
        var scheduled = await _adhanPlaybackService.ScheduleTestAlarmAsync(id, TimeSpan.FromSeconds(12)).ConfigureAwait(false);
        return new { ok = scheduled, action = "testAlarm", id };
    }

    private async Task<object> TestAdhanNotificationAsync(JsonElement payload) {
        var id = ReadString(payload, "id") ?? _settingsService.Load().Notifications.SoundKey;
        var started = await _adhanPlaybackService.PlayPreviewAsync(id).ConfigureAwait(false);
        return new { ok = started, action = "testNotification", id };
    }

    private async Task ResolveAllPermissionsAsync() {
        var snapshots = await _permissionCenter.GetSnapshotsAsync().ConfigureAwait(false);
        foreach (var snapshot in snapshots.Where(item => item.IsSupported && !item.IsGranted)) {
            await _permissionCenter.ResolveAsync(snapshot.Kind).ConfigureAwait(false);
        }
    }

    private async Task ResolvePermissionAsync(JsonElement payload) {
        var id = ReadString(payload, "id");
        if (string.IsNullOrWhiteSpace(id) ||
            !Enum.TryParse<AppPermissionKind>(id, ignoreCase: true, out var kind)) {
            return;
        }

        await _permissionCenter.ResolveAsync(kind).ConfigureAwait(false);
    }

    private async Task<object> RefreshGpsLocationAsync() {
        var settings = _settingsService.Load();
        var gpsSettings = CopySettings(
            settings,
            location: new LocationSettings {
                Mode = LocationMode.Gps,
                City = string.Empty,
                Country = string.Empty,
                CountryCode = string.Empty,
                Latitude = settings.Location.Latitude,
                Longitude = settings.Location.Longitude,
                TimeZoneId = settings.Location.TimeZoneId,
                LastUpdatedUtc = settings.Location.LastUpdatedUtc
            });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try {
            var updated = await _dataService.UpdateLocationAsync(gpsSettings, timeout.Token, forceRefresh: true)
                .ConfigureAwait(false);
            if (!HasUsableCoordinates(updated.Location.Latitude, updated.Location.Longitude)) {
                throw new InvalidOperationException(T("webGpsUnavailable"));
            }

            SaveSettings(updated);
            return BuildLocationsSettings(updated);
        } catch (OperationCanceledException) {
            throw new InvalidOperationException(T("webGpsTimedOut"));
        }
    }

    private async Task<object> ReverseGeocodeLocationAsync(JsonElement payload) {
        var settings = _settingsService.Load();
        var latitude = ReadDouble(payload, "latitude", settings.Location.Latitude);
        var longitude = ReadDouble(payload, "longitude", settings.Location.Longitude);
        if (!HasUsableCoordinates(latitude, longitude)) {
            return BuildLocationsSettings(settings);
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
            LastUpdatedUtc = DateTime.UtcNow
        };
        var updated = CopySettings(settings, location: location);
        SaveSettings(updated);
        return BuildLocationsSettings(updated);
    }

    private object CompleteOnboarding() {
        var settings = _settingsService.Load();
        SaveSettings(CopySettings(settings, onboardingCompleted: true));
        return BuildShellSnapshot();
    }

    private void SaveSettings(AppSettings settings) {
        _dataService.SaveSettings(settings);
    }

    private async Task<LocationSettings> PatchLocationAsync(LocationSettings current, JsonElement payload) {
        var patched = PatchLocation(current, payload);
        if (patched.Mode == LocationMode.Gps) {
            return patched;
        }

        var latitudeChanged = payload.TryGetProperty("latitude", out _);
        var longitudeChanged = payload.TryGetProperty("longitude", out _);
        if ((latitudeChanged || longitudeChanged) && HasUsableCoordinates(patched.Latitude, patched.Longitude)) {
            var reverse = await _geoLookupService.ReverseAsync(patched.Latitude, patched.Longitude, CancellationToken.None)
                .ConfigureAwait(false);
            if (reverse != null) {
                return new LocationSettings {
                    Mode = LocationMode.Manual,
                    City = string.IsNullOrWhiteSpace(reverse.City) ? patched.City : reverse.City,
                    Country = string.IsNullOrWhiteSpace(reverse.Country) ? patched.Country : reverse.Country,
                    CountryCode = string.IsNullOrWhiteSpace(reverse.CountryCode) ? patched.CountryCode : reverse.CountryCode,
                    Latitude = patched.Latitude,
                    Longitude = patched.Longitude,
                    TimeZoneId = patched.TimeZoneId,
                    LastUpdatedUtc = DateTime.UtcNow
                };
            }

            return new LocationSettings {
                Mode = LocationMode.Manual,
                City = T("UnknownCity"),
                Country = T("UnknownCountry"),
                CountryCode = string.Empty,
                Latitude = patched.Latitude,
                Longitude = patched.Longitude,
                TimeZoneId = patched.TimeZoneId,
                LastUpdatedUtc = DateTime.UtcNow
            };
        }

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
                    LastUpdatedUtc = DateTime.UtcNow
                };
            }
        }

        return patched;
    }

    private GeoLocationResult? FindKnownPlace(string? countryCode, string? country, string? city) {
        return _geoLookupService.GetKnownPlaces().FirstOrDefault(item =>
            (string.IsNullOrWhiteSpace(countryCode) ||
             string.Equals(item.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(item.Country, countryCode, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(country) ||
             string.Equals(item.Country, country, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(item.CountryCode, country, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(city) ||
             string.Equals(item.City, city, StringComparison.OrdinalIgnoreCase)));
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
            LastUpdatedUtc = DateTime.UtcNow
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
            VibrationStrength = ParseEnum(ReadString(payload, "vibrationStrength"), current.VibrationStrength),
            VibrationPattern = ParseEnum(ReadString(payload, "vibrationPattern"), current.VibrationPattern),
            ReminderScope = ParseEnum(ReadString(payload, "reminderScope"), current.ReminderScope),
            ReminderPrayer = ParseEnum(ReadString(payload, "reminderPrayer"), current.ReminderPrayer),
            ReminderItems = ReadAdhanReminderItems(payload, current.ReminderItems),
            ReminderOffsetsMinutes = ReadAdhanReminderItems(payload, current.ReminderItems).Select(item => item.OffsetMinutes).ToList(),
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

        switch (action) {
            case "addTasbihPreset":
                presets.Add(new TasbihPresetSettings {
                    Name = ReadString(payload, "name") ?? T("tasbih"),
                    RepeatMode = TasbihRepeatMode.None,
                    Items = new List<TasbihItemSettings>()
                });
                break;
            case "updateTasbihPreset": {
                var index = ReadIndex(payload, "id");
                if (index >= 0 && index < presets.Count) {
                    presets[index] = new TasbihPresetSettings {
                        Name = ReadString(payload, "name") ?? presets[index].Name,
                        RepeatMode = ParseEnum(ReadString(payload, "repeatMode"), presets[index].RepeatMode),
                        Items = presets[index].Items
                    };
                }
                break;
            }
            case "addTasbihItem": {
                var index = ReadIndex(payload, "presetId");
                var text = ReadString(payload, "text");
                if (index >= 0 && index < presets.Count && !string.IsNullOrWhiteSpace(text)) {
                    presets[index].Items.Add(new TasbihItemSettings {
                        Text = text,
                        TargetCount = Math.Max(1, ReadInt(payload, "targetCount", 33))
                    });
                }
                break;
            }
            case "updateTasbihItem": {
                var presetIndex = ReadIndex(payload, "presetId");
                var itemIndex = ReadInt(payload, "index", -1);
                if (presetIndex >= 0 && presetIndex < presets.Count && itemIndex >= 0 && itemIndex < presets[presetIndex].Items.Count) {
                    var item = presets[presetIndex].Items[itemIndex];
                    presets[presetIndex].Items[itemIndex] = new TasbihItemSettings {
                        Text = ReadString(payload, "text") ?? item.Text,
                        TargetCount = Math.Max(1, ReadInt(payload, "targetCount", item.TargetCount))
                    };
                }
                break;
            }
            case "moveTasbihItem": {
                var presetIndex = ReadIndex(payload, "presetId");
                var itemIndex = ReadInt(payload, "index", -1);
                var direction = ReadString(payload, "direction");
                if (presetIndex >= 0 && presetIndex < presets.Count && itemIndex >= 0 && itemIndex < presets[presetIndex].Items.Count) {
                    var target = string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase) ? itemIndex - 1 : itemIndex + 1;
                    if (target >= 0 && target < presets[presetIndex].Items.Count) {
                        (presets[presetIndex].Items[itemIndex], presets[presetIndex].Items[target]) =
                            (presets[presetIndex].Items[target], presets[presetIndex].Items[itemIndex]);
                    }
                }
                break;
            }
            case "removeTasbihItem": {
                var presetIndex = ReadIndex(payload, "presetId");
                var itemIndex = ReadInt(payload, "index", -1);
                if (presetIndex >= 0 && presetIndex < presets.Count && itemIndex >= 0 && itemIndex < presets[presetIndex].Items.Count) {
                    presets[presetIndex].Items.RemoveAt(itemIndex);
                }
                break;
            }
        }

        SaveSettings(CopySettings(settings, tasbih: new TasbihSettings {
            Presets = presets,
            SelectedPresetIndex = Math.Clamp(settings.Tasbih.SelectedPresetIndex, 0, Math.Max(0, presets.Count - 1))
        }));
    }

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
        if (!payload.TryGetProperty("perPrayerOverrides", out var overrides) || overrides.ValueKind != JsonValueKind.Array) {
            return current;
        }

        var result = new List<AdhanPrayerOverride>();
        foreach (var item in overrides.EnumerateArray()) {
            var prayerName = ReadString(item, "prayer") ?? ReadString(item, "id") ?? "";
            if (!TryParsePrayer(prayerName, out var prayer)) {
                continue;
            }

            result.Add(new AdhanPrayerOverride {
                Prayer = prayer,
                SoundKey = string.Equals(ReadString(item, "soundId"), "default", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : ReadString(item, "soundId"),
                EnableVibration = ReadString(item, "vibration") switch {
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
        if (!payload.TryGetProperty("reminders", out var reminders) || reminders.ValueKind != JsonValueKind.Array) {
            return fallback.ToList();
        }

        var result = new List<AdhanReminderItem>();
        foreach (var reminder in reminders.EnumerateArray()) {
            var value = Math.Max(0, ReadInt(reminder, "value", ReadInt(reminder, "offsetMinutes", 0)));
            if (value <= 0) {
                continue;
            }

            var unit = ReadString(reminder, "unit");
            var minutes = string.Equals(unit, "hour", StringComparison.OrdinalIgnoreCase) ? value * 60 : value;
            if (string.Equals(ReadString(reminder, "direction"), "after", StringComparison.OrdinalIgnoreCase)) {
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
        if (!payload.TryGetProperty("sounds", out var sounds) || sounds.ValueKind != JsonValueKind.Array) {
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
        if (!payload.TryGetProperty(propertyName, out var reminders) || reminders.ValueKind != JsonValueKind.Array) {
            return fallback.ToList();
        }

        var result = new List<int>();
        foreach (var reminder in reminders.EnumerateArray()) {
            var value = Math.Max(0, ReadInt(reminder, "value", 0));
            var unit = ReadString(reminder, "unit");
            var minutes = string.Equals(unit, "hour", StringComparison.OrdinalIgnoreCase) ? value * 60 : value;
            if (minutes > 0) {
                if (string.Equals(ReadString(reminder, "direction"), "after", StringComparison.OrdinalIgnoreCase)) {
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

        return new {
            useGps = settings.Location.Mode == LocationMode.Gps,
            latitude,
            longitude,
            country = countryCode,
            countryName,
            city,
            vpnWarning = false,
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
        return new[] {
            CalculationMethod.Auto,
            CalculationMethod.Jafari,
            CalculationMethod.Karachi,
            CalculationMethod.Isna,
            CalculationMethod.MuslimWorldLeague,
            CalculationMethod.UmmAlQura,
            CalculationMethod.Egypt,
            CalculationMethod.Tehran,
            CalculationMethod.Gulf,
            CalculationMethod.Kuwait,
            CalculationMethod.Qatar,
            CalculationMethod.Singapore,
            CalculationMethod.France,
            CalculationMethod.Turkey,
            CalculationMethod.Russia,
            CalculationMethod.Moonsighting,
            CalculationMethod.Dubai,
            CalculationMethod.Jakim,
            CalculationMethod.Tunisia,
            CalculationMethod.Algeria,
            CalculationMethod.Kemenag,
            CalculationMethod.Morocco,
            CalculationMethod.Portugal,
            CalculationMethod.Jordan,
            CalculationMethod.Custom
        }.Select(method => new {
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
            enableAdhan = settings.Notifications.EnableAdhan,
            mobilePrimaryAdhanType = settings.Notifications.MobilePrimaryAdhanType.ToString(),
            hideOnCloseWindows = settings.Notifications.HideOnCloseOnWindows,
            runBackgroundServiceWindows = settings.Notifications.RunBackgroundServiceOnWindows,
            vibration = settings.Notifications.EnableVibration,
            vibrationStrength = settings.Notifications.VibrationStrength.ToString(),
            vibrationPattern = settings.Notifications.VibrationPattern.ToString(),
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
        var snapshots = await _permissionCenter.GetSnapshotsAsync().ConfigureAwait(false);
        var alarm = await _alarmCapability.GetCurrentDecisionAsync().ConfigureAwait(false);
        return new {
            alarmMode = new {
                title = T("PermissionsAlarmModeTitle"),
                status = T($"PermissionsAlarmMode_{alarm.SupportStatus}"),
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

    private static string? ReadString(JsonElement payload, string propertyName) {
        return payload.ValueKind == JsonValueKind.Object &&
               payload.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static double ReadDouble(JsonElement payload, string propertyName) {
        return ReadDouble(payload, propertyName, 0);
    }

    private static double ReadDouble(JsonElement payload, string propertyName, double fallback) {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty(propertyName, out var property)) {
            return fallback;
        }

        return property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var value)
            ? value
            : fallback;
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

        return fallback;
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
            _ => fallback
        };
    }

    private static bool TryGetObject(JsonElement payload, string propertyName, out JsonElement property) {
        if (payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty(propertyName, out property) &&
            property.ValueKind == JsonValueKind.Object) {
            return true;
        }

        property = default;
        return false;
    }

    private static int ReadIndex(JsonElement payload, string propertyName) {
        var value = ReadString(payload, propertyName);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
            ? index
            : ReadInt(payload, propertyName, -1);
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum {
        if (string.IsNullOrWhiteSpace(value)) {
            return fallback;
        }

        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            ? parsed
            : fallback;
    }

    private static ThemeMode ParseThemeMode(string? value, ThemeMode fallback) {
        return value?.ToLowerInvariant() switch {
            "system" or "auto" => ThemeMode.Auto,
            "light" => ThemeMode.Light,
            "dark" => ThemeMode.Dark,
            _ => fallback
        };
    }

    private static ClockFormat ParseClockFormat(string? value, ClockFormat fallback) {
        return value?.ToLowerInvariant() switch {
            "12h" => ClockFormat.TwelveHour,
            "24h" => ClockFormat.TwentyFourHour,
            "auto" => ClockFormat.Auto,
            _ => fallback
        };
    }

    private static MobilePrimaryAdhanType ParseMobilePrimaryAdhanType(string? value, MobilePrimaryAdhanType fallback) {
        return value?.ToLowerInvariant() switch {
            "full" or "alarm" => MobilePrimaryAdhanType.Alarm,
            "notification" or "adhannotification" => MobilePrimaryAdhanType.AdhanNotification,
            _ => fallback
        };
    }

    private static int AccentToIndex(string? value, int fallback) {
        return value?.ToLowerInvariant() switch {
            "amber" => 0,
            "green" => 5,
            "teal" => 6,
            "blue" => 4,
            "rose" => 12,
            _ => fallback
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
