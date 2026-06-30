import { countries } from "./countries";

const settings = {
  locations: {
    useGps: false, latitude: 52.3676, longitude: 4.9041,
    country: "NL", city: "Amsterdam", vpnWarning: false,
    countries,
  },
  theme: {
    language: "en", themeMode: "system", accentColor: "teal",
    textSize: 100,
    diagnostics: { bridgeReady: true, lastSync: "Just now" },
    languages: [
      { code: "en", name: "English" }, { code: "ar", name: "العربية" },
      { code: "fr", name: "Français" }, { code: "es", name: "Español" }, { code: "tr", name: "Türkçe" },
    ],
    accentColors: ["teal", "green", "blue", "amber", "rose"],
  },
  adhan: {
    sounds: [
      { id: "makkah", label: "Makkah", selected: true, isCustom: false },
      { id: "madinah", label: "Madinah", selected: false, isCustom: false },
      { id: "abdul-basit", label: "Abdul Basit", selected: false, isCustom: false },
    ],
    volume: 80,
    calculationMethod: "MuslimWorldLeague",
    madhhab: "Shafi",
    highLatitudeRule: "MiddleOfTheNight",
    fajrAngle: 18, ishaAngle: 17, isCustomMethod: false,
    offsets: { fajr: 0, sunrise: 0, dhuhr: 0, asr: 0, maghrib: 0, isha: 0, imsak: 0 },
    clockFormat: "12h",
    fasting: { iftarDelay: 0, imsakAdvance: 10 },
    imsakReminders: [{ id: "1", value: 15, unit: "min", direction: "before" }],
    iftarReminders: [],
    perPrayerOverrides: [
      { prayer: "Fajr", soundId: "makkah", vibration: "default" },
      { prayer: "Dhuhr", soundId: "makkah", vibration: "default" },
    ],
  },
  notifications: {
    enableAdhan: true,
    mobilePrimaryAdhanType: "Full",
    hideOnCloseWindows: false,
    runBackgroundServiceWindows: true,
    vibration: true,
    vibrationStrength: "Medium",
    vibrationPattern: "Default",
    minutesBefore: 10,
    reminders: [],
  },
  permissions: {
    alarmMode: { title: "Exact alarms", status: "Granted", description: "Required for precise adhan timing" },
    items: [
      { id: "location", title: "Location", role: "critical", description: "For accurate prayer times", fallback: "Manual entry", status: "Granted", action: "Open settings" },
      { id: "notifications", title: "Notifications", role: "critical", description: "For adhan reminders", fallback: "Silent mode", status: "Granted", action: "Open settings" },
      { id: "background", title: "Background activity", role: "optional", description: "Reliable alarms", fallback: "Foreground only", status: "Denied", action: "Grant" },
    ],
  },
  alarmReminders: {
    builtIn: [
      { id: "wudu", text: "Make wudu before prayer", enabled: true },
      { id: "qibla", text: "Face the Qibla", enabled: true },
    ],
    userRemindersEnabled: true,
    userReminders: [{ id: "u1", text: "Read Quran after Fajr", enabled: true }],
  },
};

export function getSettingsMock(section?: string) {
  if (!section) return settings;
  return (settings as Record<string, unknown>)[section];
}
export function patchSettings(patch: Record<string, unknown>) {
  Object.assign(settings as Record<string, unknown>, patch);
  return { ok: true };
}
export function invokeSettings(_p: { action: string; payload?: unknown }) {
  return { ok: true };
}
