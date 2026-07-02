using System.Globalization;
using System.Text.Json;
using MauiWebber;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Services;

public sealed class WebAppRpcHandler : IMauiWebberRpcHandler {
    private readonly TodayWebRpcHandler _today;
    private readonly CalendarViewModel _calendar;
    private readonly QiblaViewModel _qibla;
    private readonly TasbihViewModel _tasbih;
    private readonly SettingsService _settingsService;
    private readonly IAppPermissionCenterService _permissionCenter;
    private readonly AndroidAlarmCapabilityService _alarmCapability;
    private DateTime _calendarMonth = DateTime.Today;
    private bool _qiblaLoaded;
    private string _qiblaDisplayMode = "compass";
    private string _qiblaVisualFilter = "none";

    public WebAppRpcHandler(
        TodayWebRpcHandler today,
        CalendarViewModel calendar,
        QiblaViewModel qibla,
        TasbihViewModel tasbih,
        SettingsService settingsService,
        IAppPermissionCenterService permissionCenter,
        AndroidAlarmCapabilityService alarmCapability) {
        _today = today;
        _calendar = calendar;
        _qibla = qibla;
        _tasbih = tasbih;
        _settingsService = settingsService;
        _permissionCenter = permissionCenter;
        _alarmCapability = alarmCapability;
        _calendarMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    }

    public Task PreloadAsync() {
        return _today.PreloadAsync();
    }

    public async Task<object?> HandleAsync(string method, JsonElement payload, CancellationToken cancellationToken) {
        return method switch {
            "today.getSnapshot" or "today.refresh" => await _today.HandleAsync(method, payload, cancellationToken).ConfigureAwait(false),
            "app.getShellSnapshot" => BuildShellSnapshot(),
            "app.getLocalization" => BuildLabels(),
            "app.setLanguage" => SetLanguage(payload),
            "app.setTheme" => new { ok = true },
            "app.navigate" => new { ok = true },
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
            "tasbih.increment" => RunTasbihCommand(_tasbih.IncrementCommand),
            "tasbih.reset" => RunTasbihCommand(_tasbih.ResetCommand),
            "tasbih.selectPreset" => SelectTasbihPreset(payload),
            "settings.getSnapshot" => await GetSettingsSnapshotAsync(payload).ConfigureAwait(false),
            "settings.patch" => new { ok = true },
            "settings.invoke" => new { ok = true },
            "onboarding.getSnapshot" => BuildOnboardingSnapshot(),
            "onboarding.complete" => new { ok = true },
            _ => throw new InvalidOperationException($"Unknown MauiWebber RPC method: {method}")
        };
    }

    private object BuildShellSnapshot() {
        var settings = _settingsService.Load();
        var language = ResolveLanguage(settings.Language);
        return new {
            route = "/",
            language,
            isRtl = IsRtl(),
            themeMode = ResolveTheme(settings.ThemeMode),
            accentColor = "teal",
            tabs = new[] {
                new { id = "today", label = T("Today"), icon = "sun" },
                new { id = "calendar", label = T("Calendar"), icon = "calendar" },
                new { id = "qibla", label = T("Qibla"), icon = "compass" },
                new { id = "tasbih", label = T("Tasbih"), icon = "circle" },
                new { id = "settings", label = T("Settings"), icon = "settings" }
            },
            labels = BuildLabels(),
            onboardingCompleted = settings.OnboardingCompleted
        };
    }

    private static IReadOnlyDictionary<string, string> BuildLabels() {
        return new Dictionary<string, string> {
            ["today"] = T("Today"),
            ["calendar"] = T("Calendar"),
            ["qibla"] = T("Qibla"),
            ["tasbih"] = T("Tasbih"),
            ["settings"] = T("Settings"),
            ["nextPrayer"] = T("NextPrayer"),
            ["timeLeft"] = T("TimeLeft"),
            ["qiblaDirection"] = T("QiblaDirection"),
            ["permissionMissing"] = T("PermissionStatus_Disabled"),
            ["grantPermission"] = T("PermissionAction_Request"),
            ["auto"] = T("QiblaHeadingMode_Auto"),
            ["manual"] = T("QiblaHeadingMode_Manual"),
            ["compass"] = T("QiblaModeCompass"),
            ["map"] = T("QiblaModeMap"),
            ["filter_none"] = T("QiblaVisualFilter_None"),
            ["filter_night"] = T("QiblaVisualFilter_Night"),
            ["filter_contrast"] = T("QiblaVisualFilter_Contrast"),
            ["searching"] = T("FindingLocation"),
            ["aligned"] = T("StayReady")
        };
    }

    private object SetLanguage(JsonElement payload) {
        var language = ReadString(payload, "language");
        if (!string.IsNullOrWhiteSpace(language)) {
            LocalizationManager.SetLanguage(language);
        }

        return new { ok = true };
    }

    private async Task<object> GetCalendarAsync(JsonElement payload) {
        var requestedMonth = ReadString(payload, "month");
        if (!string.IsNullOrWhiteSpace(requestedMonth) &&
            DateTime.TryParse($"{requestedMonth}-01", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) {
            _calendarMonth = new DateTime(parsed.Year, parsed.Month, 1);
        }

        _calendar.SelectedMonth = _calendarMonth;
        await _calendar.LoadAsync().ConfigureAwait(false);
        return BuildCalendarSnapshot();
    }

    private Task<object> SetCalendarMonthAsync(JsonElement payload) {
        return GetCalendarAsync(payload);
    }

    private async Task<object> MoveCalendarAsync(int offset, bool today = false) {
        _calendarMonth = today
            ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
            : _calendarMonth.AddMonths(offset);
        _calendar.SelectedMonth = _calendarMonth;
        await _calendar.LoadAsync().ConfigureAwait(false);
        return BuildCalendarSnapshot();
    }

    private object BuildCalendarSnapshot() {
        return new {
            selectedMonth = _calendarMonth.ToString("MMMM yyyy", CultureInfo.CurrentUICulture),
            statusMessage = _calendar.StatusMessage,
            days = _calendar.Days.Select(day => new {
                date = day.Date,
                hijri = day.Hijri,
                fajr = day.Fajr,
                sunrise = day.Sunrise,
                dhuhr = day.Dhuhr,
                asr = day.Asr,
                maghrib = day.Maghrib,
                isha = day.Isha,
                isToday = IsToday(day)
            }).ToList()
        };
    }

    private static bool IsToday(CalendarDayRow day) {
        return day.SourceDate == DateOnly.FromDateTime(DateTime.Today);
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

    private object RunTasbihCommand(Command command) {
        if (command.CanExecute(null)) {
            command.Execute(null);
        }

        return BuildTasbihSnapshot();
    }

    private object SelectTasbihPreset(JsonElement payload) {
        var id = ReadString(payload, "id");
        var preset = _tasbih.Presets.Select((item, index) => new { item, index })
            .FirstOrDefault(item => string.Equals(item.index.ToString(CultureInfo.InvariantCulture), id, StringComparison.Ordinal));
        if (preset?.item != null) {
            _tasbih.SelectedPreset = preset.item;
        }

        return BuildTasbihSnapshot();
    }

    private object BuildTasbihSnapshot() {
        return new {
            count = _tasbih.Count,
            currentPhrase = _tasbih.CurrentPhrase,
            progressText = _tasbih.ProgressText,
            isPresetSelectionEnabled = _tasbih.IsPresetSelectionEnabled,
            selectedPresetId = Math.Max(0, _tasbih.Presets.IndexOf(_tasbih.SelectedPreset!)).ToString(CultureInfo.InvariantCulture),
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

    private static object BuildLocationsSettings(AppSettings settings) {
        return new {
            useGps = settings.Location.Mode == LocationMode.Gps,
            latitude = settings.Location.Latitude,
            longitude = settings.Location.Longitude,
            country = string.IsNullOrWhiteSpace(settings.Location.CountryCode) ? "NL" : settings.Location.CountryCode,
            city = string.IsNullOrWhiteSpace(settings.Location.City) ? "Amsterdam" : settings.Location.City,
            vpnWarning = false,
            countries = new[] {
                new { code = "NL", name = "Netherlands", cities = new[] { "Amsterdam", "Rotterdam", "Utrecht" } },
                new { code = "SA", name = "Saudi Arabia", cities = new[] { "Makkah", "Madinah", "Riyadh" } },
                new { code = "TR", name = "Turkey", cities = new[] { "Istanbul", "Ankara" } },
                new { code = "US", name = "United States", cities = new[] { "New York", "Chicago", "Dearborn" } }
            }
        };
    }

    private static object BuildThemeSettings(AppSettings settings) {
        return new {
            language = ResolveLanguage(settings.Language),
            themeMode = ResolveTheme(settings.ThemeMode),
            accentColor = "teal",
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
        return new {
            sounds = new[] {
                new { id = "adhan_default", label = T("Sound_Default"), selected = true, isCustom = false },
                new { id = "builtin_1", label = T("Sound_Builtin_1"), selected = false, isCustom = false },
                new { id = "builtin_2", label = T("Sound_Builtin_2"), selected = false, isCustom = false }
            },
            volume = (int)Math.Round(settings.Notifications.AdhanVolume * 100),
            calculationMethod = settings.Method.ToString(),
            madhhab = settings.Madhhab.ToString(),
            highLatitudeRule = settings.HighLatitudeRule.ToString(),
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
            clockFormat = settings.ClockFormat == ClockFormat.TwentyFourHour ? "24h" : "12h",
            fasting = new {
                iftarDelay = settings.FastingOffsets.IftarDelayMinutes,
                imsakAdvance = settings.FastingOffsets.ImsakAdvanceMinutes
            },
            imsakReminders = Array.Empty<object>(),
            iftarReminders = Array.Empty<object>(),
            perPrayerOverrides = new[] {
                new { prayer = T("Prayer_Fajr"), soundId = "adhan_default", vibration = "default" },
                new { prayer = T("Prayer_Dhuhr"), soundId = "adhan_default", vibration = "default" },
                new { prayer = T("Prayer_Asr"), soundId = "adhan_default", vibration = "default" },
                new { prayer = T("Prayer_Maghrib"), soundId = "adhan_default", vibration = "default" },
                new { prayer = T("Prayer_Isha"), soundId = "adhan_default", vibration = "default" }
            }
        };
    }

    private static object BuildNotificationSettings(AppSettings settings) {
        return new {
            enableAdhan = settings.Notifications.EnableAdhan,
            mobilePrimaryAdhanType = settings.Notifications.MobilePrimaryAdhanType.ToString(),
            hideOnCloseWindows = settings.Notifications.HideOnCloseOnWindows,
            runBackgroundServiceWindows = settings.Notifications.RunBackgroundServiceOnWindows,
            vibration = settings.Notifications.EnableVibration,
            vibrationStrength = settings.Notifications.VibrationStrength.ToString(),
            vibrationPattern = settings.Notifications.VibrationPattern.ToString(),
            minutesBefore = settings.Notifications.MinutesBefore,
            reminders = Array.Empty<object>()
        };
    }

    private async Task<object> BuildPermissionsSettingsAsync() {
        var snapshots = await _permissionCenter.GetSnapshotsAsync().ConfigureAwait(false);
        var alarm = await _alarmCapability.GetCurrentDecisionAsync().ConfigureAwait(false);
        return new {
            alarmMode = new {
                title = T("PermissionsAlarmModeTitle"),
                status = alarm.SupportStatus.ToString(),
                description = T("PermissionsSubtitle")
            },
            items = snapshots.Where(item => item.IsSupported).Select(item => new {
                id = item.Kind.ToString(),
                title = item.Kind.ToString(),
                role = item.IsCritical ? "critical" : "optional",
                description = item.Kind.ToString(),
                fallback = "",
                status = item.IsGranted ? "Granted" : "Denied",
                action = item.IsGranted ? T("PermissionAction_OpenSettings") : T("PermissionAction_Request")
            }).ToList()
        };
    }

    private static object BuildAlarmReminderSettings(AppSettings settings) {
        return new {
            builtIn = new[] {
                new { id = "wudu", text = "Make wudu before prayer", enabled = true },
                new { id = "qibla", text = "Face the Qibla", enabled = true }
            },
            userRemindersEnabled = true,
            userReminders = settings.AlarmReminders.UserItems.Select(item => new {
                id = item.Id,
                text = item.Text,
                enabled = item.IsEnabled
            }).ToList()
        };
    }

    private object BuildOnboardingSnapshot() {
        return new {
            language = ResolveLanguage(_settingsService.Load().Language),
            step = "location",
            title = T("OnboardingLocationTitle"),
            subtitle = T("OnboardingManualLocationHint"),
            permissions = Array.Empty<object>(),
            location = BuildLocationsSettings(_settingsService.Load())
        };
    }

    private static string T(string key) {
        return LocalizationManager.Translate(key);
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
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty(propertyName, out var property)) {
            return 0;
        }

        return property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var value)
            ? value
            : 0;
    }

    private static double NormalizeAngle(double angle) {
        var normalized = (angle % 360 + 360) % 360;
        return normalized > 180 ? 360 - normalized : normalized;
    }
}
