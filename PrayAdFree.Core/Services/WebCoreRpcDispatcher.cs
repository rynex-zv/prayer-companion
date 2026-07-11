using System.Globalization;
using System.Text.Json;
using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public sealed class WebCoreRpcDispatcher {
    private readonly DailyPrayerSnapshotFactory _dailyFactory = new();
    private readonly CalendarMonthPresenter _calendarPresenter = new();
    private readonly TasbihProgressCalculator _tasbihCalculator = new();
    private readonly WebPrayerMonthFactory _prayerMonthFactory = new();
    private readonly IslamicOccasionCatalog _occasions = new();
    private WebState _state = WebState.Default();

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
        return method switch {
            "app.getShellSnapshot" => ShellSnapshot(),
            "app.getLocalization" => Labels(),
            "app.getLanguageObject" => LanguageObject(GetString(payload, "language", _state.Language)),
            "app.setLanguage" => SetLanguage(GetString(payload, "language", _state.Language)),
            "app.setTheme" => SetTheme(GetString(payload, "theme", _state.ThemeMode)),
            "app.navigate" => new { navigatedTo = GetString(payload, "route", "/") },
            "app.importState" => ImportState(GetString(payload, "state", GetString(payload, "_state", ""))),
            "app.exportState" => ExportState(),

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

            "alarm.getSnapshot" or "alarm.snooze" or "alarm.stop" => AlarmSnapshot(),

            "settings.getSnapshot" => SettingsSnapshot(GetString(payload, "section", "")),
            "settings.setField" => SetSettingsField(payload),
            "settings.patch" => new { ok = true },
            "settings.invoke" => InvokeSetting(GetString(payload, "action", "") ?? "", payload.TryGetProperty("payload", out var actionPayload) ? actionPayload : default),

            "onboarding.getSnapshot" => OnboardingSnapshot(),
            "onboarding.complete" => CompleteOnboarding(),
            "mauiWebber.getRemoteUrl" => RemoteUrlSnapshot(),
            "mauiWebber.setRemoteUrl" => SetRemoteUrl(GetString(payload, "url", _state.RemoteWebUrl)),
            "mauiWebber.pullRemote" => BrowserUnavailable(T("webRemotePullUnavailable")),
            "mauiWebber.useEmbedded" => BrowserUnavailable(T("webEmbeddedResetUnavailable")),
            _ => throw new InvalidOperationException($"No web core handler for \"{method}\".")
        };
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
        _state.Language = WebCatalog.NormalizeLanguage(language);
        return new {
            code = _state.Language,
            direction = WebCatalog.IsRtl(_state.Language) ? "rtl" : "ltr",
            labels = Labels(),
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
        var now = DateTime.Now;
        var settings = BuildSettings();
        var day = _prayerMonthFactory.BuildDay(settings, DateOnly.FromDateTime(now));
        var snapshot = _dailyFactory.Build(day, settings, now);
        var next = snapshot.NextPrayerTime;

        return new {
            locationTitle = LocationTitle(),
            hijriDate = day.Hijri.Date,
            gregorianDate = now.ToString("dddd, dd MMMM yyyy", CultureInfo.InvariantCulture),
            currentTime = FormatLiveClock(now, settings),
            nextPrayerId = snapshot.NextPrayerId.ToString(),
            nextPrayerClock = Format(next, settings),
            nextPrayerBaseClock = snapshot.NextPrayerBaseTime.HasValue ? Format(snapshot.NextPrayerBaseTime.Value, settings) : Format(next, settings),
            showNextPrayerBaseClock = snapshot.NextPrayerBaseTime.HasValue,
            nextPrayerDayId = snapshot.IsNextPrayerTomorrow ? "tomorrow" : "today",
            countdown = FormatDuration(next - now),
            statusMessage = "",
            imsakTime = Format(day.Timings.Imsak, settings),
            iftarTime = Format(day.Timings.Maghrib, settings),
            isImsakNext = snapshot.NextPrayerId == PrayerId.Imsak,
            isIftarNext = snapshot.NextPrayerId == PrayerId.Maghrib,
            nextFastingCountdown = FormatDuration(day.Timings.Maghrib - now),
            isRtl = WebCatalog.IsRtl(_state.Language),
            labels = Labels(),
            todayTimings = snapshot.Entries.Select(entry => new {
                id = entry.Prayer.ToString().ToLowerInvariant(),
                time = Format(entry.AdjustedTime, settings),
                baseTime = Format(entry.BaseTime, settings),
                isNext = entry.IsNext
            }).ToArray()
        };
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
        _state.HeadingMode = mode == "manual" ? "manual" : "auto";
        return QiblaSnapshot();
    }

    private object AdjustManualHeading(double delta) {
        _state.ManualHeading = NormalizeDegrees(_state.ManualHeading + delta);
        return QiblaSnapshot();
    }

    private object SetDisplayMode(string? mode) {
        _state.ReadingMode = mode == "map" ? "map" : "compass";
        return QiblaSnapshot();
    }

    private object SetVisualFilter(string? mode) {
        _state.FilterMode = mode is "night" or "contrast" ? mode : "none";
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
                repeatMode = item.RepeatMode,
                items = item.Items.Select(i => new { Text = TranslateTasbihText(i.Text), i.TargetCount }).ToArray()
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
        if (_state.TasbihCount == 0 && _state.TasbihPresets.Any(item => item.Id == id)) {
            _state.SelectedTasbihPresetId = id!;
        }

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
            "adhan" => new {
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
                perPrayerOverrides = Array.Empty<object>()
            },
            "notifications" => new {
                enableAdhan = WebCatalog.NotificationDefaults.EnableAdhan,
                mobilePrimaryAdhanType = WebCatalog.NotificationDefaults.MobilePrimaryAdhanType,
                hideOnCloseWindows = WebCatalog.NotificationDefaults.HideOnCloseWindows,
                runBackgroundServiceWindows = WebCatalog.NotificationDefaults.RunBackgroundServiceWindows,
                vibration = WebCatalog.NotificationDefaults.Vibration,
                vibrationStrength = WebCatalog.NotificationDefaults.VibrationStrength,
                vibrationPattern = WebCatalog.NotificationDefaults.VibrationPattern,
                minutesBefore = WebCatalog.NotificationDefaults.MinutesBefore,
                reminders = Array.Empty<object>(),
                pendingDeferredReminder = (object?)null
            },
            "permissions" => PermissionsSnapshot(),
            "alarmReminders" => new {
                builtIn = WebCatalog.BuiltInAlarmReminders.Select(item => new { id = item.Id, text = item.Text, enabled = item.Enabled }).ToArray(),
                userRemindersEnabled = true,
                userReminders = Array.Empty<object>()
            },
            "about" => AboutSnapshot(),
            _ => new { locations = SettingsSnapshot("locations"), theme = SettingsSnapshot("theme"), adhan = SettingsSnapshot("adhan") }
        };
    }

    private object SetSettingsField(JsonElement payload) {
        var section = GetString(payload, "section", "");
        var field = GetString(payload, "field", "");
        var value = payload.TryGetProperty("value", out var valueElement) ? valueElement : default;

        if (section == "theme" && field == "language") {
            _state.Language = WebCatalog.NormalizeLanguage(value.ValueKind == JsonValueKind.String ? value.GetString() : null);
            return new { ok = true, section, field, value = _state.Language, languageObject = LanguageObject(_state.Language) };
        }

        if (section == "theme" && field == "themeMode") {
            _state.ThemeMode = WebCatalog.NormalizeTheme(value.ValueKind == JsonValueKind.String ? value.GetString() : null);
        } else if (section == "theme" && field == "accentColor") {
            _state.AccentColor = WebCatalog.NormalizeAccent(value.ValueKind == JsonValueKind.String ? value.GetString() : null);
        } else if (section == "theme" && field == "textSize") {
            _state.TextSize = WebCatalog.ClampTextSize(value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var textSize) ? textSize : _state.TextSize);
        } else if (section == "locations" && field == "value" && value.ValueKind == JsonValueKind.Object) {
            var previousLatitude = _state.Latitude;
            var previousLongitude = _state.Longitude;
            _state.UseGps = GetBool(value, "useGps", _state.UseGps);
            _state.Latitude = GetDouble(value, "latitude", _state.Latitude);
            _state.Longitude = GetDouble(value, "longitude", _state.Longitude);
            _state.CountryCode = GetString(value, "country", _state.CountryCode) ?? _state.CountryCode;
            _state.Country = GetString(value, "countryName", _state.Country) ?? _state.Country;
            _state.City = GetString(value, "city", _state.City) ?? _state.City;
            _state.ReadingMode = GetString(value, "qiblaReadingMode", _state.ReadingMode) ?? _state.ReadingMode;
            _state.FilterMode = GetString(value, "qiblaFilterMode", _state.FilterMode) ?? _state.FilterMode;
            if (Math.Abs(previousLatitude - _state.Latitude) > 0.000001 ||
                Math.Abs(previousLongitude - _state.Longitude) > 0.000001) {
                var place = WebCatalog.FindNearestPlace(_state.Latitude, _state.Longitude, 50);
                _state.Country = place?.Country ?? string.Empty;
                _state.CountryCode = place?.CountryCode ?? string.Empty;
                _state.City = place?.City ?? string.Empty;
            }
        } else if (section == "adhan" && field == "value" && value.ValueKind == JsonValueKind.Object) {
            _state.ClockFormat = GetString(value, "clockFormat", _state.ClockFormat) ?? _state.ClockFormat;
        }

        return new {
            ok = true,
            section,
            field,
            value = CloneJsonValue(value),
            calculated = section == "locations" ? SettingsSnapshot("locations") : null
        };
    }

    private object InvokeSetting(string action, JsonElement payload) {
        return action switch {
            "requestPermission" => NativeAction(action, ok: true),
            "requestAllPermissions" => NativeAction(action, ok: true),
            "refreshGps" => NativeAction(action, ok: false),
            "openUrl" or "openEmail" or "call" => new { ok = true, action, platform = "web", intent = CloneJsonValue(payload) },
            "addTasbihPreset" => AddTasbihPreset(GetString(payload, "name", T("newPresetName"))),
            "updateTasbihPreset" => UpdateTasbihPreset(payload),
            "addTasbihItem" => AddTasbihItem(payload),
            "updateTasbihItem" => UpdateTasbihItem(payload),
            "moveTasbihItem" => MoveTasbihItem(payload),
            "removeTasbihItem" => RemoveTasbihItem(payload),
            "addCustomAdhanSound" or "testNotification" or "previewSound" or "removeCustomAdhanSound" => NativeAction(action, ok: false),
            _ => NativeAction(action, ok: false)
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
        if (preset is null) {
            return TasbihSnapshot();
        }

        var name = GetString(payload, "name", preset.Name) ?? preset.Name;
        var repeatMode = NormalizeTasbihRepeatMode(GetString(payload, "repeatMode", preset.RepeatMode));
        ReplaceTasbihPreset(preset, preset with { Name = string.IsNullOrWhiteSpace(name) ? preset.Name : name.Trim(), RepeatMode = repeatMode });
        return TasbihSnapshot();
    }

    private object AddTasbihItem(JsonElement payload) {
        var preset = FindTasbihPreset(payload);
        if (preset is null) {
            return TasbihSnapshot();
        }

        var text = GetString(payload, "text", WebStateDefaults.DefaultTasbihItemText) ?? WebStateDefaults.DefaultTasbihItemText;
        var targetCount = Math.Max(1, GetInt(payload, "targetCount", WebStateDefaults.DefaultTasbihTargetCount));
        preset.Items.Add(new WebTasbihItem(string.IsNullOrWhiteSpace(text) ? WebStateDefaults.DefaultTasbihItemText : text.Trim(), targetCount));
        _state.TasbihCount = 0;
        return TasbihSnapshot();
    }

    private object UpdateTasbihItem(JsonElement payload) {
        var preset = FindTasbihPreset(payload);
        var index = GetInt(payload, "index", -1);
        if (preset is null || index < 0 || index >= preset.Items.Count) {
            return TasbihSnapshot();
        }

        var item = preset.Items[index];
        var text = GetString(payload, "text", item.Text) ?? item.Text;
        var targetCount = Math.Max(1, GetInt(payload, "targetCount", item.TargetCount));
        preset.Items[index] = item with { Text = string.IsNullOrWhiteSpace(text) ? item.Text : text.Trim(), TargetCount = targetCount };
        _state.TasbihCount = 0;
        return TasbihSnapshot();
    }

    private object MoveTasbihItem(JsonElement payload) {
        var preset = FindTasbihPreset(payload);
        var index = GetInt(payload, "index", -1);
        var direction = GetString(payload, "direction", "");
        if (preset is null || index < 0 || index >= preset.Items.Count) {
            return TasbihSnapshot();
        }

        var target = direction == "up" ? index - 1 : direction == "down" ? index + 1 : index;
        if (target < 0 || target >= preset.Items.Count || target == index) {
            return TasbihSnapshot();
        }

        (preset.Items[index], preset.Items[target]) = (preset.Items[target], preset.Items[index]);
        _state.TasbihCount = 0;
        return TasbihSnapshot();
    }

    private object RemoveTasbihItem(JsonElement payload) {
        var preset = FindTasbihPreset(payload);
        var index = GetInt(payload, "index", -1);
        if (preset is not null && preset.Items.Count > 1 && index >= 0 && index < preset.Items.Count) {
            preset.Items.RemoveAt(index);
            _state.TasbihCount = 0;
        }

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

    private object ImportState(string? stateJson) {
        if (!string.IsNullOrWhiteSpace(stateJson)) {
            _state = JsonSerializer.Deserialize(stateJson, CoreJsonContext.Default.WebState) ?? WebState.Default();
            _state.EnsureDefaults();
        }

        return new { ok = true };
    }

    private object ExportState() => JsonSerializer.Serialize(_state, CoreJsonContext.Default.WebState);

    private AppSettings BuildSettings() {
        return new AppSettings {
            Location = new LocationSettings {
                Mode = _state.UseGps ? LocationMode.Gps : LocationMode.Manual,
                City = _state.City,
                Country = _state.Country,
                CountryCode = _state.CountryCode,
                Latitude = _state.Latitude,
                Longitude = _state.Longitude,
                TimeZoneId = TimeZoneInfo.Local.Id
            },
            ClockFormat = _state.ClockFormat == "24h" ? ClockFormat.TwentyFourHour : _state.ClockFormat == "12h" ? ClockFormat.TwelveHour : ClockFormat.Auto,
            Language = _state.Language,
            LanguageSelected = true,
            ThemeMode = _state.ThemeMode switch { "light" => ThemeMode.Light, "dark" => ThemeMode.Dark, _ => ThemeMode.Auto },
            TextScale = _state.TextSize,
            OnboardingCompleted = _state.OnboardingCompleted
        };
    }

    private TasbihPresetSettings ToCorePreset(WebTasbihPreset preset) {
        return new TasbihPresetSettings {
            Name = preset.Name,
            RepeatMode = preset.RepeatMode is "Loop" or "Continue" ? TasbihRepeatMode.RepeatContinue : TasbihRepeatMode.None,
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

    private string LocationTitle() => string.IsNullOrWhiteSpace(_state.Country)
        ? _state.City
        : $"{_state.City}, {_state.Country}";

    private string DirectionLabel(double bearing) {
        string[] labels = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];
        return labels[(int)Math.Round(NormalizeDegrees(bearing) / 45d) % labels.Length];
    }

    private double NormalizeDegrees(double value) {
        var normalized = value % 360d;
        return normalized < 0 ? normalized + 360d : normalized;
    }

    private string? GetString(JsonElement payload, string property, string? fallback) {
        return payload.ValueKind == JsonValueKind.Object &&
               payload.TryGetProperty(property, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : fallback;
    }

    private double GetDouble(JsonElement payload, string property, double fallback) {
        return payload.ValueKind == JsonValueKind.Object &&
               payload.TryGetProperty(property, out var value) &&
               value.TryGetDouble(out var number)
            ? number
            : fallback;
    }

    private bool GetBool(JsonElement payload, string property, bool fallback) {
        return payload.ValueKind == JsonValueKind.Object &&
               payload.TryGetProperty(property, out var value) &&
               value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;
    }

    private int GetInt(JsonElement payload, string property, int fallback) {
        return payload.ValueKind == JsonValueKind.Object &&
               payload.TryGetProperty(property, out var value) &&
               value.TryGetInt32(out var number)
            ? number
            : fallback;
    }

    private string NormalizeTasbihRepeatMode(string? mode) => mode is "Continue" or "Reset" or "None" or "Loop" or "Sequence" ? mode : "Continue";

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
        return string.IsNullOrWhiteSpace(value) ? value : WebCatalog.Translate(_state.Language, value.Trim());
    }

    private static object? CloneJsonValue(JsonElement value) =>
        value.ValueKind == JsonValueKind.Undefined ? null : value.Clone();

}
