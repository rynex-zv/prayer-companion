import type { TestConfig } from "@/app/TEST";

export function getCalendarMock(state: TestConfig, _p?: { month?: string; offset?: number }) {
  const days = Array.from({ length: 30 }, (_, i) => {
    const d = i + 1;
    return {
      date: `${String(d).padStart(2, "0")} Jun 2026`,
      hijri: `${String((d + 14) % 30 || 30).padStart(2, "0")} Rabi' al-Awwal 1447`,
      fajr: "05:12", sunrise: "06:47", dhuhr: "13:28",
      asr: "16:05", maghrib: "19:52", isha: "21:18",
      isToday: d === 30,
    };
  });
  return {
    selectedMonth: "June 2026",
    selectedMonthValue: "2026-06",
    statusMessage: "",
    days,
    isRtl: state.language === "ar",
  };
}
