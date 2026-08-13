import { useSyncExternalStore } from "react";
import { executeCommand, nativeBackendReady, platformIntents } from "@/client/applicationClient";
import { appClient, type BootstrapResult } from "@/client/appClient";
import { readBrowserPermissionStates, watchBrowserPermissionChanges, type BrowserPermissionStates } from "@/native/webPlatformAdapter";
import bundledEnglishLabels from "../../../PrayAdFree/Resources/Raw/i18n/en.json";
import bundledArabicLabels from "../../../PrayAdFree/Resources/Raw/i18n/ar.json";
import bundledFrenchLabels from "../../../PrayAdFree/Resources/Raw/i18n/fr.json";
import bundledSpanishLabels from "../../../PrayAdFree/Resources/Raw/i18n/es.json";
import bundledTurkishLabels from "../../../PrayAdFree/Resources/Raw/i18n/tr.json";

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
  startupRoute: string;
  startupIntent?: string;
  languages: { code: string; name: string; direction?: Direction }[];
  settings: Record<string, unknown>;
  fieldSync: Record<string, FieldSync>;
  locationRuntime: {
    status: "idle" | "checking" | "refreshing" | "ready" | "choice-required" | "error";
    source?: "gps" | "ip" | "manual";
    error?: string;
    permissions?: BrowserPermissionStates;
  };
};

type ShellSnapshot = {
  language: string;
  isRtl: boolean;
  themeMode: "system" | "light" | "dark";
  accentColor?: string;
  textSize?: number;
  labels?: Record<string, string>;
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

type LocationSnapshot = {
  useGps?: boolean;
  latitude?: number;
  longitude?: number;
  country?: string;
  city?: string;
  locationSource?: "gps" | "ip" | "manual" | "";
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
  startupRoute: "/",
  startupIntent: undefined,
  languages: [],
  settings: {},
  fieldSync: {},
  locationRuntime: { status: "idle" },
};

let state = defaultState;
const listeners = new Set<() => void>();
let languageTarget = state.languageObject;
let systemThemeListenerAttached = false;
let permissionWatcherAttached = false;
let lastLocationPermission: BrowserPermissionStates["location"] | undefined;

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

export function retryBootstrapAppState(): Promise<void> {
  bootstrapPromise = undefined;
  updateState({ bootstrapStatus: "idle" });
  return bootstrapAppState();
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
  const bundledLabels = bundledLabelsByLanguage[backend.language] ?? bundledEnglishLabels;
  const backendLanguage = normalizeLanguageObject(backend.languageObject ?? {
    code: backend.language,
    direction: backend.isRtl ? "rtl" : "ltr",
    labels: backend.labels ?? bundledLabels,
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
    startupRoute: response.data.startup?.route ?? "/",
    startupIntent: response.data.startup?.intent,
    languages: backend.languages ?? state.languages,
    fieldSync: {
      ...state.fieldSync,
      "shell.bootstrap": { status: "saved", updatedAt: Date.now() },
    },
  });

  installPermissionWatcher();
  void synchronizeLocationAfterBootstrap();
}

async function synchronizeLocationAfterBootstrap(): Promise<"refreshed" | "blocked"> {
  return await refreshAutomaticLocation() ? "refreshed" : "blocked";
}

let resumePromise: Promise<void> | undefined;
export function resumeAppState(): Promise<void> {
  if (resumePromise) return resumePromise;
  resumePromise = (async () => {
    if (state.bootstrapStatus !== "ready") return;
    await synchronizeLocationAfterBootstrap();
  })().finally(() => { resumePromise = undefined; });
  return resumePromise;
}

let locationRefreshPromise: Promise<boolean> | undefined;

export function refreshAutomaticLocation(): Promise<boolean> {
  if (locationRefreshPromise) return locationRefreshPromise;
  locationRefreshPromise = refreshAppLocation("auto").finally(() => { locationRefreshPromise = undefined; });
  return locationRefreshPromise;
}

export async function refreshAppLocation(source: "auto" | "gps" | "ip"): Promise<boolean> {
  const previousRuntime = state.locationRuntime;
  const requestedSource = source === "auto" ? state.locationRuntime.source : source;
  updateState({ locationRuntime: { ...state.locationRuntime, status: "refreshing", source: requestedSource, error: undefined } });
  const response = await platformIntents.refreshLocation({ source });
  if (!response.ok) {
    // A transient resume/GPS failure must not discard a previously confirmed
    // location and replace the whole Today page with a source chooser.
    updateState({ locationRuntime: previousRuntime.status === "ready"
      ? { ...previousRuntime, status: "ready", error: undefined }
      : { ...state.locationRuntime, status: "error", source: requestedSource, error: response.error } });
    return false;
  }
  const payload = response.data as { location?: LocationSnapshot; changed?: boolean } | undefined;
  const location = payload?.location ?? response.data as LocationSnapshot | undefined;
  if (!nativeBackendReady() && (!location?.country || !location?.city)) {
    updateState({ locationRuntime: { ...state.locationRuntime, status: "error", source: requestedSource, error: getLabel("locationAddressUnavailable") } });
    return false;
  }
  const actualSource = location?.locationSource || (location?.useGps ? "gps" : source === "ip" ? "ip" : "manual");
  updateState({ locationRuntime: { ...state.locationRuntime, status: "ready", source: actualSource, error: undefined } });
  if (payload?.changed !== false) await refreshTodayForLocation();
  return true;
}

export async function confirmAppLocation(location: Pick<LocationSnapshot, "latitude" | "longitude">): Promise<boolean> {
  if (!hasUsableCoordinates(location.latitude, location.longitude)) {
    updateState({ locationRuntime: { ...state.locationRuntime, status: "choice-required" } });
    return false;
  }
  updateState({ locationRuntime: { ...state.locationRuntime, status: "ready", error: undefined } });
  await refreshTodayForLocation();
  return true;
}

async function refreshTodayForLocation(): Promise<void> {
  const refreshed = await appClient.command({ name: "today.refresh", domain: "today", projectionKey: "today.snapshot" });
  if (!refreshed.ok) {
    if (state.locationRuntime.status !== "ready") {
      updateState({ locationRuntime: { ...state.locationRuntime, status: "error", error: refreshed.error.message } });
    }
  }
}

function installPermissionWatcher() {
  if (permissionWatcherAttached || nativeBackendReady()) return;
  permissionWatcherAttached = true;
  watchBrowserPermissionChanges((permissions) => {
    const previous = lastLocationPermission;
    lastLocationPermission = permissions.location;
    updateState({ locationRuntime: { ...state.locationRuntime, permissions } });
    if (previous !== undefined && previous !== permissions.location) {
      void refreshAutomaticLocation();
    }
  });
}

function hasUsableCoordinates(latitude?: number, longitude?: number): boolean {
  return Number.isFinite(latitude) && Number.isFinite(longitude)
    && Math.abs(latitude ?? 0) <= 90 && Math.abs(longitude ?? 0) <= 180
    && (Math.abs(latitude ?? 0) > 0.000001 || Math.abs(longitude ?? 0) > 0.000001);
}

const bundledLabelsByLanguage: Record<string, Record<string, string>> = {
  en: bundledEnglishLabels,
  ar: bundledArabicLabels,
  fr: bundledFrenchLabels,
  es: bundledSpanishLabels,
  tr: bundledTurkishLabels,
};

export async function setLanguage(code: string): Promise<boolean> {
  markField("theme.language", "dirty");
  const response = await executeCommand<LanguageObject>("app.getLanguageObject", { language: code });
  if (!response.ok) {
    markField("theme.language", "error", response.error);
    return false;
  }

  setLanguageObject(response.data);
  return syncField("theme", "language", code);
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
    return JSON.stringify(canonicalValue(a)) === JSON.stringify(canonicalValue(b));
  } catch {
    return false;
  }
}

function canonicalValue(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(canonicalValue);
  if (value && typeof value === "object") {
    return Object.fromEntries(Object.entries(value as Record<string, unknown>)
      .sort(([left], [right]) => left.localeCompare(right))
      .map(([key, item]) => [key, canonicalValue(item)]));
  }
  return value;
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
