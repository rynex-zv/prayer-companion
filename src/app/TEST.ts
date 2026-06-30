// Single source of truth for browser/preview test configuration.
// Editing these values changes the mock data the whole app sees.
// In MAUI (when window.mauiWebber exists), this is ignored.

declare global {
  interface Window {
    mauiWebber?: {
      call: (method: string, payload?: unknown) => Promise<{ ok: boolean; data?: unknown; error?: string }>;
    };
  }
}

export type TestConfig = {
  enabled: boolean;
  country: string;
  city: string;
  language: "en" | "ar" | "fr" | "es" | "tr";
  theme: "light" | "dark" | "system";
  clockFormat: "12h" | "24h";
  qiblaState: "aligned" | "searching" | "noPermission" | "manual" | "map";
  permissionsScenario: "allGranted" | "partial" | "missingCritical";
  onboardingCompleted: boolean;
};

const hasMaui = typeof window !== "undefined" && !!window.mauiWebber;

export const TEST: TestConfig = {
  enabled: !hasMaui,
  country: "NL",
  city: "Amsterdam",
  language: "en",
  theme: "light",
  clockFormat: "12h",
  qiblaState: "aligned",
  permissionsScenario: "partial",
  onboardingCompleted: true,
};

export const BUILD_TARGET: "phone" | "web" =
  (import.meta.env.VITE_BUILD_TARGET as "phone" | "web" | undefined) ??
  (import.meta.env.MODE === "phone" ? "phone" : "web");

export const IS_PHONE = BUILD_TARGET === "phone" || hasMaui;
export const IS_MAUI = hasMaui;
