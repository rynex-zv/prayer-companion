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
    private readonly PrayerDataService _dataService;
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
        PrayerDataService dataService,
        IAppPermissionCenterService permissionCenter,
        AndroidAlarmCapabilityService alarmCapability) {
        _today = today;
        _calendar = calendar;
        _qibla = qibla;
        _tasbih = tasbih;
        _settingsService = settingsService;
        _dataService = dataService;
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
            "mauiWebber.trace" => new { ok = true },
            "app.getShellSnapshot" => BuildShellSnapshot(),
            "app.getLocalization" => BuildLabels(),
            "app.setLanguage" => SetLanguage(payload),
            "app.setTheme" => SetTheme(payload),
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
            "settings.patch" => await PatchSettingsAsync(payload).ConfigureAwait(false),
            "settings.invoke" => await InvokeSettingsAsync(payload).ConfigureAwait(false),
            "onboarding.getSnapshot" => BuildOnboardingSnapshot(),
            "onboarding.complete" => CompleteOnboarding(),
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
            ["aligned"] = T("StayReady"),
            ["locations"] = T("SettingsLocations"),
            ["theme"] = T("Theme"),
            ["themeDiagnostics"] = T("SettingsDiagnostics"),
            ["adhan"] = T("SettingsAdhan"),
            ["notifications"] = T("SettingsNotifications"),
            ["permissions"] = T("SettingsPermissions"),
            ["alarmReminders"] = T("SettingsAlarmReminders"),
            ["tasbihSettings"] = T("SettingsTasbih"),
            ["about"] = T("About"),
            ["useGps"] = T("UseGps"),
            ["refreshGps"] = T("RefreshGps"),
            ["country"] = T("Country"),
            ["city"] = T("City"),
            ["latitude"] = T("Latitude"),
            ["longitude"] = T("Longitude"),
            ["language"] = T("Language"),
            ["themeMode"] = T("Theme"),
            ["system"] = T("System"),
            ["light"] = T("Light"),
            ["dark"] = T("Dark"),
            ["accentColor"] = T("AccentColor"),
            ["textSize"] = T("TextSize"),
            ["diagnostics"] = T("Diagnostics"),
            ["bridgeReady"] = T("BridgeReady"),
            ["lastSync"] = T("LastUpdated"),
            ["reset"] = T("Reset"),
            ["presets"] = T("Presets"),
            ["add"] = T("Add"),
            ["remove"] = T("Remove"),
            ["back"] = T("Back"),
            ["next"] = T("Next"),
            ["finish"] = T("Finish"),
            ["grantPermissions"] = T("PermissionAction_Request"),
            ["vpnWarning"] = T("OnboardingVpnWarning"),
            ["locationAndGps"] = T("Location"),
            ["themeLanguageAccent"] = T("ThemeMode"),
            ["soundAndCalculation"] = T("Calculation"),
            ["remindersAndVibration"] = T("AdhanReminders"),
            ["systemPermissions"] = T("PermissionsTitle"),
            ["alarmScreenReminders"] = T("AlarmRemindersTitle"),
            ["tasbihPresets"] = T("TasbihPresets"),
            ["appAndContactInfo"] = T("About"),
            ["adhanSound"] = T("AdhanSound"),
            ["addCustomSound"] = T("AddCustomAdhanSound"),
            ["testNotification"] = T("TestNotification"),
            ["testAlarm"] = T("TestAlarm"),
            ["volume"] = T("AdhanVolume"),
            ["calculation"] = T("Calculation"),
            ["method"] = T("Method"),
            ["madhhab"] = T("Madhhab"),
            ["highLatitudeRule"] = T("HighLatitude"),
            ["fajrAngle"] = T("FajrAngle"),
            ["ishaAngle"] = T("IshaAngle"),
            ["offsetsMinutes"] = T("Offsets"),
            ["clockFormat"] = T("ClockFormat"),
            ["iftarDelay"] = T("IftarDelay"),
            ["imsakAdvance"] = T("ImsakAdvance"),
            ["fastingReminders"] = T("FastingSettings"),
            ["imsakReminders"] = T("ImsakReminders"),
            ["iftarReminders"] = T("IftarReminders"),
            ["addReminder"] = T("Add"),
            ["perPrayerAdhan"] = T("AdhanPerPrayer"),
            ["enableAdhan"] = T("EnableAdhan"),
            ["primaryAdhanType"] = T("PrimaryAdhanType"),
            ["hideOnCloseWindows"] = T("WindowsHideOnClose"),
            ["runBackgroundWindows"] = T("WindowsBackgroundServiceEnable"),
            ["vibration"] = T("Vibration"),
            ["vibrationStrength"] = T("VibrationStrength"),
            ["vibrationPattern"] = T("VibrationPattern"),
            ["minutesBefore"] = T("MinutesBefore"),
            ["fallback"] = T("PermissionsSubtitle"),
            ["builtIn"] = T("AlarmRemindersBuiltIn"),
            ["yourReminders"] = T("AlarmRemindersUser"),
            ["newReminder"] = T("AlarmReminderNewPlaceholder"),
            ["tasbihPresetName"] = T("TasbihPresetName"),
            ["editPreset"] = T("AlarmReminderEdit"),
            ["repeatMode"] = T("TasbihRepeatMode"),
            ["items"] = T("TasbihItems"),
            ["itemText"] = T("TasbihText"),
            ["newPresetName"] = T("TasbihPresetNewName"),
            ["tagline"] = L("PrayerTimesTagline", "Prayer times, Qibla, and tasbih - ad free."),
            ["privacy"] = L("PrivacySummary", "We don't collect personal data. Everything stays on your device."),
            ["source"] = L("OpenSourceSummary", "Open source on GitHub."),
            ["maintainedBy"] = L("MaintainedBy", "Maintained by"),
            ["contact"] = L("SupportAndFeedback", "Support and feedback"),
            ["websiteNote"] = L("WebsiteNote", "Visit for updates and web version."),
            ["email"] = L("Email", "Email"),
            ["call"] = L("Call", "Call"),
            ["website"] = L("Website", "Website"),
            ["report"] = T("ReportIssue"),
            ["chooseLanguage"] = T("LanguageTitle"),
            ["permissionsIntro"] = T("PermissionsSubtitle"),
            ["permissionStatus"] = T("PermissionsTitle"),
            ["locationNoInternetGps"] = T("OnboardingLocationRequired"),
            ["locationNetwork"] = T("OnboardingLocationGranted"),
            ["locationGps"] = T("OnboardingManualLocationHint"),
            ["stepProgress"] = L("Step", "Step"),
            ["of"] = L("Of", "of"),
            ["minutes"] = T("Minutes"),
            ["hours"] = T("Hours"),
            ["before"] = T("Before"),
            ["after"] = T("After"),
            ["clock12h"] = T("Clock_12h"),
            ["clock24h"] = T("Clock_24h"),
            ["method_MuslimWorldLeague"] = T("Method_MuslimWorldLeague"),
            ["method_Egyptian"] = T("Method_Egypt"),
            ["method_Karachi"] = T("Method_Karachi"),
            ["method_UmmAlQura"] = T("Method_UmmAlQura"),
            ["method_Dubai"] = T("Method_Dubai"),
            ["method_Qatar"] = T("Method_Qatar"),
            ["method_Kuwait"] = T("Method_Kuwait"),
            ["method_MoonsightingCommittee"] = T("Method_Moonsighting"),
            ["method_NorthAmerica"] = T("Method_Isna"),
            ["method_Custom"] = T("Method_Custom"),
            ["madhhab_Shafi"] = T("Madhhab_Shafi"),
            ["madhhab_Hanafi"] = T("Madhhab_Hanafi"),
            ["highLatitude_MiddleOfTheNight"] = T("HighLatitude_MiddleOfTheNight"),
            ["highLatitude_SeventhOfTheNight"] = T("HighLatitude_SeventhOfTheNight"),
            ["highLatitude_TwilightAngle"] = T("HighLatitude_TwilightAngle"),
            ["reminderType_Full"] = T("ReminderType_Alarm"),
            ["reminderType_Notification"] = T("ReminderType_Notification"),
            ["reminderType_Silent"] = T("ReminderType_Silent"),
            ["vibration_Light"] = T("Vibration_Low"),
            ["vibration_Medium"] = T("Vibration_Medium"),
            ["vibration_Strong"] = T("Vibration_High"),
            ["vibration_Default"] = T("Vibration_Short"),
            ["vibration_Pulse"] = T("Vibration_Pulse"),
            ["vibration_Heartbeat"] = T("Vibration_Long"),
            ["tasbihRepeat_Sequence"] = T("TasbihRepeat_Continue"),
            ["tasbihRepeat_Loop"] = T("TasbihRepeat_Reset"),
            ["tasbihRepeat_Once"] = T("TasbihRepeat_None"),
            ["prayer_Fajr"] = T("Prayer_Fajr"),
            ["prayer_Sunrise"] = T("Prayer_Sunrise"),
            ["prayer_Dhuhr"] = T("Prayer_Dhuhr"),
            ["prayer_Asr"] = T("Prayer_Asr"),
            ["prayer_Maghrib"] = T("Prayer_Maghrib"),
            ["prayer_Isha"] = T("Prayer_Isha"),
            ["previousMonth"] = L("PreviousMonth", "Previous month"),
            ["nextMonth"] = L("NextMonth", "Next month"),
            ["todayBadge"] = T("Today"),
            ["resetToChangePreset"] = L("ResetToChangePreset", "Reset to change preset.")
        };
    }

    private static string L(string key, string fallback) {
        var value = T(key);
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
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

    private async Task<object?> PatchSettingsAsync(JsonElement payload) {
        var settings = _settingsService.Load();
        var next = settings;
        string? changedSection = null;

        if (TryGetObject(payload, "locations", out var locations)) {
            changedSection = "locations";
            next = CopySettings(next, location: PatchLocation(next.Location, locations));
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
        return changedSection == null
            ? await GetSettingsSnapshotAsync(payload).ConfigureAwait(false)
            : await GetSettingsSnapshotAsync(BuildSectionPayload(changedSection)).ConfigureAwait(false);
    }

    private async Task<object?> InvokeSettingsAsync(JsonElement payload) {
        var action = ReadString(payload, "action") ?? "";
        var actionPayload = TryGetObject(payload, "payload", out var p) ? p : default;

        switch (action) {
            case "addTasbihPreset":
            case "updateTasbihPreset":
            case "addTasbihItem":
            case "updateTasbihItem":
            case "moveTasbihItem":
            case "removeTasbihItem":
                PatchTasbih(action, actionPayload);
                return BuildTasbihSnapshot();
            case "requestAllPermissions":
            case "requestPermission":
                return await BuildPermissionsSettingsAsync().ConfigureAwait(false);
            default:
                return new { ok = true, action };
        }
    }

    private object CompleteOnboarding() {
        var settings = _settingsService.Load();
        SaveSettings(CopySettings(settings, onboardingCompleted: true));
        return BuildShellSnapshot();
    }

    private void SaveSettings(AppSettings settings) {
        _dataService.SaveSettings(settings);
    }

    private static LocationSettings PatchLocation(LocationSettings current, JsonElement payload) {
        var useGps = ReadBool(payload, "useGps", current.Mode == LocationMode.Gps);
        return new LocationSettings {
            Mode = useGps ? LocationMode.Gps : LocationMode.Manual,
            City = ReadString(payload, "city") ?? current.City,
            Country = current.Country,
            CountryCode = ReadString(payload, "country") ?? current.CountryCode,
            Latitude = ReadDouble(payload, "latitude", current.Latitude),
            Longitude = ReadDouble(payload, "longitude", current.Longitude),
            TimeZoneId = current.TimeZoneId,
            LastUpdatedUtc = DateTime.UtcNow
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
            ReminderScope = current.ReminderScope,
            ReminderPrayer = current.ReminderPrayer,
            ReminderItems = current.ReminderItems,
            ReminderOffsetsMinutes = current.ReminderOffsetsMinutes,
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
                    Name = ReadString(payload, "name") ?? T("Tasbih"),
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
            var prayerName = ReadString(item, "prayer") ?? "";
            if (!TryParsePrayer(prayerName, out var prayer)) {
                continue;
            }

            result.Add(new AdhanPrayerOverride {
                Prayer = prayer,
                SoundKey = ReadString(item, "soundId"),
                EnableVibration = ReadString(item, "vibration") switch {
                    "none" => false,
                    null => null,
                    _ => true
                }
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
                result.Add(minutes);
            }
        }

        return result;
    }

    private static JsonElement BuildSectionPayload(string section) {
        return JsonSerializer.SerializeToElement(new { section });
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
            "teal" => 0,
            "green" => 1,
            "blue" => 2,
            "amber" => 3,
            "rose" => 4,
            _ => fallback
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
