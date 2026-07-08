using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace PrayAdFree.WebBridge;

public static partial class WebRpcBridge {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DailyPrayerSnapshotFactory DailyFactory = new();
    private static readonly CalendarMonthPresenter CalendarPresenter = new();
    private static readonly TasbihProgressCalculator TasbihCalculator = new();
    private static WebState State = WebState.Default();

    [JSExport]
    public static string Call(string method, string payloadJson) {
        try {
            using var payloadDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson);
            var data = Handle(method, payloadDocument.RootElement);
            return JsonSerializer.Serialize(new { ok = true, data }, JsonOptions);
        } catch (Exception ex) {
            return JsonSerializer.Serialize(new { ok = false, error = CleanError(ex.Message) }, JsonOptions);
        }
    }

    private static object? Handle(string method, JsonElement payload) {
        return method switch {
            "app.getShellSnapshot" => ShellSnapshot(),
            "app.getLocalization" => Labels(),
            "app.getLanguageObject" => LanguageObject(GetString(payload, "language", State.Language)),
            "app.setLanguage" => SetLanguage(GetString(payload, "language", State.Language)),
            "app.setTheme" => SetTheme(GetString(payload, "theme", State.ThemeMode)),
            "app.navigate" => new { navigatedTo = GetString(payload, "route", "/") },
            "app.importState" => ImportState(GetString(payload, "state", "")),
            "app.exportState" => ExportState(),

            "today.getSnapshot" or "today.refresh" => TodaySnapshot(),

            "calendar.getSnapshot" => CalendarSnapshot(GetString(payload, "month", null)),
            "calendar.setMonth" => SetCalendarMonth(GetString(payload, "month", null)),
            "calendar.today" => SetCalendarMonth(DateTime.Today.ToString("yyyy-MM", CultureInfo.InvariantCulture)),
            "calendar.nextMonth" => MoveCalendar(1),
            "calendar.previousMonth" => MoveCalendar(-1),

            "qibla.getSnapshot" => QiblaSnapshot(),
            "qibla.updateHeading" => SetHeading(GetDouble(payload, "heading", State.Heading)),
            "qibla.setHeadingMode" => SetHeadingMode(GetString(payload, "mode", State.HeadingMode)),
            "qibla.adjustManualHeading" => AdjustManualHeading(GetDouble(payload, "delta", 0)),
            "qibla.commitManualHeading" => QiblaSnapshot(),
            "qibla.setDisplayMode" => SetDisplayMode(GetString(payload, "mode", State.ReadingMode)),
            "qibla.setVisualFilter" => SetVisualFilter(GetString(payload, "mode", State.FilterMode)),

            "tasbih.getSnapshot" => TasbihSnapshot(),
            "tasbih.increment" => IncrementTasbih(),
            "tasbih.reset" => ResetTasbih(),
            "tasbih.selectPreset" => SelectTasbihPreset(GetString(payload, "id", State.SelectedTasbihPresetId)),

            "settings.getSnapshot" => SettingsSnapshot(GetString(payload, "section", "")),
            "settings.setField" => SetSettingsField(payload),
            "settings.patch" => new { ok = true },
            "settings.invoke" => InvokeSetting(GetString(payload, "action", "") ?? "", payload.TryGetProperty("payload", out var actionPayload) ? actionPayload : default),

            "onboarding.getSnapshot" => OnboardingSnapshot(),
            "onboarding.complete" => CompleteOnboarding(),
            "mauiWebber.getRemoteUrl" => RemoteUrlSnapshot(),
            "mauiWebber.setRemoteUrl" => SetRemoteUrl(GetString(payload, "url", State.RemoteWebUrl)),
            "mauiWebber.pullRemote" => new { status = "notAvailable", version = "browser", lastPulledVersion = "browser", error = "Remote bundle pull is only available inside the phone or Windows app." },
            "mauiWebber.useEmbedded" => new { status = "notAvailable", version = "browser", lastPulledVersion = "browser", error = "Embedded bundle reset is only available inside the phone or Windows app." },
            _ => throw new InvalidOperationException($"No web core handler for \"{method}\".")
        };
    }

    private static object ShellSnapshot() => new {
        route = "/",
        language = State.Language,
        isRtl = State.Language == "ar",
        languageObject = LanguageObject(State.Language),
        languages = new[] {
            new { code = "en", name = "English", direction = "ltr" },
            new { code = "ar", name = "العربية", direction = "rtl" },
            new { code = "fr", name = "Français", direction = "ltr" },
            new { code = "es", name = "Español", direction = "ltr" },
            new { code = "tr", name = "Türkçe", direction = "ltr" }
        },
        themeMode = State.ThemeMode,
        accentColor = State.AccentColor,
        textSize = State.TextSize,
        tabs = new[] {
            new { id = "today", label = T("today"), icon = "sun" },
            new { id = "calendar", label = T("calendar"), icon = "calendar" },
            new { id = "qibla", label = T("qibla"), icon = "compass" },
            new { id = "tasbih", label = T("tasbih"), icon = "circle" },
            new { id = "settings", label = T("settings"), icon = "settings" }
        },
        labels = Labels(),
        onboardingCompleted = State.OnboardingCompleted
    };

    private static object LanguageObject(string? language) {
        State.Language = NormalizeLanguage(language);
        return new {
            code = State.Language,
            direction = State.Language == "ar" ? "rtl" : "ltr",
            labels = Labels(),
            updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    private static object SetLanguage(string? language) {
        State.Language = NormalizeLanguage(language);
        return new { ok = true, languageObject = LanguageObject(State.Language) };
    }

    private static object SetTheme(string? theme) {
        State.ThemeMode = NormalizeTheme(theme);
        return new { ok = true };
    }

    private static object TodaySnapshot() {
        var now = DateTime.Now;
        var day = BuildPrayerDay(DateOnly.FromDateTime(now));
        var settings = BuildSettings();
        var snapshot = DailyFactory.Build(day, settings, now);
        var next = snapshot.NextPrayerTime;

        return new {
            locationTitle = LocationTitle(),
            hijriDate = day.Hijri.Date,
            gregorianDate = now.ToString("dddd, dd MMMM yyyy", CultureInfo.InvariantCulture),
            nextPrayerName = PrayerLabel(snapshot.NextPrayerId),
            nextPrayerClock = Format(next, settings),
            nextPrayerBaseClock = snapshot.NextPrayerBaseTime.HasValue ? Format(snapshot.NextPrayerBaseTime.Value, settings) : Format(next, settings),
            showNextPrayerBaseClock = snapshot.NextPrayerBaseTime.HasValue,
            nextPrayerDayLabel = snapshot.IsNextPrayerTomorrow ? T("tomorrow") : T("today"),
            countdown = FormatDuration(next - now),
            statusMessage = "",
            imsakTime = Format(day.Timings.Imsak, settings),
            iftarTime = Format(day.Timings.Maghrib, settings),
            isImsakNext = snapshot.NextPrayerId == PrayerId.Imsak,
            isIftarNext = snapshot.NextPrayerId == PrayerId.Maghrib,
            nextFastingCountdown = FormatDuration(day.Timings.Maghrib - now),
            isRtl = State.Language == "ar",
            labels = Labels(),
            todayTimings = snapshot.Entries.Select(entry => new {
                id = entry.Prayer.ToString().ToLowerInvariant(),
                name = PrayerLabel(entry.Prayer),
                time = Format(entry.AdjustedTime, settings),
                baseTime = Format(entry.BaseTime, settings),
                isNext = entry.IsNext
            }).ToArray()
        };
    }

    private static object CalendarSnapshot(string? monthValue = null) {
        if (!string.IsNullOrWhiteSpace(monthValue) && DateTime.TryParseExact(monthValue + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) {
            State.SelectedMonth = CalendarPresenter.NormalizeMonth(parsed);
        }

        var settings = BuildSettings();
        var month = new PrayerMonth {
            Year = State.SelectedMonth.Year,
            Month = State.SelectedMonth.Month,
            LocationKey = $"{State.CountryCode}:{State.City}",
            MethodKey = "web-core",
            FetchedOnUtc = DateTime.UtcNow,
            Days = Enumerable.Range(1, DateTime.DaysInMonth(State.SelectedMonth.Year, State.SelectedMonth.Month))
                .Select(day => BuildPrayerDay(new DateOnly(State.SelectedMonth.Year, State.SelectedMonth.Month, day)))
                .ToArray()
        };
        var rows = CalendarPresenter.BuildRows(month, settings, CultureInfo.InvariantCulture);

        return new {
            selectedMonth = State.SelectedMonth.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            selectedMonthValue = State.SelectedMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            statusMessage = "",
            days = rows.Select(row => new {
                date = row.Date,
                hijri = row.Hijri,
                fajr = row.Fajr,
                sunrise = row.Sunrise,
                dhuhr = row.Dhuhr,
                asr = row.Asr,
                maghrib = row.Maghrib,
                isha = row.Isha,
                isToday = row.SourceDate == DateOnly.FromDateTime(DateTime.Today)
            }).ToArray(),
            isRtl = State.Language == "ar"
        };
    }

    private static object SetCalendarMonth(string? month) => CalendarSnapshot(month);

    private static object MoveCalendar(int offset) {
        State.SelectedMonth = CalendarPresenter.MoveMonth(State.SelectedMonth, offset);
        return CalendarSnapshot();
    }

    private static object QiblaSnapshot() {
        var bearing = QiblaCalculator.CalculateBearing(State.Latitude, State.Longitude);
        var heading = State.HeadingMode == "manual" ? State.ManualHeading : State.Heading;
        var needleRotation = NormalizeDegrees(bearing - heading);
        var aligned = Math.Abs(needleRotation) < 5 || Math.Abs(needleRotation - 360) < 5;
        var displayMode = State.ReadingMode == "map" ? "Map" : "Compass";

        return new {
            bearing,
            heading,
            latitude = State.Latitude,
            longitude = State.Longitude,
            needleRotation,
            compassRotation = -heading,
            directionLabel = DirectionLabel(bearing),
            locationTitle = LocationTitle(),
            statusMessage = aligned ? T("aligned") : "",
            selectedHeadingMode = State.HeadingMode,
            selectedReadingMode = State.ReadingMode,
            selectedFilterMode = State.FilterMode,
            displayMode,
            visualFilter = State.FilterMode switch { "night" => "Night", "contrast" => "Contrast", _ => "None" },
            state = State.ReadingMode == "map" ? "map" : State.HeadingMode == "manual" ? "manual" : aligned ? "aligned" : "sensor",
            isAligned = aligned,
            headingModes = new[] { new { id = "auto", label = T("auto") }, new { id = "manual", label = T("manual") } },
            readingModes = new[] { new { id = "compass", label = T("compass") }, new { id = "map", label = T("map") } },
            filterModes = new[] { new { id = "none", label = T("filter_none") }, new { id = "night", label = T("filter_night") }, new { id = "contrast", label = T("filter_contrast") } },
            labels = Labels()
        };
    }

    private static object SetHeading(double heading) {
        State.Heading = NormalizeDegrees(heading);
        return QiblaSnapshot();
    }

    private static object SetHeadingMode(string? mode) {
        State.HeadingMode = mode == "manual" ? "manual" : "auto";
        return QiblaSnapshot();
    }

    private static object AdjustManualHeading(double delta) {
        State.ManualHeading = NormalizeDegrees(State.ManualHeading + delta);
        return QiblaSnapshot();
    }

    private static object SetDisplayMode(string? mode) {
        State.ReadingMode = mode == "map" ? "map" : "compass";
        return QiblaSnapshot();
    }

    private static object SetVisualFilter(string? mode) {
        State.FilterMode = mode is "night" or "contrast" ? mode : "none";
        return QiblaSnapshot();
    }

    private static object TasbihSnapshot() {
        var preset = State.TasbihPresets.FirstOrDefault(item => item.Id == State.SelectedTasbihPresetId) ?? State.TasbihPresets[0];
        var corePreset = ToCorePreset(preset);
        var progress = TasbihCalculator.BuildSnapshot(corePreset, State.TasbihCount);
        var total = TasbihCalculator.GetTotalTarget(corePreset);
        return new {
            count = State.TasbihCount,
            currentPhrase = string.IsNullOrWhiteSpace(progress.CurrentText) ? preset.Items[0].Text : progress.CurrentText,
            progressText = $"{Math.Min(State.TasbihCount, total)} / {total}",
            isPresetSelectionEnabled = State.TasbihCount == 0,
            selectedPresetId = preset.Id,
            presets = State.TasbihPresets.Select(item => new {
                id = item.Id,
                name = item.Name,
                repeatMode = item.RepeatMode,
                items = item.Items.Select(i => new { i.Text, i.TargetCount }).ToArray()
            }).ToArray()
        };
    }

    private static object IncrementTasbih() {
        var preset = State.TasbihPresets.First(item => item.Id == State.SelectedTasbihPresetId);
        State.TasbihCount = TasbihCalculator.GetNextCount(ToCorePreset(preset), State.TasbihCount);
        return TasbihSnapshot();
    }

    private static object ResetTasbih() {
        State.TasbihCount = 0;
        return TasbihSnapshot();
    }

    private static object SelectTasbihPreset(string? id) {
        if (State.TasbihCount == 0 && State.TasbihPresets.Any(item => item.Id == id)) {
            State.SelectedTasbihPresetId = id!;
        }

        return TasbihSnapshot();
    }

    private static object SettingsSnapshot(string? section) {
        return section switch {
            "locations" => new {
                useGps = State.UseGps,
                latitude = State.Latitude,
                longitude = State.Longitude,
                country = State.CountryCode,
                countryName = State.Country,
                city = State.City,
                vpnWarning = false,
                qiblaReadingMode = State.ReadingMode,
                qiblaFilterMode = State.FilterMode,
                qiblaReadingModes = new[] { new { id = "compass", label = T("compass") }, new { id = "map", label = T("map") } },
                qiblaFilterModes = new[] { new { id = "none", label = T("filter_none") }, new { id = "night", label = T("filter_night") }, new { id = "contrast", label = T("filter_contrast") } },
                countries = new[] {
                    new { code = "NL", name = "Netherlands", cities = new[] { "Amsterdam", "Rotterdam", "Utrecht" } },
                    new { code = "SA", name = "Saudi Arabia", cities = new[] { "Makkah", "Madinah", "Riyadh" } }
                },
                places = new[] {
                    new { country = "Netherlands", countryCode = "NL", city = "Amsterdam", latitude = 52.3676, longitude = 4.9041 },
                    new { country = "Saudi Arabia", countryCode = "SA", city = "Makkah", latitude = 21.3891, longitude = 39.8579 }
                }
            },
            "theme" => new {
                language = State.Language,
                themeMode = State.ThemeMode,
                accentColor = State.AccentColor,
                textSize = State.TextSize,
                diagnostics = new { bridgeReady = true, lastSync = "WASM core" },
                languages = new[] {
                    new { code = "en", name = "English" },
                    new { code = "ar", name = "العربية" },
                    new { code = "fr", name = "Français" },
                    new { code = "es", name = "Español" },
                    new { code = "tr", name = "Türkçe" }
                },
                accentColors = new[] { "teal", "green", "blue", "amber", "rose" }
            },
            "adhan" => new {
                sounds = new[] { new { id = "makkah", label = "Makkah", selected = true, isCustom = false, canPreview = false } },
                volume = 80,
                calculationMethod = "Auto",
                madhhab = "Shafi",
                highLatitudeRule = "MiddleOfTheNight",
                fajrAngle = 18,
                ishaAngle = 17,
                isCustomMethod = false,
                offsets = new { fajr = 0, sunrise = 0, dhuhr = 0, asr = 0, maghrib = 0, isha = 0, imsak = 0 },
                clockFormat = State.ClockFormat,
                fasting = new { iftarDelay = 0, imsakAdvance = 10 },
                imsakReminders = Array.Empty<object>(),
                iftarReminders = Array.Empty<object>(),
                perPrayerOverrides = Array.Empty<object>()
            },
            "notifications" => new {
                enableAdhan = true,
                mobilePrimaryAdhanType = "Full",
                hideOnCloseWindows = false,
                runBackgroundServiceWindows = false,
                vibration = false,
                vibrationStrength = "Medium",
                vibrationPattern = "Default",
                minutesBefore = 10,
                reminders = Array.Empty<object>()
            },
            "permissions" => PermissionsSnapshot(),
            "alarmReminders" => new {
                builtIn = new[] { new { id = "wudu", text = "Make wudu before prayer", enabled = true }, new { id = "qibla", text = "Face the Qibla", enabled = true } },
                userRemindersEnabled = true,
                userReminders = Array.Empty<object>()
            },
            _ => new { locations = SettingsSnapshot("locations"), theme = SettingsSnapshot("theme"), adhan = SettingsSnapshot("adhan") }
        };
    }

    private static object SetSettingsField(JsonElement payload) {
        var section = GetString(payload, "section", "");
        var field = GetString(payload, "field", "");
        var value = payload.TryGetProperty("value", out var valueElement) ? valueElement : default;

        if (section == "theme" && field == "language") {
            State.Language = NormalizeLanguage(value.ValueKind == JsonValueKind.String ? value.GetString() : null);
            return new { ok = true, section, field, value = State.Language, languageObject = LanguageObject(State.Language) };
        }

        if (section == "theme" && field == "themeMode") {
            State.ThemeMode = NormalizeTheme(value.ValueKind == JsonValueKind.String ? value.GetString() : null);
        } else if (section == "locations" && field == "value" && value.ValueKind == JsonValueKind.Object) {
            State.UseGps = GetBool(value, "useGps", State.UseGps);
            State.Latitude = GetDouble(value, "latitude", State.Latitude);
            State.Longitude = GetDouble(value, "longitude", State.Longitude);
            State.CountryCode = GetString(value, "country", State.CountryCode) ?? State.CountryCode;
            State.Country = GetString(value, "countryName", State.Country) ?? State.Country;
            State.City = GetString(value, "city", State.City) ?? State.City;
            State.ReadingMode = GetString(value, "qiblaReadingMode", State.ReadingMode) ?? State.ReadingMode;
            State.FilterMode = GetString(value, "qiblaFilterMode", State.FilterMode) ?? State.FilterMode;
        } else if (section == "adhan" && field == "value" && value.ValueKind == JsonValueKind.Object) {
            State.ClockFormat = GetString(value, "clockFormat", State.ClockFormat) ?? State.ClockFormat;
        }

        return new { ok = true, section, field, value = value.ValueKind == JsonValueKind.Undefined ? null : JsonSerializer.Deserialize<object>(value.GetRawText(), JsonOptions) };
    }

    private static object InvokeSetting(string action, JsonElement payload) {
        return action switch {
            "requestPermission" => new { ok = true, action, platform = "web", message = "Use browser permission prompts where available; native permissions are not required in browser web." },
            "requestAllPermissions" => new { ok = true, action, platform = "web", message = "Browser permissions are requested only when a web API needs them." },
            "refreshGps" => new { ok = false, action, platform = "web", message = "Browser location is handled by the web adapter; enter the location manually if geolocation is unavailable." },
            "openUrl" or "openEmail" or "call" => new { ok = true, action, platform = "web", intent = payload.ValueKind == JsonValueKind.Undefined ? null : JsonSerializer.Deserialize<object>(payload.GetRawText(), JsonOptions) },
            "addTasbihPreset" => AddTasbihPreset(GetString(payload, "name", "New preset")),
            "updateTasbihPreset" => UpdateTasbihPreset(payload),
            "addTasbihItem" => AddTasbihItem(payload),
            "updateTasbihItem" => UpdateTasbihItem(payload),
            "moveTasbihItem" => MoveTasbihItem(payload),
            "removeTasbihItem" => RemoveTasbihItem(payload),
            "addCustomAdhanSound" or "testNotification" or "previewSound" or "removeCustomAdhanSound" => new { ok = false, action, platform = "web", message = "Native adhan sound actions are not available in browser web." },
            _ => new { ok = false, action, platform = "web", message = "This native action is not available in browser web." }
        };
    }

    private static object AddTasbihPreset(string? name) {
        var normalizedName = string.IsNullOrWhiteSpace(name) ? "New preset" : name.Trim();
        var idBase = Slug(normalizedName);
        var id = idBase;
        var suffix = 2;
        while (State.TasbihPresets.Any(item => item.Id == id)) {
            id = $"{idBase}-{suffix++}";
        }

        State.TasbihPresets.Add(new WebTasbihPreset(id, normalizedName, "Continue", [new WebTasbihItem("SubhanAllah", 33)]));
        State.SelectedTasbihPresetId = id;
        State.TasbihCount = 0;
        return TasbihSnapshot();
    }

    private static object UpdateTasbihPreset(JsonElement payload) {
        var id = GetString(payload, "id", null);
        var preset = State.TasbihPresets.FirstOrDefault(item => item.Id == id);
        if (preset is null) {
            return TasbihSnapshot();
        }

        var name = GetString(payload, "name", preset.Name) ?? preset.Name;
        var repeatMode = NormalizeTasbihRepeatMode(GetString(payload, "repeatMode", preset.RepeatMode));
        ReplaceTasbihPreset(preset, preset with { Name = string.IsNullOrWhiteSpace(name) ? preset.Name : name.Trim(), RepeatMode = repeatMode });
        return TasbihSnapshot();
    }

    private static object AddTasbihItem(JsonElement payload) {
        var preset = FindTasbihPreset(payload);
        if (preset is null) {
            return TasbihSnapshot();
        }

        var text = GetString(payload, "text", "SubhanAllah") ?? "SubhanAllah";
        var targetCount = Math.Max(1, GetInt(payload, "targetCount", 33));
        preset.Items.Add(new WebTasbihItem(string.IsNullOrWhiteSpace(text) ? "SubhanAllah" : text.Trim(), targetCount));
        State.TasbihCount = 0;
        return TasbihSnapshot();
    }

    private static object UpdateTasbihItem(JsonElement payload) {
        var preset = FindTasbihPreset(payload);
        var index = GetInt(payload, "index", -1);
        if (preset is null || index < 0 || index >= preset.Items.Count) {
            return TasbihSnapshot();
        }

        var item = preset.Items[index];
        var text = GetString(payload, "text", item.Text) ?? item.Text;
        var targetCount = Math.Max(1, GetInt(payload, "targetCount", item.TargetCount));
        preset.Items[index] = item with { Text = string.IsNullOrWhiteSpace(text) ? item.Text : text.Trim(), TargetCount = targetCount };
        State.TasbihCount = 0;
        return TasbihSnapshot();
    }

    private static object MoveTasbihItem(JsonElement payload) {
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
        State.TasbihCount = 0;
        return TasbihSnapshot();
    }

    private static object RemoveTasbihItem(JsonElement payload) {
        var preset = FindTasbihPreset(payload);
        var index = GetInt(payload, "index", -1);
        if (preset is not null && preset.Items.Count > 1 && index >= 0 && index < preset.Items.Count) {
            preset.Items.RemoveAt(index);
            State.TasbihCount = 0;
        }

        return TasbihSnapshot();
    }

    private static WebTasbihPreset? FindTasbihPreset(JsonElement payload) {
        var presetId = GetString(payload, "presetId", State.SelectedTasbihPresetId);
        return State.TasbihPresets.FirstOrDefault(item => item.Id == presetId);
    }

    private static void ReplaceTasbihPreset(WebTasbihPreset original, WebTasbihPreset replacement) {
        var index = State.TasbihPresets.IndexOf(original);
        if (index >= 0) {
            State.TasbihPresets[index] = replacement;
        }
    }

    private static object RemoteUrlSnapshot() => new {
        url = State.RemoteWebUrl,
        manifestUrl = BuildManifestUrl(State.RemoteWebUrl),
        lastPulledVersion = "browser"
    };

    private static object SetRemoteUrl(string? url) {
        State.RemoteWebUrl = NormalizeRemoteUrl(url);
        return RemoteUrlSnapshot();
    }

    private static object OnboardingSnapshot() => new {
        language = State.Language,
        isRtl = State.Language == "ar",
        permissions = PermissionItems(),
        labels = Labels(),
        location = SettingsSnapshot("locations"),
        completed = State.OnboardingCompleted
    };

    private static object PermissionsSnapshot() => new {
        alarmMode = new { title = "Exact alarms", status = "Not available on web", description = "Native exact alarms require the phone or Windows app." },
        items = PermissionItems()
    };

    private static object PermissionItems() => new[] {
        new { id = "location", title = "Location", role = "critical", description = "Browser geolocation can be requested here.", fallback = "Manual entry", status = "Available", action = "Grant" },
        new { id = "notifications", title = "Notifications", role = "critical", description = "Browser notifications can be requested here.", fallback = "In-app messages", status = "Available", action = "Grant" },
        new { id = "background", title = "Background activity", role = "optional", description = "Background native alarms are not available in browser web.", fallback = "Foreground only", status = "Not available", action = "Unavailable" }
    };

    private static object CompleteOnboarding() {
        State.OnboardingCompleted = true;
        return new { ok = true };
    }

    private static object ImportState(string? stateJson) {
        if (!string.IsNullOrWhiteSpace(stateJson)) {
            State = JsonSerializer.Deserialize<WebState>(stateJson, JsonOptions) ?? WebState.Default();
            State.EnsureDefaults();
        }

        return new { ok = true };
    }

    private static object ExportState() => JsonSerializer.Serialize(State, JsonOptions);

    private static AppSettings BuildSettings() {
        return new AppSettings {
            Location = new LocationSettings {
                Mode = State.UseGps ? LocationMode.Gps : LocationMode.Manual,
                City = State.City,
                Country = State.Country,
                CountryCode = State.CountryCode,
                Latitude = State.Latitude,
                Longitude = State.Longitude,
                TimeZoneId = TimeZoneInfo.Local.Id
            },
            ClockFormat = State.ClockFormat == "24h" ? ClockFormat.TwentyFourHour : State.ClockFormat == "12h" ? ClockFormat.TwelveHour : ClockFormat.Auto,
            Language = State.Language,
            LanguageSelected = true,
            ThemeMode = State.ThemeMode switch { "light" => ThemeMode.Light, "dark" => ThemeMode.Dark, _ => ThemeMode.Auto },
            TextScale = State.TextSize,
            OnboardingCompleted = State.OnboardingCompleted
        };
    }

    private static PrayerDay BuildPrayerDay(DateOnly date) {
        var baseDate = date.ToDateTime(TimeOnly.MinValue);
        return new PrayerDay {
            Date = date,
            TimeZoneId = TimeZoneInfo.Local.Id,
            Hijri = new HijriDate { Day = date.Day.ToString("00", CultureInfo.InvariantCulture), Month = "Ramadan", Year = "1447" },
            Timings = new PrayerTimings {
                Imsak = baseDate.AddHours(4).AddMinutes(52),
                Fajr = baseDate.AddHours(5).AddMinutes(12),
                Sunrise = baseDate.AddHours(6).AddMinutes(47),
                Dhuhr = baseDate.AddHours(13).AddMinutes(28),
                Asr = baseDate.AddHours(16).AddMinutes(5),
                Maghrib = baseDate.AddHours(19).AddMinutes(52),
                Isha = baseDate.AddHours(21).AddMinutes(18)
            }
        };
    }

    private static TasbihPresetSettings ToCorePreset(WebTasbihPreset preset) {
        return new TasbihPresetSettings {
            Name = preset.Name,
            RepeatMode = preset.RepeatMode is "Loop" or "Continue" ? TasbihRepeatMode.RepeatContinue : TasbihRepeatMode.None,
            Items = preset.Items.Select(item => new TasbihItemSettings { Text = item.Text, TargetCount = item.TargetCount }).ToList()
        };
    }

    private static string Format(DateTime value, AppSettings settings) {
        return settings.ClockFormat switch {
            ClockFormat.TwelveHour => value.ToString("h:mm tt", CultureInfo.InvariantCulture),
            _ => value.ToString("HH:mm", CultureInfo.InvariantCulture)
        };
    }

    private static string FormatDuration(TimeSpan value) {
        if (value < TimeSpan.Zero) {
            value = TimeSpan.Zero;
        }

        return $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
    }

    private static string PrayerLabel(PrayerId id) => id switch {
        PrayerId.Fajr => T("fajr"),
        PrayerId.Sunrise => T("sunrise"),
        PrayerId.Dhuhr => T("dhuhr"),
        PrayerId.Asr => T("asr"),
        PrayerId.Maghrib => T("maghrib"),
        PrayerId.Isha => T("isha"),
        PrayerId.Imsak => T("imsak"),
        _ => id.ToString()
    };

    private static string LocationTitle() => string.IsNullOrWhiteSpace(State.Country)
        ? State.City
        : $"{State.City}, {State.Country}";

    private static string DirectionLabel(double bearing) {
        string[] labels = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];
        return labels[(int)Math.Round(NormalizeDegrees(bearing) / 45d) % labels.Length];
    }

    private static double NormalizeDegrees(double value) {
        var normalized = value % 360d;
        return normalized < 0 ? normalized + 360d : normalized;
    }

    private static string NormalizeLanguage(string? language) => language is "ar" or "fr" or "es" or "tr" ? language : "en";

    private static string NormalizeTheme(string? theme) => theme is "light" or "dark" ? theme : "system";

    private static string? GetString(JsonElement payload, string property, string? fallback) {
        return payload.ValueKind == JsonValueKind.Object &&
               payload.TryGetProperty(property, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : fallback;
    }

    private static double GetDouble(JsonElement payload, string property, double fallback) {
        return payload.ValueKind == JsonValueKind.Object &&
               payload.TryGetProperty(property, out var value) &&
               value.TryGetDouble(out var number)
            ? number
            : fallback;
    }

    private static bool GetBool(JsonElement payload, string property, bool fallback) {
        return payload.ValueKind == JsonValueKind.Object &&
               payload.TryGetProperty(property, out var value) &&
               value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;
    }

    private static int GetInt(JsonElement payload, string property, int fallback) {
        return payload.ValueKind == JsonValueKind.Object &&
               payload.TryGetProperty(property, out var value) &&
               value.TryGetInt32(out var number)
            ? number
            : fallback;
    }

    private static string NormalizeTasbihRepeatMode(string? mode) => mode is "Continue" or "Reset" or "None" or "Loop" or "Sequence" ? mode : "Continue";

    private static string Slug(string value) {
        var chars = value.ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var slug = string.Join("-", new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(slug) ? "preset" : slug;
    }

    private static string NormalizeRemoteUrl(string? value) {
        var candidate = string.IsNullOrWhiteSpace(value) ? "http://pray.rynex.nl/" : value.Trim();
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

    private static string BuildManifestUrl(string baseUrl) => new Uri(new Uri(baseUrl), "web.manifest.json").ToString();

    private static object Labels() => State.Language == "ar" ? ArabicLabels : EnglishLabels;

    private static string T(string key) {
        var labels = State.Language == "ar" ? ArabicLabels : EnglishLabels;
        return labels.TryGetValue(key, out var value) ? value : key;
    }

    private static string CleanError(string message) => string.IsNullOrWhiteSpace(message) ? "Unknown web core error." : message.Split('\n')[0].Trim();

    private static readonly Dictionary<string, string> EnglishLabels = new() {
        ["today"] = "Today", ["tomorrow"] = "Tomorrow", ["calendar"] = "Calendar", ["qibla"] = "Qibla", ["tasbih"] = "Tasbih", ["settings"] = "Settings",
        ["nextPrayer"] = "Next prayer", ["fajr"] = "Fajr", ["sunrise"] = "Sunrise", ["dhuhr"] = "Dhuhr", ["asr"] = "Asr", ["maghrib"] = "Maghrib", ["isha"] = "Isha",
        ["imsak"] = "Imsak", ["iftar"] = "Iftar", ["basmala"] = "In the name of Allah, the Most Gracious, the Most Merciful", ["aligned"] = "Aligned with Qibla",
        ["auto"] = "Auto", ["manual"] = "Manual", ["compass"] = "Compass", ["map"] = "Map", ["filter_none"] = "None", ["filter_night"] = "Night", ["filter_contrast"] = "Contrast",
        ["qiblaDirection"] = "Qibla Direction", ["permissionMissing"] = "Location permission required", ["grantPermission"] = "Grant permission",
        ["status_ready"] = "Ready", ["status_saving"] = "Saving", ["status_saved"] = "Saved", ["status_error"] = "Error", ["status_refreshing"] = "Refreshing",
        ["locationAndGps"] = "Location and GPS", ["useGps"] = "Use GPS", ["refreshGps"] = "Refresh GPS", ["enabled"] = "Enabled", ["disabled"] = "Disabled",
        ["locations"] = "Locations", ["country"] = "Country", ["city"] = "City", ["latitude"] = "Latitude", ["longitude"] = "Longitude",
        ["qiblaPreferences"] = "Qibla preferences", ["compassReadingMode"] = "Compass reading mode", ["compassFilter"] = "Compass filter",
        ["cardinalNorth"] = "N", ["cardinalEast"] = "E", ["cardinalSouth"] = "S", ["cardinalWest"] = "W"
    };

    private static readonly Dictionary<string, string> ArabicLabels = new(EnglishLabels) {
        ["today"] = "اليوم", ["tomorrow"] = "غدًا", ["calendar"] = "التقويم", ["qibla"] = "القبلة", ["tasbih"] = "التسبيح", ["settings"] = "الإعدادات",
        ["nextPrayer"] = "الصلاة التالية", ["fajr"] = "الفجر", ["sunrise"] = "الشروق", ["dhuhr"] = "الظهر", ["asr"] = "العصر", ["maghrib"] = "المغرب", ["isha"] = "العشاء",
        ["imsak"] = "الإمساك", ["iftar"] = "الإفطار", ["aligned"] = "متوافق مع القبلة", ["auto"] = "تلقائي", ["manual"] = "يدوي", ["compass"] = "البوصلة", ["map"] = "الخريطة"
    };
}

public sealed class WebState {
    public string Language { get; set; } = "en";
    public string ThemeMode { get; set; } = "system";
    public string AccentColor { get; set; } = "teal";
    public int TextSize { get; set; } = 100;
    public bool OnboardingCompleted { get; set; } = true;
    public bool UseGps { get; set; }
    public string Country { get; set; } = "Netherlands";
    public string CountryCode { get; set; } = "NL";
    public string City { get; set; } = "Amsterdam";
    public double Latitude { get; set; } = 52.3676;
    public double Longitude { get; set; } = 4.9041;
    public DateTime SelectedMonth { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public double Heading { get; set; } = 95;
    public double ManualHeading { get; set; } = 100;
    public string HeadingMode { get; set; } = "auto";
    public string ReadingMode { get; set; } = "compass";
    public string FilterMode { get; set; } = "none";
    public string ClockFormat { get; set; } = "24h";
    public string RemoteWebUrl { get; set; } = "http://pray.rynex.nl/";
    public int TasbihCount { get; set; }
    public string SelectedTasbihPresetId { get; set; } = "after-prayer";
    public List<WebTasbihPreset> TasbihPresets { get; set; } = DefaultTasbihPresets();

    public static WebState Default() => new();

    public void EnsureDefaults() {
        if (TasbihPresets.Count == 0) {
            TasbihPresets = DefaultTasbihPresets();
        }

        if (!TasbihPresets.Any(item => item.Id == SelectedTasbihPresetId)) {
            SelectedTasbihPresetId = TasbihPresets[0].Id;
        }

        if (string.IsNullOrWhiteSpace(RemoteWebUrl)) {
            RemoteWebUrl = "http://pray.rynex.nl/";
        }
    }

    private static List<WebTasbihPreset> DefaultTasbihPresets() => [
        new WebTasbihPreset("after-prayer", "After Prayer", "Sequence", [
            new WebTasbihItem("SubhanAllah", 33),
            new WebTasbihItem("Alhamdulillah", 33),
            new WebTasbihItem("Allahu Akbar", 34)
        ]),
        new WebTasbihPreset("istighfar", "Istighfar", "Loop", [new WebTasbihItem("Astaghfirullah", 100)]),
        new WebTasbihPreset("salawat", "Salawat", "Loop", [new WebTasbihItem("Allahumma salli ala Muhammad", 100)])
    ];
}

public sealed record WebTasbihPreset(string Id, string Name, string RepeatMode, List<WebTasbihItem> Items);

public sealed record WebTasbihItem(string Text, int TargetCount);
