export type AutomationPlatform = "windows" | "android" | "web";
export const automationRoute = "/test";

function enabled(value: unknown): boolean {
  return String(value).toLowerCase() === "true";
}

export function automationPlatform(): AutomationPlatform {
  if (!window.mauiWebber) return "web";
  return /android/i.test(navigator.userAgent) ? "android" : "windows";
}

export function automationEnabled(): boolean {
  if (!enabled(import.meta.env.VITE_PRAY_AUTOMATION)) return false;
  if (!automationRouteActive()) return false;
  return automationPlatform() === "windows"
    ? enabled(import.meta.env.VITE_PRAY_AUTOMATION_WINDOWS)
    : automationPlatform() === "android"
      ? enabled(import.meta.env.VITE_PRAY_AUTOMATION_ANDROID)
      : enabled(import.meta.env.VITE_PRAY_AUTOMATION_WEB);
}

export function automationRouteActive(): boolean {
  const pathname = normalizeRoute(window.location.pathname);
  const hashRoute = normalizeRoute(window.location.hash.replace(/^#/, "").split("?")[0]);
  return pathname === automationRoute || hashRoute === automationRoute;
}

function normalizeRoute(route: string): string {
  const normalized = route.startsWith("/") ? route : `/${route}`;
  return normalized.replace(/\/+$/, "") || "/";
}

export const automationThresholds = {
  warningMs: 200,
  failureMs: 300,
};
