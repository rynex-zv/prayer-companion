import type { BridgeResponse } from "./mauiWebberClient";
import { tryCallWasmCore } from "./wasmCoreClient";

type SettingsInvokePayload = {
  action?: string;
  payload?: { id?: string; latitude?: number; longitude?: number };
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

type WebLabels = Record<string, string>;

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
  const handlesAction = action === "requestAllPermissions" ||
    action === "refreshGps" ||
    action === "reverseGeocode" ||
    (action === "requestPermission" && (!permissionId || permissionId === "location" || permissionId === "notifications"));
  if (!handlesAction) {
    return undefined;
  }

  const labels = await loadWebLabels();

  if (action === "requestPermission" && permissionId === "notifications") {
    return requestBrowserNotifications<T>(labels);
  }

  if (action === "requestAllPermissions") {
    return requestAllBrowserPermissions<T>(labels);
  }

  if (
    action === "refreshGps" ||
    (action === "requestPermission" && (!permissionId || permissionId === "location"))
  ) {
    return refreshBrowserGps<T>(labels);
  }

  if (action === "reverseGeocode") {
    return reverseGeocodeBrowserLocation<T>(labels, request?.payload?.latitude, request?.payload?.longitude);
  }

  return undefined;
}

async function requestBrowserNotifications<T>(labels: WebLabels): Promise<BridgeResponse<T>> {
  if (!("Notification" in window)) {
    return { ok: false, error: label(labels, "webNotificationsUnavailable") };
  }

  const permission = await withTimeout(Notification.requestPermission(), 15000, label(labels, "webNotificationPermissionTimedOut"));
  if (permission !== "granted") {
    return { ok: false, error: label(labels, "webNotificationPermissionDenied") };
  }

  return {
    ok: true,
    data: { ok: true, permission, platform: "web" } as T,
  };
}

async function refreshBrowserGps<T>(labels: WebLabels): Promise<BridgeResponse<T>> {
  if (!navigator.geolocation) {
    return { ok: false, error: label(labels, "webGeolocationUnavailable") };
  }

  const position = await getCurrentBrowserPosition(labels);
  const current = await tryCallWasmCore<LocationSettings>("settings.getSnapshot", { section: "locations" });
  if (!current?.ok) {
    return { ok: false, error: current?.error ?? label(labels, "webCoreLocationLoadFailed") };
  }

  const next: LocationSettings = {
    ...current.data,
    useGps: true,
    latitude: position.coords.latitude,
    longitude: position.coords.longitude,
    city: "",
    country: "",
    countryName: "",
  };

  const saved = await tryCallWasmCore("settings.setField", {
    section: "locations",
    field: "value",
    value: next,
  });
  if (!saved?.ok) {
    return { ok: false, error: saved?.error ?? label(labels, "webLocationSaveFailed") };
  }

  const address = await reverseAddress(position.coords.latitude, position.coords.longitude);
  if (address) {
    const calculated = await tryCallWasmCore<LocationSettings>("settings.getSnapshot", { section: "locations" });
    if (calculated?.ok) {
      await tryCallWasmCore("settings.setField", {
        section: "locations",
        field: "value",
        value: {
          ...calculated.data,
          useGps: true,
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          city: address.city,
          country: address.countryCode,
          countryName: address.country,
        },
      });
    }
  }

  const refreshed = await tryCallWasmCore<LocationSettings>("settings.getSnapshot", { section: "locations" });
  if (!refreshed?.ok) {
    return { ok: false, error: refreshed?.error ?? label(labels, "webLocationRefreshFailed") };
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

async function reverseGeocodeBrowserLocation<T>(labels: WebLabels, latitude?: number, longitude?: number): Promise<BridgeResponse<T>> {
  if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) {
    return { ok: false, error: label(labels, "webInvalidCoordinates") };
  }

  const current = await tryCallWasmCore<LocationSettings>("settings.getSnapshot", { section: "locations" });
  if (!current?.ok) {
    return { ok: false, error: current?.error ?? label(labels, "webCoreLocationLoadFailed") };
  }

  const address = await reverseAddress(latitude!, longitude!);
  const next = {
    ...current.data,
    useGps: false,
    latitude: latitude!,
    longitude: longitude!,
    city: address?.city ?? "",
    country: address?.countryCode ?? "",
    countryName: address?.country ?? "",
  };
  const saved = await tryCallWasmCore("settings.setField", { section: "locations", field: "value", value: next });
  if (!saved?.ok) {
    return { ok: false, error: saved?.error ?? label(labels, "webLocationSaveFailed") };
  }

  const refreshed = await tryCallWasmCore<LocationSettings>("settings.getSnapshot", { section: "locations" });
  return refreshed?.ok
    ? { ok: true, data: refreshed.data as T }
    : { ok: false, error: refreshed?.error ?? label(labels, "webLocationRefreshFailed") };
}

async function reverseAddress(latitude: number, longitude: number): Promise<{ city: string; country: string; countryCode: string } | null> {
  try {
    const url = new URL("https://nominatim.openstreetmap.org/reverse");
    url.searchParams.set("format", "jsonv2");
    url.searchParams.set("lat", String(latitude));
    url.searchParams.set("lon", String(longitude));
    url.searchParams.set("zoom", "10");
    url.searchParams.set("addressdetails", "1");
    const response = await fetch(url, { headers: { Accept: "application/json" } });
    if (!response.ok) return null;
    const payload = await response.json() as {
      address?: { city?: string; town?: string; village?: string; state?: string; country?: string; country_code?: string };
    };
    const address = payload.address;
    if (!address) return null;
    return {
      city: address.city ?? address.town ?? address.village ?? address.state ?? "",
      country: address.country ?? "",
      countryCode: (address.country_code ?? "").toUpperCase(),
    };
  } catch {
    return null;
  }
}

async function requestAllBrowserPermissions<T>(labels: WebLabels): Promise<BridgeResponse<T>> {
  const results: string[] = [];
  let ok = true;

  const location = await refreshBrowserGps<unknown>(labels);
  if (location.ok) {
    results.push(label(labels, "webLocationPermissionReady"));
  } else {
    ok = false;
    results.push(location.error || label(labels, "webLocationPermissionFailed"));
  }

  if ("Notification" in window) {
    const notification = await requestBrowserNotifications<unknown>(labels);
    if (notification.ok) {
      results.push(label(labels, "webNotificationPermissionReady"));
    } else {
      ok = false;
      results.push(notification.error || label(labels, "webNotificationPermissionFailed"));
    }
  } else {
    results.push(label(labels, "webNotificationsUnavailable"));
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

function getCurrentBrowserPosition(labels: WebLabels): Promise<GeolocationPosition> {
  return withTimeout(new Promise<GeolocationPosition>((resolve, reject) => {
    navigator.geolocation.getCurrentPosition(resolve, reject, {
      enableHighAccuracy: false,
      maximumAge: 300000,
      timeout: 12000,
    });
  }), 15000, label(labels, "webGpsTimedOut"), labels);
}

function withTimeout<T>(promise: Promise<T>, timeoutMs: number, message: string, labels?: WebLabels): Promise<T> {
  return new Promise((resolve, reject) => {
    const timeout = window.setTimeout(() => reject(new Error(message)), timeoutMs);
    promise.then(
      (value) => {
        window.clearTimeout(timeout);
        resolve(value);
      },
      (reason) => {
        window.clearTimeout(timeout);
        reject(isGeolocationError(reason) && labels
          ? new Error(cleanGeolocationError(reason, labels))
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

function cleanGeolocationError(error: GeolocationPositionError, labels: WebLabels): string {
  if (error.code === error.PERMISSION_DENIED) {
    return label(labels, "webLocationPermissionDenied");
  }

  if (error.code === error.POSITION_UNAVAILABLE) {
    return label(labels, "webGpsUnavailable");
  }

  return label(labels, "webGpsTimedOut");
}

async function loadWebLabels(): Promise<WebLabels> {
  const response = await tryCallWasmCore<WebLabels>("app.getLocalization", {});
  if (!response?.ok) {
    throw new Error(response?.error);
  }

  return response.data;
}

function label(labels: WebLabels, key: string): string {
  const value = labels[key];
  if (!value) {
    throw new Error(key);
  }

  return value;
}
