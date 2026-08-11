using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Contracts;

namespace PrayAdFree.Core.Services;

public sealed class WebCoreRpcDispatcher {
    private readonly DailyPrayerSnapshotFactory _dailyFactory = new();
    private readonly FastingSnapshotFactory _fastingFactory = new();
    private readonly CalendarMonthPresenter _calendarPresenter = new();
    private readonly TasbihProgressCalculator _tasbihCalculator = new();
    private readonly WebPrayerMonthFactory _prayerMonthFactory = new();
    private readonly IslamicOccasionCatalog _occasions = new();
    private readonly AppRevisionCoordinator _revisions;
    private WebState _state = WebState.Default();

    public WebCoreRpcDispatcher(WebState? state = null, AppRevision? revision = null) {
        _state = state ?? WebState.Default();
        _state.EnsureDefaults();
        _revisions = new AppRevisionCoordinator(revision);
    }

    public WebState CaptureState() => _state;
    public AppRevision CaptureRevision() => _revisions.Snapshot();

    private static readonly string[] HijriMonthNames = {
        "Muharram", "Safar", "Rabi al-awwal", "Rabi al-thani",
        "Jumada al-awwal", "Jumada al-thani", "Rajab", "Shaban",
        "Ramadan", "Shawwal", "Dhu al-Qadah", "Dhu al-Hijjah"
    };

    private static int HijriMonthNumber(string name) {
        for (int i = 0; i < HijriMonthNames.Length; i++)
            if (string.Equals(HijriMonthNames[i], name, StringComparison.OrdinalIgnoreCase)) return i + 1;
        return 0;
    }

    public object? Dispatch(string method, JsonElement payload) {
        var platformTimeZone = ReadNestedString(payload, "_platform", "timeZoneId");
        if (!string.IsNullOrWhiteSpace(platformTimeZone)) _state.TimeZoneId = platformTimeZone;
        var operationKind = WebContractExporter.Classify(method);
        var domain = ReadNestedString(payload, "_rpc", "domain") ?? method.Split('.')[0];
        var ifRevision = ReadNestedLong(payload, "_query", "ifRevision");
        var current = _revisions.Snapshot();
        if (method != "app.bootstrap" && operationKind == RpcOperationKind.Query && ifRevision > 0 &&
            current.Domains.TryGetValue(domain, out var domainRevision) && domainRevision == ifRevision) {
            return new { notModified = true, revision = domainRevision };
        }
        var result = method switch {
            "app.bootstrap" => Bootstrap(),
            "app.getShellSnapshot" => ShellSnapshot(),
            "app.getLocalization" => Labels(),
            "app.getLanguageObject" => LanguageObject(GetString(payload, "language", _state.Language)),
            "app.setLanguage" => SetLanguage(GetString(payload, "language", _state.Language)),
            "app.setTheme" => SetTheme(GetString(payload, "theme", _state.ThemeMode)),

            "today.getSnapshot" or "today.refresh" => TodaySnapshot(),

            "calendar.getSnapshot" => CalendarSnapshot(GetString(payload, "month", null)),
            "calendar.setMonth" => SetCalendarMonth(GetString(payload, "month", null)),
            "calendar.today" => SetCalendarMonth(DateTime.Today.ToString("yyyy-MM", CultureInfo.InvariantCulture)),
            "calendar.nextMonth" => MoveCalendar(1),
            "calendar.previousMonth" => MoveCalendar(-1),

            "qibla.getSnapshot" => QiblaSnapshot(),
            "qibla.updateHeading" => SetHeading(GetDouble(payload, "heading", _state.Heading)),
            "qibla.setHeadingMode" => SetHeadingMode(GetString(payload, "mode", _state.HeadingMode)),
            "qibla.adjustManualHeading" => AdjustManualHeading(GetDouble(payload, "delta", 0)),
            "qibla.commitManualHeading" => QiblaSnapshot(),
            "qibla.setDisplayMode" => SetDisplayMode(GetString(payload, "mode", _state.ReadingMode)),
            "qibla.setVisualFilter" => SetVisualFilter(GetString(payload, "mode", _state.FilterMode)),

            "tasbih.getSnapshot" => TasbihSnapshot(),
            "tasbih.increment" => IncrementTasbih(),
            "tasbih.reset" => ResetTasbih(),
            "tasbih.selectPreset" => SelectTasbihPreset(GetString(payload, "id", _state.SelectedTasbihPresetId)),
            "tasbih.addPreset" => AddTasbihPreset(GetString(payload, "name", T("newPresetName"))),
            "tasbih.updatePreset" => UpdateTasbihPreset(payload),
            "tasbih.removePreset" => RemoveTasbihPreset(payload),
            "tasbih.addItem" => AddTasbihItem(payload),
            "tasbih.updateItem" => UpdateTasbihItem(payload),
            "tasbih.moveItem" => MoveTasbihItem(payload),
            "tasbih.removeItem" => RemoveTasbihItem(payload),

            "alarm.getSnapshot" or "alarm.snooze" or "alarm.stop" => AlarmSnapshot(),
            "alarm.test" => NativeAction("testAlarm", ok: false),
            "notification.test" => NativeAction("testNotification", ok: false),
            "permissions.request" => NativeAction("requestPermission", ok: true),
            "permissions.requestAll" => NativeAction("requestAllPermissions", ok: true),
            "location.refresh" => NativeAction("refreshGps", ok: false),
            "location.reverseGeocode" => NativeAction("reverseGeocode", ok: false),
            "adhan.sound.addCustom" => NativeAction("addCustomAdhanSound", ok: false),
            "adhan.sound.preview" => NativeAction("previewSound", ok: false),
            "adhan.sound.removeCustom" => NativeAction("removeCustomAdhanSound", ok: false),
            "external.openUrl" => new { ok = true, action = "openUrl", platform = "web", intent = CloneJsonValue(payload) },
            "external.openEmail" => new { ok = true, action = "openEmail", platform = "web", intent = CloneJsonValue(payload) },
            "external.call" => new { ok = true, action = "call", platform = "web", intent = CloneJsonValue(payload) },
            "external.reportIssue" => NativeAction("reportIssue", ok: false),

            "settings.getSnapshot" => SettingsSnapshot(GetString(payload, "section", "")),
            "settings.update" => SetSettingsField(payload),

            "onboarding.getSnapshot" => OnboardingSnapshot(),
            "onboarding.complete" => CompleteOnboarding(),
            "mauiWebber.getRemoteUrl" => RemoteUrlSnapshot(),
            "mauiWebber.setRemoteUrl" => SetRemoteUrl(GetString(payload, "url", _state.RemoteWebUrl)),
            "mauiWebber.pullRemote" => BrowserUnavailable(T("webRemotePullUnavailable")),
            "mauiWebber.useEmbedded" => BrowserUnavailable(T("webEmbeddedResetUnavailable")),
            _ => throw new InvalidOperationException($"No web core handler for \"{method}\".")
        };
        if (operationKind is RpcOperationKind.Command or RpcOperationKind.CompatibilityAdapter) {
            _revisions.Changed(domain, ReadNestedString(payload, "_rpc", "requestId"), invalidationKey: $"{domain}.*");
        }
        return result;
    }

    public IReadOnlyList<AppEvent> DrainEvents() => _revisions.DrainEvents();

    private object Bootstrap() => new {
        contractVersion = AppProtocol.ContractVersion,
        persistenceSchemaVersion = AppProtocol.PersistenceSchemaVersion,
        revisions = _revisions.Snapshot(),
        startup = new { route = "/", intent = (string?)null },
        projections = new {
            shell = ShellSnapshot(),
            today = TodaySnapshot(),
            capabilities = new { platform = "browser", native = false, events = true }
        }
    };

    private static long ReadNestedLong(JsonElement payload, string parent, string name) {
        return payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty(parent, out var node) && node.ValueKind == JsonValueKind.Object &&
            node.TryGetProperty(name, out var value) && value.TryGetInt64(out var result) ? result : 0;
    }

    private static string? ReadNestedString(JsonElement payload, string parent, string name) {
        return payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty(parent, out var node) && node.ValueKind == JsonValueKind.Object &&
            node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private object ShellSnapshot() => new {
        route = "/",
        language = _state.Language,
        isRtl = WebCatalog.IsRtl(_state.Language),
        languageObject = LanguageObject(_state.Language),
        languages = WebCatalog.Languages.Select(item => new { code = item.Code, name = item.Name, direction = item.Direction }).ToArray(),
        themeMode = _state.ThemeMode,
        accentColor = _state.AccentColor,
        textSize = _state.TextSize,
        tabs = WebCatalog.LocalizedShellTabs(_state.Language),
        labels = Labels(),
        onboardingCompleted = _state.OnboardingCompleted
    };

    private object LanguageObject(string? language) {
        var normalized = WebCatalog.NormalizeLanguage(language);
        return new {
            code = normalized,
            direction = WebCatalog.IsRtl(normalized) ? "rtl" : "ltr",
            labels = WebCatalog.Labels(normalized),
            updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    private object SetLanguage(string? language) {
        _state.Language = WebCatalog.NormalizeLanguage(language);
        return new { ok = true, languageObject = LanguageObject(_state.Language) };
    }

    private object SetTheme(string? theme) {
        _state.ThemeMode = WebCatalog.NormalizeTheme(theme);
        return new { ok = true };
    }

    private object TodaySnapshot() {
        try {
            return BuildTodaySnapshot();
        } catch (ArgumentException exception) {
            var adhan = ReadStoredAdhanSettings();
            var selectedMethod = adhan.Method;
            return new {
                locationTitle = LocationTitle(),
                hijriDate = string.Empty,
                gregorianDate = DateTime.Now.ToString("dddd, dd MMMM yyyy", DisplayCulture()),
                currentTime = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                nextPrayerId = "Fajr",
                nextPrayerClock = string.Empty,
                nextPrayerBaseClock = string.Empty,
                showNextPrayerBaseClock = false,
                nextPrayerDayId = "today",
                countdown = string.Empty,
                nextPrayerAt = (long?)null,
                statusMessage = exception.Message,
                calculation = new {
                    selectedMethod = selectedMethod.ToString(),
                    selectedMethodLabel = T($"method_{selectedMethod}"),
                    effectiveMethod = selectedMethod.ToString(),
                    effectiveMethodLabel = T($"method_{selectedMethod}"),
                    madhhab = adhan.Madhhab.ToString(),
                    madhhabLabel = T($"madhhab_{adhan.Madhhab}"),
                    highLatitudeRule = adhan.HighLatitudeRule.ToString(),
                    highLatitudeRuleLabel = T($"highLatitude_{adhan.HighLatitudeRule}")
                },
                imsakTime = string.Empty,
                iftarTime = string.Empty,
                isImsakNext = false,
                isIftarNext = false,
                nextFastingCountdown = string.Empty,
                isRtl = WebCatalog.IsRtl(_state.Language),
                error = T("UnableToLoadPrayerTimes"),
                todayTimings = Array.Empty<object>()
            };
        }
    }

    private object BuildTodaySnapshot() {
        var now = DateTime.Now;
        var settings = BuildSettings();
        var day = _prayerMonthFactory.BuildDay(settings, DateOnly.FromDateTime(now));
        var tomorrow = _prayerMonthFactory.BuildDay(settings, DateOnly.FromDateTime(now.AddDays(1)));
        var snapshot = _dailyFactory.Build(day, settings, now);
        var fasting = _fastingFactory.Build(day, tomorrow, settings, now);
        var next = snapshot.NextPrayerTime;
        var effectiveMethod = settings.Method == CalculationMethod.Auto
            ? MethodResolver.ResolveRequired(settings.Location.CountryCode)
            : settings.Method;

        return new {
            locationTitle = LocationTitle(),
            hijriDate = day.Hijri.Date,
            gregorianDate = now.ToString("dddd, dd MMMM yyyy", DisplayCulture()),
            currentTime = FormatLiveClock(now, settings),
            nextPrayerId = snapshot.NextPrayerId.ToString(),
            nextPrayerClock = Format(next, settings),
            nextPrayerBaseClock = snapshot.NextPrayerBaseTime.HasValue ? Format(snapshot.NextPrayerBaseTime.Value, settings) : Format(next, settings),
            showNextPrayerBaseClock = snapshot.NextPrayerBaseTime.HasValue,
            nextPrayerDayId = snapshot.IsNextPrayerTomorrow ? "tomorrow" : "today",
            countdown = FormatDuration(next - now),
            nextPrayerAt = ToUnixMilliseconds(next, settings.Location.TimeZoneId),
            statusMessage = "",
            calculation = new {
                selectedMethod = settings.Method.ToString(),
                selectedMethodLabel = T($"method_{settings.Method}"),
                effectiveMethod = effectiveMethod.ToString(),
                effectiveMethodLabel = T($"method_{effectiveMethod}"),
                madhhab = settings.Madhhab.ToString(),
                madhhabLabel = T($"madhhab_{settings.Madhhab}"),
                highLatitudeRule = settings.HighLatitudeRule.ToString(),
                highLatitudeRuleLabel = T($"highLatitude_{settings.HighLatitudeRule}")
            },
            imsakTime = Format(fasting.ImsakTime, settings),
            iftarTime = Format(fasting.IftarTime, settings),
            isImsakNext = fasting.IsImsakNext,
            isIftarNext = fasting.IsIftarNext,
            nextFastingCountdown = FormatDuration(fasting.Remaining),
            isRtl = WebCatalog.IsRtl(_state.Language),
            todayTimings = snapshot.Entries.Select(entry => new {
                id = entry.Prayer.ToString().ToLowerInvariant(),
                time = Format(entry.AdjustedTime, settings),
                baseTime = Format(entry.BaseTime, settings),
                isNext = entry.IsNext
            }).ToArray()
        };
    }

    private CultureInfo DisplayCulture() => CultureInfo.GetCultureInfo(_state.Language switch {
        "ar" => "ar-SA",
        "fr" => "fr-FR",
        "es" => "es-ES",
        "tr" => "tr-TR",
        _ => "en-US"
    });

    private static long ToUnixMilliseconds(DateTime localTime, string timeZoneId) {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var offset = timeZone.GetUtcOffset(localTime);
        return new DateTimeOffset(DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified), offset).ToUnixTimeMilliseconds();
    }

    private object CalendarSnapshot(string? monthValue = null) {
        if (!string.IsNullOrWhiteSpace(monthValue) && DateTime.TryParseExact(monthValue + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) {
            _state.SelectedMonth = _calendarPresenter.NormalizeMonth(parsed);
        }

        var settings = BuildSettings();
        var month = _prayerMonthFactory.BuildMonth(settings, _state.SelectedMonth.Year, _state.SelectedMonth.Month);
        var rows = _calendarPresenter.BuildRows(month, settings, CultureInfo.InvariantCulture);
        var occasions = _occasions.ForMadhhab(settings.Madhhab);
        var occasionByHijri = occasions
            .GroupBy(o => (o.HijriMonth, o.HijriDay))
            .ToDictionary(g => g.Key, g => g.First());

        var daysPayload = new List<object>(rows.Count);
        for (int i = 0; i < rows.Count; i++) {
            var row = rows[i];
            var hijri = month.Days[i].Hijri;
            int hDay = int.TryParse(hijri.Day, out var hd) ? hd : 0;
            int hMonth = HijriMonthNumber(hijri.Month);
            int hYear = int.TryParse(hijri.Year, out var hy) ? hy : 0;
            occasionByHijri.TryGetValue((hMonth, hDay), out var occ);
            var sourceDt = row.SourceDate.ToDateTime(TimeOnly.MinValue);
            daysPayload.Add(new {
                sourceDate = row.SourceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                weekday = (int)sourceDt.DayOfWeek,
                dayNumber = row.SourceDate.Day,
                date = row.Date,
                hijri = row.Hijri,
                hijriDay = hDay,
                hijriMonth = hMonth,
                hijriMonthName = hijri.Month,
                hijriYear = hYear,
                fajr = row.Fajr, sunrise = row.Sunrise, dhuhr = row.Dhuhr,
                asr = row.Asr, maghrib = row.Maghrib, isha = row.Isha,
                isToday = row.SourceDate == DateOnly.FromDateTime(DateTime.Today),
                occasionKey = occ?.LabelKey,
                occasionColor = occ?.Color,
                occasionImportance = occ?.Importance
            });
        }

        var firstHijri = month.Days.Count > 0 ? month.Days[0].Hijri : null;
        var lastHijri = month.Days.Count > 0 ? month.Days[^1].Hijri : null;
        var hijriMonthLabel = firstHijri is null
            ? ""
            : firstHijri.Month == lastHijri!.Month
                ? $"{firstHijri.Month} {firstHijri.Year}"
                : $"{firstHijri.Month} – {lastHijri.Month} {lastHijri.Year}";

        return new {
            selectedMonth = _state.SelectedMonth.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            selectedMonthValue = _state.SelectedMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            monthName = _state.SelectedMonth.ToString("MMMM", CultureInfo.InvariantCulture),
            yearNumber = _state.SelectedMonth.Year,
            monthNumber = _state.SelectedMonth.Month,
            hijriMonthLabel,
            statusMessage = "",
            days = daysPayload.ToArray(),
            madhhab = settings.Madhhab.ToString(),
            isRtl = WebCatalog.IsRtl(_state.Language)
        };
    }


    private object SetCalendarMonth(string? month) => CalendarSnapshot(month);

    private object MoveCalendar(int offset) {
        _state.SelectedMonth = _calendarPresenter.MoveMonth(_state.SelectedMonth, offset);
        return CalendarSnapshot();
    }

    private object QiblaSnapshot() {
        var bearing = QiblaCalculator.CalculateBearing(_state.Latitude, _state.Longitude);
        var heading = _state.HeadingMode == "manual" ? _state.ManualHeading : _state.Heading;
        var needleRotation = NormalizeDegrees(bearing - heading);
        var aligned = Math.Abs(needleRotation) < 5 || Math.Abs(needleRotation - 360) < 5;
        var displayMode = WebCatalog.QiblaDisplayLabel(_state.Language, _state.ReadingMode);

        return new {
            bearing,
            heading,
            latitude = _state.Latitude,
            longitude = _state.Longitude,
            needleRotation,
            compassRotation = -heading,
            directionLabel = DirectionLabel(bearing),
            locationTitle = LocationTitle(),
            statusMessage = aligned ? T("aligned") : "",
            selectedHeadingMode = _state.HeadingMode,
            selectedReadingMode = _state.ReadingMode,
            selectedFilterMode = _state.FilterMode,
            displayMode,
            visualFilter = WebCatalog.QiblaFilterLabel(_state.Language, _state.FilterMode),
            _state = _state.ReadingMode == "map" ? "map" : _state.HeadingMode == "manual" ? "manual" : aligned ? "aligned" : "sensor",
            isAligned = aligned,
            headingModes = WebCatalog.LocalizedOptions(WebCatalog.HeadingModes, _state.Language),
            readingModes = WebCatalog.LocalizedOptions(WebCatalog.QiblaReadingModes, _state.Language),
            filterModes = WebCatalog.LocalizedOptions(WebCatalog.QiblaFilterModes, _state.Language),
            labels = Labels()
        };
    }

    private object SetHeading(double heading) {
        _state.Heading = NormalizeDegrees(heading);
        return QiblaSnapshot();
    }

    private object SetHeadingMode(string? mode) {
        _state.HeadingMode = AppInputContract.RequiredChoice(mode, "qibla.headingMode", "auto", "manual");
        return QiblaSnapshot();
    }

    private object AdjustManualHeading(double delta) {
        _state.ManualHeading = NormalizeDegrees(_state.ManualHeading + delta);
        return QiblaSnapshot();
    }

    private object SetDisplayMode(string? mode) {
        _state.ReadingMode = AppInputContract.RequiredChoice(mode, "qibla.displayMode", "compass", "map");
        return QiblaSnapshot();
    }

    private object SetVisualFilter(string? mode) {
        _state.FilterMode = AppInputContract.RequiredChoice(mode, "qibla.visualFilter", "none", "night", "contrast");
        return QiblaSnapshot();
    }

    private object TasbihSnapshot() {
        var preset = _state.TasbihPresets.FirstOrDefault(item => item.Id == _state.SelectedTasbihPresetId) ?? _state.TasbihPresets[0];
        var corePreset = ToCorePreset(preset);
        var progress = _tasbihCalculator.BuildSnapshot(corePreset, _state.TasbihCount);
        var total = _tasbihCalculator.GetTotalTarget(corePreset);
        return new {
            count = _state.TasbihCount,
            currentPhrase = TranslateTasbihText(string.IsNullOrWhiteSpace(progress.CurrentText) ? preset.Items[0].Text : progress.CurrentText),
            progressText = $"{Math.Min(_state.TasbihCount, total)} / {total}",
            isPresetSelectionEnabled = _state.TasbihCount == 0,
            selectedPresetId = preset.Id,
            presets = _state.TasbihPresets.Select(item => new {
                id = item.Id,
                name = TranslateTasbihText(item.Name),
                repeatMode = NormalizeTasbihRepeatMode(item.RepeatMode),
                items = item.Items.Select(i => new {
                    text = TranslateTasbihText(i.Text),
                    targetCount = i.TargetCount
                }).ToArray()
            }).ToArray()
        };
    }

    private object IncrementTasbih() {
        var preset = _state.TasbihPresets.First(item => item.Id == _state.SelectedTasbihPresetId);
        _state.TasbihCount = _tasbihCalculator.GetNextCount(ToCorePreset(preset), _state.TasbihCount);
        return TasbihSnapshot();
    }

    private object ResetTasbih() {
        _state.TasbihCount = 0;
        return TasbihSnapshot();
    }

    private object AlarmSnapshot() => WebAlarmSnapshotFactory.Inactive(_state.Language);

    private object SelectTasbihPreset(string? id) {
        var preset = _state.TasbihPresets.FirstOrDefault(item => item.Id == id)
            ?? throw new ArgumentException($"Unknown Tasbih preset ID '{id ?? "<missing>"}'.", nameof(id));
        if (_state.TasbihCount != 0) throw new InvalidOperationException("Reset Tasbih before changing presets.");
        _state.SelectedTasbihPresetId = preset.Id;

        return TasbihSnapshot();
    }

    private object SettingsSnapshot(string? section) {
        return section switch {
            "locations" => new {
                useGps = _state.UseGps,
                latitude = _state.Latitude,
                longitude = _state.Longitude,
                country = _state.CountryCode,
                countryName = _state.Country,
                city = _state.City,
                vpnWarning = false,
                qiblaReadingMode = _state.ReadingMode,
                qiblaFilterMode = _state.FilterMode,
                qiblaReadingModes = WebCatalog.LocalizedOptions(WebCatalog.QiblaReadingModes, _state.Language),
                qiblaFilterModes = WebCatalog.LocalizedOptions(WebCatalog.QiblaFilterModes, _state.Language),
                countries = WebCatalog.Countries.Select(item => new { code = item.Code, name = item.Name, cities = item.Cities }).ToArray(),
                places = WebCatalog.Places.Select(item => new { country = item.Country, countryCode = item.CountryCode, city = item.City, latitude = item.Latitude, longitude = item.Longitude }).ToArray()
            },
            "theme" => new {
                language = _state.Language,
                themeMode = _state.ThemeMode,
                accentColor = _state.AccentColor,
                textSize = _state.TextSize,
                diagnostics = new { bridgeReady = true, lastSync = T("webCoreLastSync") },
                languages = WebCatalog.Languages.Select(item => new { code = item.Code, name = item.Name }).ToArray(),
                accentColors = WebCatalog.AccentColors
            },
            "adhan" when !string.IsNullOrWhiteSpace(_state.AdhanSettingsJson) => BuildStoredAdhanProjection(),
            "adhan" => new {
                calculationEngine = WebPrayerMonthFactory.EngineId,
                calculationEngines = new[] { new { id = WebPrayerMonthFactory.EngineId, label = T("calculationEngine_SharedCoreAdhan") } },
                calculationMethods = CalculationMethodPresetCatalog.SupportedMethods
                    .Select(method => new { id = method.ToString(), label = T($"method_{method}") }).ToArray(),
                madhhabs = Enum.GetValues<Madhhab>()
                    .Select(value => new { id = value.ToString(), label = T($"madhhab_{value}") }).ToArray(),
                highLatitudeRules = Enum.GetValues<HighLatitudeRule>()
                    .Select(value => new { id = value.ToString(), label = T($"highLatitude_{value}") }).ToArray(),
                clockFormats = new[] {
                    new { id = "auto", label = T("auto") },
                    new { id = "12h", label = T("clock12h") },
                    new { id = "24h", label = T("clock24h") }
                },
                defaults = WebCatalog.AdhanDefaults,
                sounds = WebCatalog.DefaultAdhanSounds.Select(item => new { id = item.Id, label = item.Label, selected = item.Selected, isCustom = item.IsCustom, canPreview = item.CanPreview }).ToArray(),
                volume = WebCatalog.AdhanDefaults.Volume,
                calculationMethod = WebCatalog.AdhanDefaults.CalculationMethod,
                madhhab = WebCatalog.AdhanDefaults.Madhhab,
                highLatitudeRule = WebCatalog.AdhanDefaults.HighLatitudeRule,
                fajrAngle = WebCatalog.AdhanDefaults.FajrAngle,
                ishaAngle = WebCatalog.AdhanDefaults.IshaAngle,
                isCustomMethod = false,
                offsets = new { fajr = 0, sunrise = 0, dhuhr = 0, asr = 0, maghrib = 0, isha = 0, imsak = 0 },
                clockFormat = _state.ClockFormat,
                fasting = new { iftarDelay = 0, imsakAdvance = 10 },
                imsakReminders = Array.Empty<object>(),
                iftarReminders = Array.Empty<object>(),
                vibrationOverrideOptions = new[] {
                    new { id = "default", label = T("useGlobal") },
                    new { id = "enabled", label = T("PermissionStatus_Enabled") },
                    new { id = "none", label = T("PermissionStatus_Disabled") }
                },
                perPrayerOverrides = Array.Empty<object>()
            },
            "notifications" when !string.IsNullOrWhiteSpace(_state.NotificationSettingsJson) => ParseStoredProjection(_state.NotificationSettingsJson),
            "notifications" => new {
                enableAdhan = WebCatalog.NotificationDefaults.EnableAdhan,
                mobilePrimaryAdhanType = WebCatalog.NotificationDefaults.MobilePrimaryAdhanType,
                hideOnCloseWindows = WebCatalog.NotificationDefaults.HideOnCloseWindows,
                runBackgroundServiceWindows = WebCatalog.NotificationDefaults.RunBackgroundServiceWindows,
                vibration = WebCatalog.NotificationDefaults.Vibration,
                vibrationStrength = WebCatalog.NotificationDefaults.VibrationStrength,
                vibrationPattern = WebCatalog.NotificationDefaults.VibrationPattern,
                minutesBefore = WebCatalog.NotificationDefaults.MinutesBefore,
                reminderScope = "All",
                reminderPrayer = "Fajr",
                reminderScopes = new[] {
                    new { id = "All", label = T("reminder_All") },
                    new { id = "SpecificPrayer", label = T("Reminder_Specific") }
                },
                reminderPrayers = new[] { "Fajr", "Dhuhr", "Asr", "Maghrib", "Isha" }
                    .Select(id => new { id, label = T($"prayer_{id}") }).ToArray(),
                reminderAlertTypes = new[] { "Adhan", "Notification", "Silent", "Alarm" }
                    .Select(id => new { id, label = T($"reminderType_{id}") }).ToArray(),
                reminderUnits = new[] { new { id = "minute", label = T("minutes") }, new { id = "hour", label = T("hours") } },
                reminderDirections = new[] { new { id = "before", label = T("before") }, new { id = "after", label = T("after") } },
                reminders = Array.Empty<object>(),
                pendingDeferredReminder = (object?)null
            },
            "permissions" => PermissionsSnapshot(),
            "alarmReminders" when !string.IsNullOrWhiteSpace(_state.AlarmRemindersSettingsJson) => ParseStoredProjection(_state.AlarmRemindersSettingsJson),
            "alarmReminders" => new {
                builtIn = WebCatalog.BuiltInAlarmReminders.Select(item => new { id = item.Id, text = item.Text, enabled = item.Enabled }).ToArray(),
                userRemindersEnabled = true,
                userReminders = Array.Empty<object>()
            },
            "about" => AboutSnapshot(),
            null or "" => new { locations = SettingsSnapshot("locations"), theme = SettingsSnapshot("theme"), adhan = SettingsSnapshot("adhan") },
            _ => throw new ArgumentException($"Unknown settings section '{section}'.", nameof(section))
        };
    }

    private object SetSettingsField(JsonElement payload) {
        var section = GetString(payload, "section", "");
        var field = GetString(payload, "field", "");
        var value = payload.TryGetProperty("value", out var valueElement) ? valueElement : default;

        if (section == "theme" && field == "language") {
            _state.Language = WebCatalog.NormalizeLanguage(RequiredStringValue(value, "value"));
            return new { ok = true, section, field, value = _state.Language, languageObject = LanguageObject(_state.Language) };
        }

        if (section == "theme" && field == "themeMode") {
            _state.ThemeMode = WebCatalog.NormalizeTheme(RequiredStringValue(value, "value"));
        } else if (section == "theme" && field == "accentColor") {
            _state.AccentColor = WebCatalog.NormalizeAccent(RequiredStringValue(value, "value"));
        } else if (section == "theme" && field == "textSize") {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var textSize)) {
                throw new ArgumentException("Theme textSize requires an integer value.", "value");
            }
            _state.TextSize = WebCatalog.ClampTextSize(textSize);
        } else if (section == "locations" && field == "value" && value.ValueKind == JsonValueKind.Object) {
            var previousCountryCode = _state.CountryCode;
            var previousCountry = _state.Country;
            var previousCity = _state.City;
            var previousLatitude = _state.Latitude;
            var previousLongitude = _state.Longitude;
            _state.UseGps = GetBool(value, "useGps", _state.UseGps);
            _state.Latitude = GetDouble(value, "latitude", _state.Latitude);
            _state.Longitude = GetDouble(value, "longitude", _state.Longitude);
            _state.ReadingMode = GetString(value, "qiblaReadingMode", _state.ReadingMode) ?? _state.ReadingMode;
            _state.FilterMode = GetString(value, "qiblaFilterMode", _state.FilterMode) ?? _state.FilterMode;

            var coordinatesChanged = Math.Abs(previousLatitude - _state.Latitude) > 0.000001 ||
                Math.Abs(previousLongitude - _state.Longitude) > 0.000001;
            var incomingCountryCode = CleanLocationText(GetString(value, "country", coordinatesChanged ? string.Empty : _state.CountryCode));
            var incomingCountry = CleanLocationText(GetString(value, "countryName", coordinatesChanged ? string.Empty : _state.Country));
            var incomingCity = CleanLocationText(GetString(value, "city", coordinatesChanged ? string.Empty : _state.City));

            if (coordinatesChanged) {
                var place = WebCatalog.FindNearestPlace(_state.Latitude, _state.Longitude, 50);
                if (place is not null) {
                    _state.Country = place.Country;
                    _state.CountryCode = place.CountryCode;
                    _state.City = place.City;
                } else if (IncomingLocationDiffersFromPrevious(incomingCountryCode, incomingCountry, incomingCity, previousCountryCode, previousCountry, previousCity)) {
                    _state.CountryCode = incomingCountryCode;
                    _state.Country = incomingCountry;
                    _state.City = incomingCity;
                } else {
                    _state.CountryCode = string.Empty;
                    _state.Country = string.Empty;
                    _state.City = string.Empty;
                }
            } else {
                _state.CountryCode = incomingCountryCode;
                _state.Country = incomingCountry;
                _state.City = incomingCity;
            }
        } else if (section == "adhan" && field == "value" && value.ValueKind == JsonValueKind.Object) {
            var candidate = value.GetRawText();
            var previous = _state.AdhanSettingsJson;
            _state.AdhanSettingsJson = candidate;
            try {
                _ = ReadStoredAdhanSettings();
            } finally {
                _state.AdhanSettingsJson = previous;
            }
            _state.ClockFormat = GetString(value, "clockFormat", _state.ClockFormat) ?? _state.ClockFormat;
            _state.AdhanSettingsJson = candidate;
        } else if (section == "notifications" && field == "value" && value.ValueKind == JsonValueKind.Object) {
            _state.NotificationSettingsJson = value.GetRawText();
        } else if (section == "alarmReminders" && field == "value" && value.ValueKind == JsonValueKind.Object) {
            _state.AlarmRemindersSettingsJson = value.GetRawText();
        } else {
            throw new ArgumentException($"Unsupported settings patch '{section}.{field}' or invalid value shape.");
        }

        return new {
            ok = true,
            section,
            field,
            value = CloneJsonValue(value),
            calculated = section == "locations" ? SettingsSnapshot("locations") : null,
            projection = SettingsSnapshot(section)
        };
    }

    private object NativeAction(string action, bool ok) {
        var messageKey = WebCatalog.NativeActionMessageKey(action);
        return new { ok, action, platform = "web", message = T(messageKey), messageKey };
    }

    private object BrowserUnavailable(string error) => new {
        status = "notAvailable",
        version = "browser",
        lastPulledVersion = "browser",
        error
    };

    private object AddTasbihPreset(string? name) {
        var normalizedName = string.IsNullOrWhiteSpace(name) ? T("newPresetName") : name.Trim();
        var idBase = Slug(normalizedName);
        var id = idBase;
        var suffix = 2;
        while (_state.TasbihPresets.Any(item => item.Id == id)) {
            id = $"{idBase}-{suffix++}";
        }

        _state.TasbihPresets.Add(new WebTasbihPreset(id, normalizedName, WebStateDefaults.DefaultTasbihRepeatMode, [new WebTasbihItem(WebStateDefaults.DefaultTasbihItemText, WebStateDefaults.DefaultTasbihTargetCount)]));
        _state.SelectedTasbihPresetId = id;
        _state.TasbihCount = 0;
        return TasbihSnapshot();
    }

    private object UpdateTasbihPreset(JsonElement payload) {
        var id = GetString(payload, "id", null);
        var preset = _state.TasbihPresets.FirstOrDefault(item => item.Id == id);
        if (preset is null) throw new ArgumentException($"Unknown Tasbih preset ID '{id ?? "<missing>"}'.");

        var name = GetString(payload, "name", preset.Name) ?? preset.Name;
        var repeatMode = NormalizeTasbihRepeatMode(GetString(payload, "repeatMode", preset.RepeatMode));
        ReplaceTasbihPreset(preset, preset with { Name = string.IsNullOrWhiteSpace(name) ? preset.Name : name.Trim(), RepeatMode = repeatMode });
        return TasbihSnapshot();
    }

    private object RemoveTasbihPreset(JsonElement payload) {
        var id = GetString(payload, "id", null);
        var preset = _state.TasbihPresets.FirstOrDefault(item => item.Id == id);
        if (preset is null) throw new ArgumentException($"Unknown Tasbih preset ID '{id ?? "<missing>"}'.");
        if (_state.TasbihPresets.Count <= 1) throw new InvalidOperationException("The last Tasbih preset cannot be removed.");

        _state.TasbihPresets.Remove(preset);
        if (_state.SelectedTasbihPresetId == id) {
            _state.SelectedTasbihPresetId = _state.TasbihPresets[0].Id;
        }
        _state.TasbihCount = 0;
        return TasbihSnapshot();
    }

    private object AddTasbihItem(JsonElement payload) {
        var preset = FindTasbihPreset(payload);
        if (preset is null) throw new ArgumentException("Unknown Tasbih preset ID.");

        var text = GetString(payload, "text", null);
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Tasbih item text is required.", "text");
        var targetCount = GetInt(payload, "targetCount", WebStateDefaults.DefaultTasbihTargetCount);
        if (targetCount <= 0) throw new ArgumentOutOfRangeException("targetCount", targetCount, "Tasbih target must be positive.");
        preset.Items.Add(new WebTasbihItem(text.Trim(), targetCount));
        _state.TasbihCount = 0;
        return TasbihSnapshot();
    }

    private object UpdateTasbihItem(JsonElement payload) {
        var preset = FindTasbihPreset(payload);
        var index = GetInt(payload, "index", -1);
        if (preset is null) throw new ArgumentException("Unknown Tasbih preset ID.");
        if (index < 0 || index >= preset.Items.Count) throw new ArgumentOutOfRangeException(nameof(index), index, "Unknown Tasbih item index.");

        var item = preset.Items[index];
        var text = GetString(payload, "text", item.Text) ?? item.Text;
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Tasbih item text cannot be empty.", "text");
        var targetCount = GetInt(payload, "targetCount", item.TargetCount);
        if (targetCount <= 0) throw new ArgumentOutOfRangeException("targetCount", targetCount, "Tasbih target must be positive.");
        preset.Items[index] = item with { Text = text.Trim(), TargetCount = targetCount };
        _state.TasbihCount = 0;
        return TasbihSnapshot();
    }

    private object MoveTasbihItem(JsonElement payload) {
        var preset = FindTasbihPreset(payload);
        var index = GetInt(payload, "index", -1);
        var direction = GetString(payload, "direction", "");
        if (preset is null) throw new ArgumentException("Unknown Tasbih preset ID.");
        if (index < 0 || index >= preset.Items.Count) throw new ArgumentOutOfRangeException(nameof(index), index, "Unknown Tasbih item index.");
        if (direction is not ("up" or "down")) throw new ArgumentException($"Unknown Tasbih move direction '{direction}'.");

        var target = direction == "up" ? index - 1 : direction == "down" ? index + 1 : index;
        if (target < 0 || target >= preset.Items.Count || target == index) {
            throw new InvalidOperationException("Tasbih item cannot move beyond the collection boundary.");
        }

        (preset.Items[index], preset.Items[target]) = (preset.Items[target], preset.Items[index]);
        _state.TasbihCount = 0;
        return TasbihSnapshot();
    }

    private object RemoveTasbihItem(JsonElement payload) {
        var preset = FindTasbihPreset(payload);
        var index = GetInt(payload, "index", -1);
        if (preset is null) throw new ArgumentException("Unknown Tasbih preset ID.");
        if (preset.Items.Count <= 1) throw new InvalidOperationException("The last Tasbih item cannot be removed.");
        if (index < 0 || index >= preset.Items.Count) throw new ArgumentOutOfRangeException(nameof(index), index, "Unknown Tasbih item index.");
        preset.Items.RemoveAt(index);
        _state.TasbihCount = 0;

        return TasbihSnapshot();
    }

    private WebTasbihPreset? FindTasbihPreset(JsonElement payload) {
        var presetId = GetString(payload, "presetId", _state.SelectedTasbihPresetId);
        return _state.TasbihPresets.FirstOrDefault(item => item.Id == presetId);
    }

    private void ReplaceTasbihPreset(WebTasbihPreset original, WebTasbihPreset replacement) {
        var index = _state.TasbihPresets.IndexOf(original);
        if (index >= 0) {
            _state.TasbihPresets[index] = replacement;
        }
    }

    private object RemoteUrlSnapshot() => new {
        url = _state.RemoteWebUrl,
        manifestUrl = BuildManifestUrl(_state.RemoteWebUrl),
        lastPulledVersion = "browser"
    };

    private object AboutSnapshot() => new {
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
        remoteWebUrl = _state.RemoteWebUrl,
        defaultRemoteWebUrl = WebCatalog.AboutInfo.RemoteWebUrl
    };

    private object SetRemoteUrl(string? url) {
        _state.RemoteWebUrl = NormalizeRemoteUrl(url);
        return RemoteUrlSnapshot();
    }

    private object OnboardingSnapshot() => new {
        language = _state.Language,
        isRtl = WebCatalog.IsRtl(_state.Language),
        permissions = PermissionItems(),
        labels = Labels(),
        location = SettingsSnapshot("locations"),
        completed = _state.OnboardingCompleted
    };

    private object PermissionsSnapshot() => new {
        alarmMode = new { title = T("webExactAlarms"), status = T("webExactAlarmsUnavailable"), description = T("webExactAlarmsDescription") },
        items = PermissionItems()
    };

    private object PermissionItems() => WebCatalog.BrowserPermissionItems
        .Select(item => new { id = item.Id, isGranted = false, title = item.Title, role = item.Role, description = item.Description, fallback = item.Fallback, status = item.Status, action = item.Action })
        .ToArray();

    private object CompleteOnboarding() {
        _state.OnboardingCompleted = true;
        return new { ok = true };
    }

    private AppSettings BuildSettings() {
        var adhan = ReadStoredAdhanSettings();
        return new AppSettings {
            Location = new LocationSettings {
                Mode = _state.UseGps ? LocationMode.Gps : LocationMode.Manual,
                City = _state.City,
                Country = _state.Country,
                CountryCode = _state.CountryCode,
                Latitude = _state.Latitude,
                Longitude = _state.Longitude,
                TimeZoneId = _state.TimeZoneId
            },
            Method = adhan.Method,
            Madhhab = adhan.Madhhab,
            HighLatitudeRule = adhan.HighLatitudeRule,
            SunAngles = adhan.SunAngles,
            Offsets = adhan.Offsets,
            FastingOffsets = adhan.FastingOffsets,
            ClockFormat = _state.ClockFormat == "24h" ? ClockFormat.TwentyFourHour : _state.ClockFormat == "12h" ? ClockFormat.TwelveHour : ClockFormat.Auto,
            Language = _state.Language,
            LanguageSelected = true,
            ThemeMode = _state.ThemeMode switch { "light" => ThemeMode.Light, "dark" => ThemeMode.Dark, _ => ThemeMode.Auto },
            TextScale = _state.TextSize,
            OnboardingCompleted = _state.OnboardingCompleted
        };
    }

    private (CalculationMethod Method, Madhhab Madhhab, HighLatitudeRule HighLatitudeRule, SunAngleSettings SunAngles, PrayerOffsets Offsets, FastingOffsets FastingOffsets) ReadStoredAdhanSettings() {
        var defaults = (
            Method: CalculationMethod.Auto,
            Madhhab: Madhhab.Shafi,
            HighLatitudeRule: HighLatitudeRule.MiddleOfTheNight,
            SunAngles: new SunAngleSettings(),
            Offsets: PrayerOffsets.Default,
            FastingOffsets: new FastingOffsets { ImsakAdvanceMinutes = 10 });
        if (string.IsNullOrWhiteSpace(_state.AdhanSettingsJson)) return defaults;

        try {
            using var document = JsonDocument.Parse(_state.AdhanSettingsJson);
            var root = document.RootElement;
            var method = ParseEnumValue(GetString(root, "calculationMethod", null), defaults.Method);
            var madhhab = ParseEnumValue(GetString(root, "madhhab", null), defaults.Madhhab);
            var highLatitude = ParseEnumValue(GetString(root, "highLatitudeRule", null), defaults.HighLatitudeRule);
            var offsets = root.TryGetProperty("offsets", out var offsetValue) && offsetValue.ValueKind == JsonValueKind.Object
                ? new PrayerOffsets {
                    Fajr = (int)GetDouble(offsetValue, "fajr", 0),
                    Sunrise = (int)GetDouble(offsetValue, "sunrise", 0),
                    Dhuhr = (int)GetDouble(offsetValue, "dhuhr", 0),
                    Asr = (int)GetDouble(offsetValue, "asr", 0),
                    Maghrib = (int)GetDouble(offsetValue, "maghrib", 0),
                    Isha = (int)GetDouble(offsetValue, "isha", 0),
                    Imsak = (int)GetDouble(offsetValue, "imsak", 0)
                }
                : defaults.Offsets;
            var fasting = root.TryGetProperty("fasting", out var fastingValue) && fastingValue.ValueKind == JsonValueKind.Object
                ? new FastingOffsets {
                    IftarDelayMinutes = (int)GetDouble(fastingValue, "iftarDelay", 0),
                    ImsakAdvanceMinutes = (int)GetDouble(fastingValue, "imsakAdvance", 10)
                }
                : defaults.FastingOffsets;
            return (
                method,
                madhhab,
                highLatitude,
                new SunAngleSettings {
                    Fajr = GetDouble(root, "fajrAngle", 18),
                    Isha = GetDouble(root, "ishaAngle", 17)
                },
                offsets,
                fasting);
        } catch (JsonException exception) {
            throw new InvalidDataException(
                "Saved Adhan settings are corrupt. Prayer times were not recalculated with defaults.", exception);
        }
    }

    private static TEnum ParseEnumValue<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return Enum.TryParse<TEnum>(value, true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new InvalidDataException($"Saved Adhan setting '{value}' is not a valid {typeof(TEnum).Name}.");
    }

    private TasbihPresetSettings ToCorePreset(WebTasbihPreset preset) {
        return new TasbihPresetSettings {
            Name = preset.Name,
            RepeatMode = preset.RepeatMode switch {
                "Loop" or "Continue" => TasbihRepeatMode.RepeatContinue,
                "Sequence" or "Reset" => TasbihRepeatMode.RepeatReset,
                "None" => TasbihRepeatMode.None,
                _ => throw new InvalidDataException($"Persisted Tasbih repeat mode '{preset.RepeatMode}' is invalid.")
            },
            Items = preset.Items.Select(item => new TasbihItemSettings { Text = item.Text, TargetCount = item.TargetCount }).ToList()
        };
    }

    private string Format(DateTime value, AppSettings settings) {
        return settings.ClockFormat switch {
            ClockFormat.TwelveHour => value.ToString("h:mm tt", CultureInfo.InvariantCulture),
            _ => value.ToString("HH:mm", CultureInfo.InvariantCulture)
        };
    }

    private string FormatLiveClock(DateTime value, AppSettings settings) {
        return settings.ClockFormat switch {
            ClockFormat.TwelveHour => value.ToString("h:mm:ss tt", CultureInfo.InvariantCulture),
            _ => value.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
        };
    }

    private string FormatDuration(TimeSpan value) {
        if (value < TimeSpan.Zero) {
            value = TimeSpan.Zero;
        }

        return $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
    }

    private string PrayerLabel(PrayerId id) => id switch {
        PrayerId.Fajr => T("fajr"),
        PrayerId.Sunrise => T("sunrise"),
        PrayerId.Dhuhr => T("dhuhr"),
        PrayerId.Asr => T("asr"),
        PrayerId.Maghrib => T("maghrib"),
        PrayerId.Isha => T("isha"),
        PrayerId.Imsak => T("imsak"),
        _ => id.ToString()
    };

    private string LocationTitle() {
        if (!string.IsNullOrWhiteSpace(_state.City) && !string.IsNullOrWhiteSpace(_state.Country)) return $"{_state.City}, {_state.Country}";
        if (!string.IsNullOrWhiteSpace(_state.City)) return _state.City;
        if (!string.IsNullOrWhiteSpace(_state.Country)) return _state.Country;
        return $"{_state.Latitude.ToString("0.####", CultureInfo.InvariantCulture)}, {_state.Longitude.ToString("0.####", CultureInfo.InvariantCulture)}";
    }

    private static string CleanLocationText(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static bool IncomingLocationDiffersFromPrevious(
        string countryCode,
        string country,
        string city,
        string previousCountryCode,
        string previousCountry,
        string previousCity) {
        if (string.IsNullOrWhiteSpace(countryCode) && string.IsNullOrWhiteSpace(country) && string.IsNullOrWhiteSpace(city)) return false;
        return !string.Equals(countryCode, previousCountryCode, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(country, previousCountry, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(city, previousCity, StringComparison.OrdinalIgnoreCase);
    }

    private string DirectionLabel(double bearing) {
        string[] labels = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];
        return labels[(int)Math.Round(NormalizeDegrees(bearing) / 45d) % labels.Length];
    }

    private double NormalizeDegrees(double value) {
        var normalized = value % 360d;
        return normalized < 0 ? normalized + 360d : normalized;
    }

    private string? GetString(JsonElement payload, string property, string? fallback) {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty(property, out var value)) return fallback;
        if (value.ValueKind == JsonValueKind.String) return value.GetString();
        if (value.ValueKind == JsonValueKind.Null) return fallback;
        throw new ArgumentException($"Invalid '{property}': expected a string.", property);
    }

    private double GetDouble(JsonElement payload, string property, double fallback) {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty(property, out var value)) return fallback;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number)) return number;
        throw new ArgumentException($"Invalid '{property}': expected a finite number.", property);
    }

    private bool GetBool(JsonElement payload, string property, bool fallback) {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty(property, out var value)) return fallback;
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False) return value.GetBoolean();
        throw new ArgumentException($"Invalid '{property}': expected a boolean.", property);
    }

    private int GetInt(JsonElement payload, string property, int fallback) {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty(property, out var value)) return fallback;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        throw new ArgumentException($"Invalid '{property}': expected an integer.", property);
    }

    private static string RequiredStringValue(JsonElement value, string field) =>
        value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new ArgumentException($"Invalid '{field}': expected a non-empty string.", field);

    private string NormalizeTasbihRepeatMode(string? mode) => mode switch {
        "Continue" or "Loop" => "Continue",
        "Reset" or "Sequence" => "Reset",
        "None" => "None",
        _ => throw new ArgumentException($"Unknown Tasbih repeat mode '{mode ?? "<missing>"}'.")
    };

    private string Slug(string value) {
        var chars = value.ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var slug = string.Join("-", new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(slug) ? "preset" : slug;
    }

    private string NormalizeRemoteUrl(string? value) {
        var candidate = string.IsNullOrWhiteSpace(value) ? WebStateDefaults.DefaultRemoteWebUrl : value.Trim();
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) {
            throw new InvalidOperationException("Remote web URL must be an http or https URL.");
        }

        if (uri.AbsolutePath.EndsWith("web.manifest.json", StringComparison.OrdinalIgnoreCase)) {
            var builder = new UriBuilder(uri) { Path = uri.AbsolutePath[..^"web.manifest.json".Length], Query = "" };
            uri = builder.Uri;
        }

        var text = uri.GetLeftPart(UriPartial.Path);
        return text.EndsWith("/", StringComparison.Ordinal) ? text : text + "/";
    }

    private string BuildManifestUrl(string baseUrl) => new Uri(new Uri(baseUrl), "web.manifest.json").ToString();

    private object Labels() => WebCatalog.Labels(_state.Language);

    private string T(string key) {
        return WebCatalog.Translate(_state.Language, key);
    }

    private string TranslateTasbihText(string value) {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var key = value.Trim();
        return WebCatalog.Labels(_state.Language).TryGetValue(key, out var translated) ? translated : value;
    }

    private static object? CloneJsonValue(JsonElement value) =>
        value.ValueKind == JsonValueKind.Undefined ? null : value.Clone();

    private static JsonElement ParseStoredProjection(string json) {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private JsonElement BuildStoredAdhanProjection() {
        var projection = JsonNode.Parse(_state.AdhanSettingsJson) as JsonObject ?? new JsonObject();
        projection["calculationEngine"] = WebPrayerMonthFactory.EngineId;
        projection["calculationEngines"] = new JsonArray(new JsonObject {
            ["id"] = WebPrayerMonthFactory.EngineId,
            ["label"] = T("calculationEngine_SharedCoreAdhan")
        });
        projection["calculationMethods"] = new JsonArray(CalculationMethodPresetCatalog.SupportedMethods
            .Select(method => (JsonNode)new JsonObject {
                ["id"] = method.ToString(),
                ["label"] = T($"method_{method}")
            }).ToArray());
        projection["madhhabs"] = new JsonArray(Enum.GetValues<Madhhab>()
            .Select(value => (JsonNode)new JsonObject {
                ["id"] = value.ToString(),
                ["label"] = T($"madhhab_{value}")
            }).ToArray());
        projection["highLatitudeRules"] = new JsonArray(Enum.GetValues<HighLatitudeRule>()
            .Select(value => (JsonNode)new JsonObject {
                ["id"] = value.ToString(),
                ["label"] = T($"highLatitude_{value}")
            }).ToArray());
        projection["clockFormats"] = new JsonArray(
            new JsonObject { ["id"] = "auto", ["label"] = T("auto") },
            new JsonObject { ["id"] = "12h", ["label"] = T("clock12h") },
            new JsonObject { ["id"] = "24h", ["label"] = T("clock24h") });
        projection["vibrationOverrideOptions"] = new JsonArray(
            new JsonObject { ["id"] = "default", ["label"] = T("useGlobal") },
            new JsonObject { ["id"] = "enabled", ["label"] = T("PermissionStatus_Enabled") },
            new JsonObject { ["id"] = "none", ["label"] = T("PermissionStatus_Disabled") });
        if (projection["sounds"] is not JsonArray sounds || sounds.Count == 0) {
            projection["sounds"] = new JsonArray(new JsonObject {
                ["id"] = "adhan_default",
                ["label"] = "Default",
                ["selected"] = true,
                ["isCustom"] = false,
                ["canPreview"] = true
            });
        } else {
            var hasSelected = false;
            foreach (var item in sounds.OfType<JsonObject>()) {
                var isCustom = item["isCustom"]?.GetValue<bool>() ?? false;
                if (!isCustom) item["canPreview"] = true;
                hasSelected |= item["selected"]?.GetValue<bool>() ?? false;
            }
            if (!hasSelected && sounds[0] is JsonObject first) first["selected"] = true;
        }
        return ParseStoredProjection(projection.ToJsonString());
    }

}
