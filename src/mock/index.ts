import type { TestConfig } from "@/app/TEST";
import { TEST } from "@/app/TEST";
import { getTodayMock } from "./today";
import { getCalendarMock } from "./calendar";
import { getQiblaMock } from "./qibla";
import { getTasbihMock, tasbihIncrement, tasbihReset, tasbihSelectPreset } from "./tasbih";
import { getSettingsMock, patchSettings, invokeSettings } from "./settings";
import { getOnboardingMock } from "./onboarding";
import { translations, type Lang } from "./translations";

const state = { ...TEST };
let qiblaManualDelta = 0;

export type MockHandler = (payload?: unknown) => unknown | Promise<unknown>;

export const mockHandlers: Record<string, MockHandler> = {
  "app.getShellSnapshot": () => ({
    route: "/",
    language: state.language,
    isRtl: state.language === "ar",
    languageObject: getLanguageObject(state.language),
    languages: [
      { code: "en", name: "English", direction: "ltr" },
      { code: "ar", name: "العربية", direction: "rtl" },
    ],
    themeMode: state.theme,
    accentColor: "teal",
    tabs: [
      { id: "today", label: t(state, "today"), icon: "sun" },
      { id: "calendar", label: t(state, "calendar"), icon: "calendar" },
      { id: "qibla", label: t(state, "qibla"), icon: "compass" },
      { id: "tasbih", label: t(state, "tasbih"), icon: "circle" },
      { id: "settings", label: t(state, "settings"), icon: "settings" },
    ],
    labels: translations[state.language as Lang],
    onboardingCompleted: state.onboardingCompleted,
  }),

  "app.navigate": (p) => ({ navigatedTo: (p as { route?: string })?.route ?? "/" }),
  "app.getLocalization": () => translations[state.language as Lang],
  "app.getLanguageObject": (p) => getLanguageObject((p as TestConfig).language ?? state.language),
  "app.setLanguage": (p) => { state.language = (p as TestConfig).language ?? state.language; return { ok: true }; },
  "app.setTheme": (p) => { state.theme = (p as TestConfig).theme ?? state.theme; return { ok: true }; },
  "mauiWebber.getRemoteUrl": () => ({ url: "http://pray.rynex.nl/", defaultUrl: "http://pray.rynex.nl/" }),

  "today.getSnapshot": () => getTodayMock(state),
  "today.refresh": () => getTodayMock(state),

  "calendar.getSnapshot": (p) => getCalendarMock(state, p as { month?: string } | undefined),
  "calendar.setMonth": (p) => getCalendarMock(state, p as { month?: string }),
  "calendar.today": () => getCalendarMock(state),
  "calendar.nextMonth": () => getCalendarMock(state, { offset: 1 } as never),
  "calendar.previousMonth": () => getCalendarMock(state, { offset: -1 } as never),

  "qibla.getSnapshot": () => getQiblaMock(state, qiblaManualDelta),
  "qibla.setHeadingMode": (p) => { state.qiblaState = ((p as { mode?: string })?.mode === "manual" ? "manual" : "aligned") as TestConfig["qiblaState"]; return getQiblaMock(state, qiblaManualDelta); },
  "qibla.adjustManualHeading": (p) => { qiblaManualDelta += (p as { delta?: number })?.delta ?? 0; return getQiblaMock(state, qiblaManualDelta); },
  "qibla.commitManualHeading": () => getQiblaMock(state, qiblaManualDelta),
  "qibla.setDisplayMode": (p) => { state.qiblaState = (p as { mode?: string })?.mode === "map" ? "map" : state.qiblaState; return getQiblaMock(state, qiblaManualDelta); },
  "qibla.setVisualFilter": () => getQiblaMock(state, qiblaManualDelta),

  "tasbih.getSnapshot": () => getTasbihMock(),
  "tasbih.increment": () => { tasbihIncrement(); return getTasbihMock(); },
  "tasbih.reset": () => { tasbihReset(); return getTasbihMock(); },
  "tasbih.selectPreset": (p) => { tasbihSelectPreset((p as { id: string }).id); return getTasbihMock(); },

  "settings.getSnapshot": (p) => getSettingsMock((p as { section?: string })?.section),
  "settings.setField": (p) => {
    const payload = p as { section?: string; field?: string; value?: unknown };
    if (payload.section === "theme" && payload.field === "language") {
      state.language = String(payload.value ?? state.language) as TestConfig["language"];
      return { ok: true, section: payload.section, field: payload.field, value: state.language, languageObject: getLanguageObject(state.language) };
    }

    if (payload.section === "theme" && payload.field === "themeMode") {
      state.theme = String(payload.value ?? state.theme) as TestConfig["theme"];
    }

    if (payload.field === "value") {
      patchSettings({ [payload.section ?? ""]: payload.value });
    } else if (payload.section) {
      patchSettings({ [payload.section]: { [payload.field ?? "value"]: payload.value } });
    }

    return { ok: true, section: payload.section, field: payload.field, value: payload.value };
  },
  "settings.patch": (p) => patchSettings(p as Record<string, unknown>),
  "settings.invoke": (p) => invokeSettings(p as { action: string; payload?: unknown }),

  "onboarding.getSnapshot": () => getOnboardingMock(state),
  "onboarding.complete": () => { state.onboardingCompleted = true; return { ok: true }; },
};

function t(s: TestConfig, key: string): string {
  return translations[s.language as Lang]?.[key] ?? key;
}

function getLanguageObject(language: string) {
  const code = (language in translations ? language : "en") as Lang;
  return {
    code,
    direction: code === "ar" ? "rtl" : "ltr",
    labels: translations[code],
    updatedAt: Date.now(),
  };
}

export { translations };
