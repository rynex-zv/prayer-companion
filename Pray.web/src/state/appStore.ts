import { useSyncExternalStore } from "react";
import { executeCommand } from "@/client/applicationClient";
import { appClient, type BootstrapResult } from "@/client/appClient";
import bundledEnglishLabels from "../../../PrayAdFree/Resources/Raw/i18n/en.json";

export type Direction = "rtl" | "ltr";
export type SyncStatus = "clean" | "dirty" | "syncing" | "saved" | "error";

export type LanguageObject = {
  code: string;
  direction: Direction;
  labels: Record<string, string>;
  updatedAt: number;
};

export type FieldSync = {
  status: SyncStatus;
  updatedAt: number;
  error?: string;
};

export type AppState = {
  schemaVersion: 1;
  source: "default" | "storage";
  bootstrapStatus: "idle" | "ready" | "error";
  languageObject: LanguageObject;
  language: string;
  direction: Direction;
  themeMode: "system" | "light" | "dark";
  accentColor: string;
  textSize: number;
  onboardingCompleted: boolean;
  languages: { code: string; name: string; direction?: Direction }[];
  settings: Record<string, unknown>;
  fieldSync: Record<string, FieldSync>;
};

type ShellSnapshot = {
  language: string;
  isRtl: boolean;
  themeMode: "system" | "light" | "dark";
  accentColor?: string;
  textSize?: number;
  labels: Record<string, string>;
  languageObject?: LanguageObject;
  languages?: { code: string; name: string; direction?: Direction }[];
  onboardingCompleted: boolean;
};

type ConfirmedField<T = unknown> = {
  ok?: boolean;
  section: string;
  field: string;
  value: T;
  calculated?: Record<string, unknown>;
  error?: string;
};

const defaultLanguageObject: LanguageObject = {
  code: "en",
  direction: "ltr",
  labels: bundledEnglishLabels,
  updatedAt: 0,
};

const defaultState: AppState = {
  schemaVersion: 1,
  source: "default",
  bootstrapStatus: "idle",
  languageObject: defaultLanguageObject,
  language: defaultLanguageObject.code,
  direction: defaultLanguageObject.direction,
  themeMode: "system",
  accentColor: "teal",
  textSize: 100,
  onboardingCompleted: false,
  languages: [],
  settings: {},
  fieldSync: {},
};

let state = defaultState;
const listeners = new Set<() => void>();
let languageTarget = state.languageObject;
let systemThemeListenerAttached = false;

export const languageProxy = new Proxy({} as LanguageObject & Record<string, string>, {
  get(_target, prop) {
    if (prop === "code") return languageTarget.code;
    if (prop === "direction") return languageTarget.direction;
    if (prop === "labels") return languageTarget.labels;
    if (prop === "updatedAt") return languageTarget.updatedAt;
    if (typeof prop !== "string") return undefined;
    return languageTarget.labels[prop] ?? prop;
  },
});

applyDocumentState(state);

export function getAppState() {
  return state;
}

export function subscribeAppState(listener: () => void) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function useAppStore<T>(selector: (state: AppState) => T): T {
  const snapshot = useSyncExternalStore(
    subscribeAppState,
    getAppState,
    getAppState,
  );
  return selector(snapshot);
}

export function getLabel(key: string) {
  return languageTarget.labels[key] ?? bundledEnglishLabels[key as keyof typeof bundledEnglishLabels] ?? key;
}

export function setLanguageObject(languageObject: LanguageObject) {
  updateState({
    languageObject: normalizeLanguageObject(languageObject),
  });
}

let bootstrapPromise: Promise<void> | undefined;

export function bootstrapAppState(): Promise<void> {
  bootstrapPromise ??= performBootstrap();
  return bootstrapPromise;
}

async function performBootstrap() {
  const response = await appClient.bootstrap<BootstrapResult>({ domain: "app", projectionKey: "app.bootstrap" });
  if (!response.ok) {
    updateState({
      bootstrapStatus: "error",
      fieldSync: {
        ...state.fieldSync,
        "shell.bootstrap": { status: "error", updatedAt: Date.now(), error: response.error.message },
      },
    });
    return;
  }

  const backend = response.data.projections.shell as ShellSnapshot;
  const backendLanguage = normalizeLanguageObject(backend.languageObject ?? {
    code: backend.language,
    direction: backend.isRtl ? "rtl" : "ltr",
    labels: backend.labels,
    updatedAt: Date.now(),
  });

  updateState({
    source: "storage",
    bootstrapStatus: "ready",
    languageObject: backendLanguage,
    themeMode: backend.themeMode,
    accentColor: backend.accentColor ?? state.accentColor,
    textSize: backend.textSize ?? state.textSize,
    onboardingCompleted: backend.onboardingCompleted,
    languages: backend.languages ?? state.languages,
    fieldSync: {
      ...state.fieldSync,
      "shell.bootstrap": { status: "saved", updatedAt: Date.now() },
    },
  });
}

export async function setLanguage(code: string) {
  markField("theme.language", "dirty");
  const response = await executeCommand<LanguageObject>("app.getLanguageObject", { language: code });
  if (!response.ok) {
    markField("theme.language", "error", response.error);
    return;
  }

  setLanguageObject(response.data);
  await syncField("theme", "language", code);
}

export async function setThemeField<T>(field: "themeMode" | "accentColor" | "textSize", value: T) {
  updateState({ [field]: value } as Partial<AppState>);
  await syncField("theme", field, value);
}

export function setSettingsSection<T>(section: string, value: T) {
  updateState({
    settings: {
      ...state.settings,
      [section]: value,
    },
  });
}

export function setOnboardingCompleted(onboardingCompleted: boolean) {
  updateState({ onboardingCompleted });
}

export async function syncField<T>(section: string, field: string, value: T, retry = true) {
  const key = `${section}.${field}`;
  markField(key, "syncing");
  const response = await executeCommand<ConfirmedField<T>>("settings.update", { section, field, value });
  if (!response.ok) {
    markField(key, "error", response.error);
    return false;
  }

  if (!sameValue(response.data.value, value)) {
    if (retry) {
      return syncField(section, field, value, false);
    }

    console.error("Prayer Companion setting sync mismatch", { section, field, expected: value, actual: response.data.value });
    markField(key, "error", "confirmed value mismatch");
    return false;
  }

  markField(key, "saved");
  return true;
}

function sameValue(a: unknown, b: unknown) {
  if (Object.is(a, b)) {
    return true;
  }

  try {
    return JSON.stringify(a) === JSON.stringify(b);
  } catch {
    return false;
  }
}

function markField(key: string, status: SyncStatus, error?: string) {
  updateState({
    fieldSync: {
      ...state.fieldSync,
      [key]: { status, updatedAt: Date.now(), error },
    },
  });
}

function updateState(patch: Partial<AppState>) {
  const nextLanguageObject = patch.languageObject ? normalizeLanguageObject(patch.languageObject) : state.languageObject;
  state = {
    ...state,
    ...patch,
    languageObject: nextLanguageObject,
    language: nextLanguageObject.code,
    direction: nextLanguageObject.direction,
  };
  languageTarget = state.languageObject;
  applyDocumentState(state);
  listeners.forEach((listener) => listener());
}

function normalizeLanguageObject(value: LanguageObject): LanguageObject {
  return {
    code: value.code || "en",
    direction: value.direction === "rtl" ? "rtl" : "ltr",
    labels: value.labels ?? {},
    updatedAt: value.updatedAt || Date.now(),
  };
}

function applyDocumentState(value: AppState) {
  if (typeof document === "undefined") {
    return;
  }

  const root = document.documentElement;
  const prefersDark = typeof window !== "undefined" &&
    typeof window.matchMedia === "function" &&
    window.matchMedia("(prefers-color-scheme: dark)").matches;
  const isDark = value.themeMode === "dark" || (value.themeMode === "system" && prefersDark);
  const textScale = Math.min(1.5, Math.max(0.75, value.textSize / 100));

  root.dir = value.languageObject.direction;
  root.lang = value.languageObject.code;
  root.classList.toggle("dark", isDark);
  root.style.setProperty("--app-text-scale", `${textScale}`);
  root.style.setProperty("--app-family-font-size", `${16 * textScale}px`);
  root.dataset.accent = value.accentColor;
  document.body?.style.setProperty("--app-text-scale", `${textScale}`);
  document.body?.style.setProperty("--app-family-font-size", `${16 * textScale}px`);
  document.body?.dataset && (document.body.dataset.accent = value.accentColor);

  if (!systemThemeListenerAttached && typeof window !== "undefined" && typeof window.matchMedia === "function") {
    systemThemeListenerAttached = true;
    const media = window.matchMedia("(prefers-color-scheme: dark)");
    const updateSystemTheme = () => {
      applyDocumentState(state);
    };
    if (typeof media.addEventListener === "function") {
      media.addEventListener("change", updateSystemTheme);
    } else {
      media.addListener(updateSystemTheme);
    }
  }
}
