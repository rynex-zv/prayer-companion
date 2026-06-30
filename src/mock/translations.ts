export const en: Record<string, string> = {
  today: "Today", calendar: "Calendar", qibla: "Qibla", tasbih: "Tasbih", settings: "Settings",
  nextPrayer: "Next prayer", fajr: "Fajr", sunrise: "Sunrise", dhuhr: "Dhuhr", asr: "Asr",
  maghrib: "Maghrib", isha: "Isha", imsak: "Imsak", iftar: "Iftar",
  basmala: "In the name of Allah, the Most Gracious, the Most Merciful",
  refresh: "Refresh", today_button: "Today", nextMonth: "Next month", prevMonth: "Previous month",
  qiblaDirection: "Qibla Direction", auto: "Auto", sensor: "Sensor", manual: "Manual",
  compass: "Compass", map: "Map", filter_none: "None", filter_night: "Night", filter_contrast: "Contrast",
  searching: "Searching for direction…", permissionMissing: "Location permission required",
  aligned: "Aligned with Qibla", grantPermission: "Grant permission",
  count: "Count", reset: "Reset", increment: "Tap to count", presets: "Presets",
  locations: "Locations", theme: "Theme & Diagnostics", adhan: "Adhan Customizations",
  notifications: "Notifications", permissions: "Permissions", alarmReminders: "Alarm Reminders",
  tasbihSettings: "Tasbih", about: "About",
  useGps: "Use GPS", refreshGps: "Refresh GPS", country: "Country", city: "City",
  latitude: "Latitude", longitude: "Longitude", vpnWarning: "VPN detected — location may be inaccurate",
  welcome: "Welcome", chooseLanguage: "Choose your language", next: "Next", finish: "Finish",
  setLocation: "Set your location", grantPermissions: "Grant permissions",
};

export const ar: Record<string, string> = {
  today: "اليوم", calendar: "التقويم", qibla: "القبلة", tasbih: "التسبيح", settings: "الإعدادات",
  nextPrayer: "الصلاة التالية", fajr: "الفجر", sunrise: "الشروق", dhuhr: "الظهر", asr: "العصر",
  maghrib: "المغرب", isha: "العشاء", imsak: "الإمساك", iftar: "الإفطار",
  basmala: "بِسْمِ اللَّهِ الرَّحْمَٰنِ الرَّحِيمِ",
  refresh: "تحديث", today_button: "اليوم", nextMonth: "الشهر التالي", prevMonth: "الشهر السابق",
  qiblaDirection: "اتجاه القبلة", auto: "تلقائي", sensor: "المستشعر", manual: "يدوي",
  compass: "البوصلة", map: "الخريطة", filter_none: "بدون", filter_night: "ليلي", filter_contrast: "تباين",
  searching: "جارٍ البحث عن الاتجاه…", permissionMissing: "يتطلب إذن الموقع",
  aligned: "متوافق مع القبلة", grantPermission: "منح الإذن",
  count: "العدد", reset: "تصفير", increment: "اضغط للعد", presets: "الإعدادات المسبقة",
  locations: "المواقع", theme: "السمة والتشخيص", adhan: "تخصيصات الأذان",
  notifications: "الإشعارات", permissions: "الأذونات", alarmReminders: "تذكيرات المنبه",
  tasbihSettings: "التسبيح", about: "حول",
  useGps: "استخدام GPS", refreshGps: "تحديث GPS", country: "الدولة", city: "المدينة",
  latitude: "خط العرض", longitude: "خط الطول", vpnWarning: "تم اكتشاف VPN — قد يكون الموقع غير دقيق",
  welcome: "مرحبًا", chooseLanguage: "اختر لغتك", next: "التالي", finish: "إنهاء",
  setLocation: "حدد موقعك", grantPermissions: "منح الأذونات",
};

export const fr = { ...en, today: "Aujourd'hui", calendar: "Calendrier", qibla: "Qibla", tasbih: "Tasbih", settings: "Paramètres", nextPrayer: "Prochaine prière" };
export const es = { ...en, today: "Hoy", calendar: "Calendario", settings: "Ajustes", nextPrayer: "Próxima oración" };
export const tr = { ...en, today: "Bugün", calendar: "Takvim", settings: "Ayarlar", nextPrayer: "Sonraki namaz" };

export type Lang = "en" | "ar" | "fr" | "es" | "tr";
export const translations: Record<Lang, Record<string, string>> = { en, ar, fr, es, tr };
