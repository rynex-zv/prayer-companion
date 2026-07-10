import type { BridgeResponse } from "./mauiWebberClient";
import { tryCallWasmCore } from "./wasmCoreClient";

type SettingsInvokePayload = {
  action?: string;
  payload?: { id?: string };
};

type LocationSettings = {
  useGps: boolean;
  latitude: number;
  longitude: number;
  country?: string;
  countryName?: string;
  city?: string;
  vpnWarning?: boolean;
  qiblaReadingMode?: string;
  qiblaFilterMode?: string;
  qiblaReadingModes?: { id: string; label: string }[];
  qiblaFilterModes?: { id: string; label: string }[];
  countries?: { code: string; name: string; cities: string[] }[];
  places?: { country: string; countryCode: string; city: string; latitude: number; longitude: number }[];
};

export async function tryHandleWebPlatformCall<T = unknown>(
  method: string,
  payload?: unknown,
): Promise<BridgeResponse<T> | undefined> {
  if (typeof window === "undefined" || method !== "settings.invoke") {
    return undefined;
  }

  const request = payload as SettingsInvokePayload | undefined;
  const action = request?.action;
  const permissionId = request?.payload?.id?.toLowerCase();

  if (action === "requestPermission" && permissionId === "notifications") {
    return requestBrowserNotifications<T>();
  }

  if (action === "requestAllPermissions") {
    return requestAllBrowserPermissions<T>();
  }

  if (
    action === "refreshGps" ||
    (action === "requestPermission" && (!permissionId || permissionId === "location"))
  ) {
    return refreshBrowserGps<T>();
  }

  return undefined;
}

async function requestBrowserNotifications<T>(): Promise<BridgeResponse<T>> {
  if (!("Notification" in window)) {
    return { ok: false, error: "Browser notifications are not available." };
  }

  const permission = await withTimeout(Notification.requestPermission(), 15000, "Notification permission timed out.");
  if (permission !== "granted") {
    return { ok: false, error: "Notification permission was not granted." };
  }

  return {
    ok: true,
    data: { ok: true, permission, platform: "web" } as T,
  };
}

async function refreshBrowserGps<T>(): Promise<BridgeResponse<T>> {
  if (!navigator.geolocation) {
    return { ok: false, error: "Browser geolocation is not available. Enter the location manually." };
  }

  const position = await getCurrentBrowserPosition();
  const current = await tryCallWasmCore<LocationSettings>("settings.getSnapshot", { section: "locations" });
  if (!current?.ok) {
    return { ok: false, error: current?.error ?? "Web Core failed to load location settings." };
  }

  const next: LocationSettings = {
    ...current.data,
    useGps: true,
    latitude: position.coords.latitude,
    longitude: position.coords.longitude,
    city: current.data.city || "GPS location",
    country: current.data.country || "GPS",
    countryName: current.data.countryName || current.data.country || "GPS",
  };

  const saved = await tryCallWasmCore("settings.setField", {
    section: "locations",
    field: "value",
    value: next,
  });
  if (!saved?.ok) {
    return { ok: false, error: saved?.error ?? "Could not save browser GPS location." };
  }

  const refreshed = await tryCallWasmCore<LocationSettings>("settings.getSnapshot", { section: "locations" });
  if (!refreshed?.ok) {
    return { ok: false, error: refreshed?.error ?? "Could not refresh browser GPS location." };
  }

  return {
    ok: true,
    data: {
      ok: true,
      action: "refreshGps",
      platform: "web",
      location: refreshed.data,
    } as T,
  };
}

async function requestAllBrowserPermissions<T>(): Promise<BridgeResponse<T>> {
  const results: string[] = [];
  let ok = true;

  const location = await refreshBrowserGps<unknown>();
  if (location.ok) {
    results.push("Location permission is ready.");
  } else {
    ok = false;
    results.push(location.error || "Location permission failed.");
  }

  if ("Notification" in window) {
    const notification = await requestBrowserNotifications<unknown>();
    if (notification.ok) {
      results.push("Notification permission is ready.");
    } else {
      ok = false;
      results.push(notification.error || "Notification permission failed.");
    }
  } else {
    results.push("Browser notifications are not available.");
  }

  const data = {
      ok,
      action: "requestAllPermissions",
      platform: "web",
      message: results.join(" "),
  } as T;

  if (!ok) {
    return { ok: false, error: results.join(" ") };
  }

  return {
    ok: true,
    data,
  };
}

function getCurrentBrowserPosition(): Promise<GeolocationPosition> {
  return withTimeout(new Promise<GeolocationPosition>((resolve, reject) => {
    navigator.geolocation.getCurrentPosition(resolve, reject, {
      enableHighAccuracy: false,
      maximumAge: 300000,
      timeout: 12000,
    });
  }), 15000, "GPS refresh timed out. Allow location permission or enter the location manually.");
}

function withTimeout<T>(promise: Promise<T>, timeoutMs: number, message: string): Promise<T> {
  return new Promise((resolve, reject) => {
    const timeout = window.setTimeout(() => reject(new Error(message)), timeoutMs);
    promise.then(
      (value) => {
        window.clearTimeout(timeout);
        resolve(value);
      },
      (reason) => {
        window.clearTimeout(timeout);
        reject(isGeolocationError(reason)
          ? new Error(cleanGeolocationError(reason))
          : reason);
      },
    );
  });
}

function isGeolocationError(value: unknown): value is GeolocationPositionError {
  return typeof value === "object" &&
    value !== null &&
    "code" in value &&
    typeof (value as { code?: unknown }).code === "number";
}

function cleanGeolocationError(error: GeolocationPositionError): string {
  if (error.code === error.PERMISSION_DENIED) {
    return "Location permission was denied. Enter the location manually or allow location access.";
  }

  if (error.code === error.POSITION_UNAVAILABLE) {
    return "GPS location is unavailable. Enter the location manually.";
  }

  return "GPS refresh timed out. Enter the location manually or try again.";
}
