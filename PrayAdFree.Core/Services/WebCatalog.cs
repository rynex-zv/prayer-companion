using PrayAdFree.Core.Models;

namespace PrayAdFree.Core.Services;

public static class WebCatalog {
    public static IReadOnlyList<WebLanguageOption> Languages { get; } = new[] {
        new WebLanguageOption("en", "English", "ltr"),
        new WebLanguageOption("ar", "العربية", "rtl"),
        new WebLanguageOption("fr", "Français", "ltr"),
        new WebLanguageOption("es", "Español", "ltr"),
        new WebLanguageOption("tr", "Türkçe", "ltr")
    };

    public static IReadOnlyList<string> AccentColors { get; } = new[] {
        "teal", "green", "blue", "amber", "rose"
    };

    public static IReadOnlyList<WebShellTabOption> ShellTabs { get; } = new[] {
        new WebShellTabOption("today", "today", "sun"),
        new WebShellTabOption("calendar", "calendar", "calendar"),
        new WebShellTabOption("qibla", "qibla", "compass"),
        new WebShellTabOption("tasbih", "tasbih", "circle"),
        new WebShellTabOption("settings", "settings", "settings")
    };

    public static IReadOnlyList<WebLabeledOption> HeadingModes { get; } = new[] {
        new WebLabeledOption("auto", "auto"),
        new WebLabeledOption("manual", "manual")
    };

    public static IReadOnlyList<WebLabeledOption> QiblaReadingModes { get; } = new[] {
        new WebLabeledOption("compass", "compass"),
        new WebLabeledOption("map", "map")
    };

    public static IReadOnlyList<WebLabeledOption> QiblaFilterModes { get; } = new[] {
        new WebLabeledOption("none", "filter_none"),
        new WebLabeledOption("night", "filter_night"),
        new WebLabeledOption("contrast", "filter_contrast")
    };

    public static WebAdhanDefaults AdhanDefaults { get; } = new(
        Volume: 80,
        CalculationMethod: "Auto",
        Madhhab: "Shafi",
        HighLatitudeRule: "MiddleOfTheNight",
        FajrAngle: 18,
        IshaAngle: 17,
        ClockFormat: "24h");

    public static WebNotificationDefaults NotificationDefaults { get; } = new(
        EnableAdhan: true,
        MobilePrimaryAdhanType: "Full",
        HideOnCloseWindows: false,
        RunBackgroundServiceWindows: false,
        Vibration: false,
        VibrationStrength: "Medium",
        VibrationPattern: "Default",
        MinutesBefore: 10);

    public static IReadOnlyList<WebPlaceOption> Places { get; } = new[] {
        new WebPlaceOption("Netherlands", "NL", "Amsterdam", 52.3676, 4.9041),
        new WebPlaceOption("Netherlands", "NL", "Rotterdam", 51.9244, 4.4777),
        new WebPlaceOption("Netherlands", "NL", "Utrecht", 52.0907, 5.1214),
        new WebPlaceOption("Saudi Arabia", "SA", "Makkah", 21.3891, 39.8579),
        new WebPlaceOption("Saudi Arabia", "SA", "Madinah", 24.5247, 39.5692),
        new WebPlaceOption("Saudi Arabia", "SA", "Riyadh", 24.7136, 46.6753)
    };

    public static IReadOnlyList<WebCountryOption> Countries => Places
        .GroupBy(item => new { item.CountryCode, item.Country })
        .Select(group => new WebCountryOption(
            group.Key.CountryCode,
            group.Key.Country,
            group.Select(item => item.City).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item).ToArray()))
        .OrderBy(item => item.Name)
        .ToArray();

    public static IReadOnlyList<WebPermissionItem> BrowserPermissionItems { get; } = new[] {
        new WebPermissionItem("location", "Location", "critical", "Browser geolocation can be requested here.", "Manual entry", "Available", "Grant"),
        new WebPermissionItem("notifications", "Notifications", "critical", "Browser notifications can be requested here.", "In-app messages", "Available", "Grant"),
        new WebPermissionItem("background", "Background activity", "optional", "Background native alarms are not available in browser web.", "Foreground only", "Not available", "Unavailable")
    };

    public static WebAboutInfo AboutInfo { get; } = new(
        Name: "Pray Ad Free",
        Maintainer: "Rynex",
        Email: "rynex@rynex.nl",
        Phone: "+31610331734",
        Website: "https://pray.rynex.nl",
        RemoteWebUrl: WebStateDefaults.DefaultRemoteWebUrl);

    public static IReadOnlyList<WebAdhanSoundOption> DefaultAdhanSounds { get; } = new[] {
        new WebAdhanSoundOption("makkah", "Makkah", true, false, false)
    };

    public static IReadOnlyList<WebReminderOption> BuiltInAlarmReminders { get; } = new[] {
        new WebReminderOption("wudu", "Make wudu before prayer", true),
        new WebReminderOption("qibla", "Face the Qibla", true)
    };

    public static IReadOnlyDictionary<string, string> Labels(string language) =>
        IsRtl(language) ? ArabicLabels : EnglishLabels;

    public static string Translate(string language, string key) =>
        Labels(language).TryGetValue(key, out var value) ? value : key;

    public static bool IsRtl(string language) => string.Equals(NormalizeLanguage(language), "ar", StringComparison.Ordinal);

    public static string NormalizeLanguage(string? language) =>
        Languages.Any(item => string.Equals(item.Code, language, StringComparison.Ordinal)) ? language! : "en";

    public static string NormalizeTheme(string? theme) => theme is "light" or "dark" ? theme : "system";

    public static string NormalizeAccent(string? accent) =>
        AccentColors.Contains(accent ?? "", StringComparer.Ordinal) ? accent! : "teal";

    public static int ClampTextSize(int value) => Math.Clamp(value, 75, 150);

    public static object[] LocalizedOptions(IEnumerable<WebLabeledOption> options, string language) =>
        options.Select(item => new { id = item.Id, label = Translate(language, item.LabelKey) }).ToArray<object>();

    public static object[] LocalizedShellTabs(string language) =>
        ShellTabs.Select(item => new { id = item.Id, label = Translate(language, item.LabelKey), icon = item.Icon }).ToArray<object>();

    public static string QiblaDisplayLabel(string language, string readingMode) =>
        Translate(language, readingMode == "map" ? "map" : "compass");

    public static string QiblaFilterLabel(string language, string filterMode) =>
        Translate(language, filterMode switch { "night" => "filter_night", "contrast" => "filter_contrast", _ => "filter_none" });

    public static string NativeActionMessageKey(string action) => action switch {
        "requestPermission" => "webPermissionRequestHandled",
        "requestAllPermissions" => "webPermissionsRequestHandled",
        "refreshGps" => "webGpsHandledByAdapter",
        "addCustomAdhanSound" or "testNotification" or "previewSound" or "removeCustomAdhanSound" => "webNativeAdhanUnavailable",
        _ => "webNativeActionUnavailable"
    };

    public static WebPlaceOption? FindPlace(string? countryCode, string? country, string? city) {
        return Places.FirstOrDefault(item =>
            (string.Equals(item.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(item.Country, country, StringComparison.OrdinalIgnoreCase)) &&
            string.Equals(item.City, city, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly Dictionary<string, string> EnglishLabels = new() {
        ["today"] = "Today", ["tomorrow"] = "Tomorrow", ["calendar"] = "Calendar", ["qibla"] = "Qibla", ["tasbih"] = "Tasbih", ["settings"] = "Settings",
        ["nextPrayer"] = "Next prayer", ["fajr"] = "Fajr", ["sunrise"] = "Sunrise", ["dhuhr"] = "Dhuhr", ["asr"] = "Asr", ["maghrib"] = "Maghrib", ["isha"] = "Isha",
        ["prayer_Fajr"] = "Fajr", ["prayer_Sunrise"] = "Sunrise", ["prayer_Dhuhr"] = "Dhuhr", ["prayer_Asr"] = "Asr", ["prayer_Maghrib"] = "Maghrib", ["prayer_Isha"] = "Isha", ["prayer_Imsak"] = "Imsak",
        ["imsak"] = "Imsak", ["iftar"] = "Iftar", ["basmala"] = "In the name of Allah, the Most Gracious, the Most Merciful", ["aligned"] = "Aligned with Qibla",
        ["previousMonth"] = "Previous month", ["nextMonth"] = "Next month", ["load"] = "Load", ["todayBadge"] = "Today",
        ["auto"] = "Auto", ["manual"] = "Manual", ["compass"] = "Compass", ["map"] = "Map", ["filter_none"] = "None", ["filter_night"] = "Night", ["filter_contrast"] = "Contrast",
        ["qiblaDirection"] = "Qibla Direction", ["permissionMissing"] = "Location permission required", ["grantPermission"] = "Grant permission",
        ["status_ready"] = "Ready", ["status_saving"] = "Saving", ["status_saved"] = "Saved", ["status_error"] = "Error", ["status_refreshing"] = "Refreshing",
        ["locationAndGps"] = "Location and GPS", ["useGps"] = "Use GPS", ["refreshGps"] = "Refresh GPS", ["enabled"] = "Enabled", ["disabled"] = "Disabled", ["vpnWarning"] = "VPN detected; location may be inaccurate",
        ["locations"] = "Locations", ["country"] = "Country", ["city"] = "City", ["latitude"] = "Latitude", ["longitude"] = "Longitude",
        ["qiblaPreferences"] = "Qibla preferences", ["compassReadingMode"] = "Compass reading mode", ["compassFilter"] = "Compass filter",
        ["themeDiagnostics"] = "Theme & Diagnostics", ["themeLanguageAccent"] = "Theme, language, accent", ["language"] = "Language", ["themeMode"] = "Theme mode",
        ["system"] = "System", ["light"] = "Light", ["dark"] = "Dark", ["accentColor"] = "Accent color", ["textSize"] = "Text size", ["diagnostics"] = "Diagnostics",
        ["bridgeReady"] = "Bridge ready", ["lastSync"] = "Last sync", ["theme"] = "Theme",
        ["adhan"] = "Adhan customizations", ["soundAndCalculation"] = "Sound and calculation", ["notifications"] = "Notifications", ["remindersAndVibration"] = "Reminders and vibration",
        ["permissions"] = "Permissions", ["systemPermissions"] = "System permissions", ["alarmReminders"] = "Alarm reminders", ["alarmScreenReminders"] = "Alarm-screen reminders",
        ["tasbihSettings"] = "Tasbih", ["tasbihPresets"] = "Tasbih presets", ["about"] = "About", ["appAndContactInfo"] = "App and contact info",
        ["add"] = "Add", ["remove"] = "Remove", ["select"] = "Select", ["selected"] = "Selected", ["play"] = "Play", ["reset"] = "Reset", ["presets"] = "Presets",
        ["resetToChangePreset"] = "Reset to change preset", ["newPresetName"] = "New preset", ["tasbihPresetName"] = "Preset name", ["repeatMode"] = "Repeat mode",
        ["tasbihRepeat_Continue"] = "Continue", ["tasbihRepeat_Reset"] = "Reset", ["tasbihRepeat_None"] = "None", ["itemText"] = "Item text", ["targetCount"] = "Target count",
        ["startIndex"] = "Start index", ["moveUp"] = "Move up", ["moveDown"] = "Move down",
        ["welcome"] = "Welcome", ["chooseLanguage"] = "Choose your language", ["next"] = "Next", ["back"] = "Back", ["finish"] = "Finish",
        ["stepProgress"] = "Step", ["of"] = "of", ["permissionStatus"] = "Permission status", ["permissionsIntro"] = "Enable browser permissions when available.",
        ["grantPermissions"] = "Grant permissions", ["locationNoInternetGps"] = "Use manual location when GPS or network is unavailable.", ["locationNetwork"] = "Network location can help find nearby prayer times.",
        ["locationGps"] = "GPS can improve accuracy when the browser allows it.",
        ["tagline"] = "Prayer times, Qibla, and tasbih - ad free.", ["privacy"] = "We don't collect personal data. Everything stays on your device.", ["source"] = "Open source on GitHub.",
        ["contact"] = "Support and feedback", ["websiteNote"] = "Visit for updates and web version.", ["maintainedBy"] = "Maintained by", ["report"] = "Report issue",
        ["emailRynex"] = "Email Rynex", ["callRynex"] = "Call", ["openWebsite"] = "Open website", ["pullLatestWebVersion"] = "Pull latest web version",
        ["pulling"] = "Pulling...", ["remoteWebBundleUrl"] = "Remote web bundle URL", ["save"] = "Save", ["resetToDefault"] = "Reset to default",
        ["savingRemoteWebUrl"] = "Saving remote web URL...", ["invalidRemoteWebUrl"] = "Invalid remote web URL.", ["remoteWebUrlSaved"] = "Remote web URL saved",
        ["pullingLatestWebVersion"] = "Pulling latest web version...", ["webUpdateFailed"] = "Web update failed.", ["lastPulledVersion"] = "Last pulled version",
        ["sameVersion"] = "Same version.", ["pulledLatestWebVersion"] = "Pulled latest web version.", ["unknown"] = "unknown",
        ["webPermissionRequestHandled"] = "Use browser permission prompts where available; native permissions are not required in browser web.",
        ["webPermissionsRequestHandled"] = "Browser permissions are requested only when a web API needs them.",
        ["webGpsHandledByAdapter"] = "Browser location is handled by the web adapter; enter the location manually if geolocation is unavailable.",
        ["webNativeAdhanUnavailable"] = "Native adhan sound actions are not available in browser web.",
        ["webNativeActionUnavailable"] = "This native action is not available in browser web.",
        ["webRemotePullUnavailable"] = "Remote bundle pull is only available inside the phone or Windows app.",
        ["webEmbeddedResetUnavailable"] = "Embedded bundle reset is only available inside the phone or Windows app.",
        ["webExactAlarms"] = "Exact alarms",
        ["webExactAlarmsUnavailable"] = "Not available on web",
        ["webExactAlarmsDescription"] = "Native exact alarms require the phone or Windows app.",
        ["webCoreLastSync"] = "WASM core",
        ["adhanSound"] = "Adhan sound", ["addCustomSound"] = "Add custom sound", ["testNotification"] = "Test notification", ["volume"] = "Volume",
        ["calculation"] = "Calculation", ["method"] = "Method", ["madhhab"] = "Madhhab", ["highLatitudeRule"] = "High-latitude rule", ["fajrAngle"] = "Fajr angle", ["ishaAngle"] = "Isha angle",
        ["offsetsMinutes"] = "Offsets in minutes", ["fastingReminders"] = "Fasting reminders", ["iftarDelay"] = "Iftar delay", ["imsakAdvance"] = "Imsak advance", ["clockFormat"] = "Clock format",
        ["clock12h"] = "12-hour", ["clock24h"] = "24-hour", ["imsakReminders"] = "Imsak reminders", ["iftarReminders"] = "Iftar reminders", ["perPrayerAdhan"] = "Per-prayer adhan",
        ["useGlobal"] = "Use global", ["vibration"] = "Vibration", ["minutes"] = "Minutes", ["hours"] = "Hours", ["before"] = "Before", ["after"] = "After",
        ["newReminderText"] = "Reminder value", ["unit"] = "Unit", ["direction"] = "Direction", ["enableAdhan"] = "Enable adhan", ["primaryAdhanType"] = "Primary adhan type",
        ["minutesBefore"] = "Minutes before", ["vibrationStrength"] = "Vibration strength", ["vibrationPattern"] = "Vibration pattern",
        ["vibration_Light"] = "Light", ["vibration_Medium"] = "Medium", ["vibration_Strong"] = "Strong", ["vibration_Default"] = "Default", ["vibration_Pulse"] = "Pulse", ["vibration_Heartbeat"] = "Heartbeat", ["vibration_Long"] = "Long",
        ["hideOnCloseWindows"] = "Hide on close in Windows", ["runBackgroundWindows"] = "Run background service in Windows", ["windowsBackgroundServiceHint"] = "Windows background service is native-only.",
        ["adhanReminders"] = "Adhan reminders", ["scope"] = "Scope", ["prayer"] = "Prayer", ["alertType"] = "Alert type", ["testAlarm"] = "Test alarm",
        ["reminder_All"] = "All prayers", ["reminder_SpecificPrayer"] = "Specific prayer", ["reminderType_Alarm"] = "Alarm", ["reminderType_Adhan"] = "Adhan", ["reminderType_Notification"] = "Notification", ["reminderType_Silent"] = "Silent",
        ["builtIn"] = "Built-in", ["yourReminders"] = "Your reminders", ["newReminder"] = "New reminder",
        ["method_Auto"] = "Auto", ["method_Jafari"] = "Jafari", ["method_Karachi"] = "Karachi", ["method_Isna"] = "ISNA", ["method_MuslimWorldLeague"] = "Muslim World League",
        ["method_UmmAlQura"] = "Umm Al-Qura", ["method_Egypt"] = "Egypt", ["method_Tehran"] = "Tehran", ["method_Gulf"] = "Gulf", ["method_Kuwait"] = "Kuwait",
        ["method_Qatar"] = "Qatar", ["method_Singapore"] = "Singapore", ["method_France"] = "France", ["method_Turkey"] = "Turkey", ["method_Russia"] = "Russia",
        ["method_Moonsighting"] = "Moonsighting", ["method_Dubai"] = "Dubai", ["method_Jakim"] = "JAKIM", ["method_Tunisia"] = "Tunisia", ["method_Algeria"] = "Algeria",
        ["method_Kemenag"] = "Kemenag", ["method_Morocco"] = "Morocco", ["method_Portugal"] = "Portugal", ["method_Jordan"] = "Jordan", ["method_Custom"] = "Custom",
        ["madhhab_Shafi"] = "Shafi", ["madhhab_Maliki"] = "Maliki", ["madhhab_Hanbali"] = "Hanbali", ["madhhab_Hanafi"] = "Hanafi",
        ["highLatitude_MiddleOfTheNight"] = "Middle of the night", ["highLatitude_SeventhOfTheNight"] = "Seventh of the night", ["highLatitude_TwilightAngle"] = "Twilight angle",
        ["cardinalNorth"] = "N", ["cardinalEast"] = "E", ["cardinalSouth"] = "S", ["cardinalWest"] = "W",
        ["TasbihPreset_AfterPrayer"] = "After Prayer", ["TasbihPreset_Hundred"] = "100x Subhan Allah", ["TasbihPreset_Salawat"] = "100x Salawat",
        ["Tasbih_SubhanAllah"] = "Subhan Allah", ["Tasbih_Alhamdulillah"] = "Alhamdulillah", ["Tasbih_AllahuAkbar"] = "Allahu Akbar",
        ["Tasbih_Salawat"] = "Allahumma salli ala Muhammad", ["Tasbih_Astaghfirullah"] = "Astaghfirullah", ["Tasbih_LaIlahaIllaAllah"] = "La ilaha illa Allah"
    };

    private static readonly Dictionary<string, string> ArabicLabels = new(EnglishLabels) {
        ["today"] = "اليوم", ["tomorrow"] = "غدا", ["calendar"] = "التقويم", ["qibla"] = "القبلة", ["tasbih"] = "التسبيح", ["settings"] = "الإعدادات",
        ["nextPrayer"] = "الصلاة التالية", ["fajr"] = "الفجر", ["sunrise"] = "الشروق", ["dhuhr"] = "الظهر", ["asr"] = "العصر", ["maghrib"] = "المغرب", ["isha"] = "العشاء",
        ["prayer_Fajr"] = "الفجر", ["prayer_Sunrise"] = "الشروق", ["prayer_Dhuhr"] = "الظهر", ["prayer_Asr"] = "العصر", ["prayer_Maghrib"] = "المغرب", ["prayer_Isha"] = "العشاء", ["prayer_Imsak"] = "الإمساك",
        ["imsak"] = "الإمساك", ["iftar"] = "الإفطار", ["aligned"] = "متوافق مع القبلة", ["auto"] = "تلقائي", ["manual"] = "يدوي", ["compass"] = "البوصلة", ["map"] = "الخريطة",
        ["previousMonth"] = "الشهر السابق", ["nextMonth"] = "الشهر التالي", ["load"] = "تحميل", ["todayBadge"] = "اليوم",
        ["themeDiagnostics"] = "السمة والتشخيص", ["themeLanguageAccent"] = "السمة واللغة واللون", ["language"] = "اللغة", ["themeMode"] = "وضع السمة",
        ["system"] = "النظام", ["light"] = "فاتح", ["dark"] = "داكن", ["accentColor"] = "لون التمييز", ["textSize"] = "حجم النص", ["diagnostics"] = "التشخيص",
        ["bridgeReady"] = "الجسر جاهز", ["lastSync"] = "آخر مزامنة", ["locationAndGps"] = "الموقع وGPS", ["useGps"] = "استخدام GPS", ["refreshGps"] = "تحديث GPS",
        ["enabled"] = "مفعل", ["disabled"] = "معطل", ["locations"] = "المواقع", ["country"] = "الدولة", ["city"] = "المدينة", ["latitude"] = "خط العرض", ["longitude"] = "خط الطول",
        ["qiblaPreferences"] = "تفضيلات القبلة", ["compassReadingMode"] = "طريقة قراءة البوصلة", ["compassFilter"] = "فلتر البوصلة",
        ["adhan"] = "تخصيصات الأذان", ["notifications"] = "الإشعارات", ["permissions"] = "الأذونات", ["systemPermissions"] = "أذونات النظام",
        ["alarmReminders"] = "تذكيرات المنبه", ["tasbihSettings"] = "التسبيح", ["tasbihPresets"] = "إعدادات التسبيح", ["about"] = "حول",
        ["add"] = "إضافة", ["remove"] = "حذف", ["select"] = "اختيار", ["selected"] = "محدد", ["play"] = "تشغيل", ["reset"] = "تصفير", ["presets"] = "الإعدادات",
        ["newPresetName"] = "إعداد جديد", ["tasbihPresetName"] = "اسم الإعداد", ["repeatMode"] = "وضع التكرار", ["itemText"] = "النص", ["targetCount"] = "العدد",
        ["moveUp"] = "رفع", ["moveDown"] = "خفض", ["welcome"] = "مرحبا", ["chooseLanguage"] = "اختر لغتك", ["next"] = "التالي", ["back"] = "رجوع", ["finish"] = "إنهاء",
        ["maintainedBy"] = "بإدارة", ["report"] = "إبلاغ عن مشكلة", ["emailRynex"] = "راسل Rynex", ["callRynex"] = "اتصال", ["openWebsite"] = "افتح الموقع",
        ["pullLatestWebVersion"] = "اسحب آخر نسخة ويب", ["pulling"] = "جار السحب...", ["remoteWebBundleUrl"] = "رابط حزمة الويب", ["save"] = "حفظ", ["resetToDefault"] = "إعادة الافتراضي",
        ["webPermissionRequestHandled"] = "استخدم أذونات المتصفح عند توفرها؛ أذونات النظام الأصلية غير مطلوبة في الويب.",
        ["webPermissionsRequestHandled"] = "يتم طلب أذونات المتصفح فقط عندما تحتاجها واجهة ويب.",
        ["webGpsHandledByAdapter"] = "موقع المتصفح يتم التعامل معه من adapter الويب؛ أدخل الموقع يدويا إذا لم يتوفر GPS.",
        ["webNativeAdhanUnavailable"] = "إجراءات أصوات الأذان الأصلية غير متوفرة في نسخة المتصفح.",
        ["webNativeActionUnavailable"] = "هذا الإجراء الأصلي غير متوفر في نسخة المتصفح.",
        ["webRemotePullUnavailable"] = "سحب حزمة الويب متوفر فقط داخل تطبيق الهاتف أو Windows.",
        ["webEmbeddedResetUnavailable"] = "إعادة الحزمة المدمجة متوفرة فقط داخل تطبيق الهاتف أو Windows.",
        ["webExactAlarms"] = "المنبهات الدقيقة",
        ["webExactAlarmsUnavailable"] = "غير متوفر على الويب",
        ["webExactAlarmsDescription"] = "المنبهات الأصلية الدقيقة تحتاج تطبيق الهاتف أو Windows.",
        ["webCoreLastSync"] = "Core عبر WASM",
        ["reminderType_Alarm"] = "منبه",
        ["vibration_Heartbeat"] = "نبضات",
        ["TasbihPreset_AfterPrayer"] = "بعد الصلاة", ["TasbihPreset_Hundred"] = "100 تسبيحة", ["TasbihPreset_Salawat"] = "100 صلاة على النبي",
        ["Tasbih_SubhanAllah"] = "سبحان الله", ["Tasbih_Alhamdulillah"] = "الحمد لله", ["Tasbih_AllahuAkbar"] = "الله أكبر",
        ["Tasbih_Salawat"] = "اللهم صل على محمد", ["Tasbih_Astaghfirullah"] = "أستغفر الله", ["Tasbih_LaIlahaIllaAllah"] = "لا إله إلا الله"
    };
}
