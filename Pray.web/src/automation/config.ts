export type AutomationPlatform = "windows" | "android" | "web";

function enabled(value: unknown): boolean {
  return String(value).toLowerCase() === "true";
}

export function automationPlatform(): AutomationPlatform {
  if (!window.mauiWebber) return "web";
  return /android/i.test(navigator.userAgent) ? "android" : "windows";
}

export function automationEnabled(): boolean {
  if (!enabled(import.meta.env.VITE_PRAY_AUTOMATION)) return false;
  return automationPlatform() === "windows"
    ? enabled(import.meta.env.VITE_PRAY_AUTOMATION_WINDOWS)
    : automationPlatform() === "android"
      ? enabled(import.meta.env.VITE_PRAY_AUTOMATION_ANDROID)
      : enabled(import.meta.env.VITE_PRAY_AUTOMATION_WEB);
}

export const automationThresholds = {
  warningMs: 200,
  failureMs: 300,
};
