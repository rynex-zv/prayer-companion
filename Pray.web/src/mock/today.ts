import type { TestConfig } from "@/app/TEST";
import { translations, type Lang } from "./translations";

export function getTodayMock(state: TestConfig) {
  const labels = translations[state.language as Lang];
  const fmt = state.clockFormat;
  const t = (h: number, m: number) => {
    if (fmt === "24h") return `${String(h).padStart(2, "0")}:${String(m).padStart(2, "0")}`;
    const am = h < 12; const hh = h % 12 || 12;
    return `${hh}:${String(m).padStart(2, "0")} ${am ? "AM" : "PM"}`;
  };
  const timings = [
    { id: "fajr", name: labels.fajr, time: t(5, 12), isNext: false },
    { id: "sunrise", name: labels.sunrise, time: t(6, 47), isNext: false },
    { id: "dhuhr", name: labels.dhuhr, time: t(13, 28), isNext: false },
    { id: "asr", name: labels.asr, time: t(16, 5), isNext: true },
    { id: "maghrib", name: labels.maghrib, time: t(19, 52), isNext: false },
    { id: "isha", name: labels.isha, time: t(21, 18), isNext: false },
  ];
  return {
    locationTitle: `${state.city}, ${state.country}`,
    hijriDate: "15 Rabi' al-Awwal 1447",
    gregorianDate: "Tuesday, 30 June 2026",
    nextPrayerName: labels.asr,
    nextPrayerClock: t(16, 5),
    nextPrayerBaseClock: t(16, 0),
    showNextPrayerBaseClock: true,
    nextPrayerDayLabel: labels.today,
    countdown: "02:14:33",
    statusMessage: "",
    imsakTime: t(5, 2),
    iftarTime: t(19, 52),
    isImsakNext: false,
    isIftarNext: false,
    nextFastingCountdown: "06:21:18",
    isRtl: state.language === "ar",
    labels,
    todayTimings: timings,
  };
}
