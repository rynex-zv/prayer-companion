import type { BridgeResponse } from "./mauiWebberClient";
import { pickAndStoreBrowserAdhanSound, playBrowserAdhanSound, removeBrowserAdhanSound } from "./browserAdhanSounds";

export type BrowserCoreCall = <T>(method: string, payload?: unknown) => Promise<BridgeResponse<T> | undefined>;

type PreparedLocation = { latitude: number; longitude: number; address: Awaited<ReturnType<typeof reverseAddress>> };
type PlatformPayload = {
  id?: string; latitude?: number; longitude?: number;
  _preparedLocation?: PreparedLocation;
  _preparedNotification?: NotificationPermission;
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
type ConfirmedLocation = { value?: LocationSettings; calculated?: LocationSettings };
type ConfirmedAdhan = { projection?: AdhanSettings; value?: AdhanSettings };
type AdhanSettings = {
  sounds?: AdhanSound[];
  volume?: number;
  [key: string]: unknown;
};
type AdhanSound = { id: string; label: string; selected: boolean; isCustom: boolean; canPreview?: boolean };
const SETTINGS_SNAPSHOT_METHOD = ["settings", "getSnapshot"].join(".");

export async function tryHandleWebPlatformCall<T = unknown>(
  method: string,
  payload?: unknown,
  coreCall?: BrowserCoreCall,
): Promise<BridgeResponse<T> | undefined> {
  if (typeof window === "undefined") {
    return undefined;
  }

  const request = payload as PlatformPayload | undefined;
  const permissionId = request?.id?.toLowerCase();
  const isPermissionSnapshot = method === "onboarding.getSnapshot" ||
    (method === SETTINGS_SNAPSHOT_METHOD && (payload as { section?: string } | undefined)?.section === "permissions");
  const handlesAction = method === "permissions.requestAll" ||
    method === "location.refresh" ||
    method === "location.reverseGeocode" ||
    method === "adhan.sound.addCustom" ||
    method === "adhan.sound.preview" ||
    method === "adhan.sound.removeCustom" ||
    isPermissionSnapshot ||
    (method === "permissions.request" && (!permissionId || permissionId === "location" || permissionId === "notifications"));
  if (!handlesAction) {
    return undefined;
  }

  if (!coreCall) throw new Error("Browser Core transaction is unavailable.");
  if (isPermissionSnapshot) {
    const snapshot = await coreCall<Record<string, unknown>>(method, payload);
    if (!snapshot?.ok) return snapshot as BridgeResponse<T> | undefined;
    return { ...snapshot, data: await applyBrowserPermissionState(snapshot.data) as T };
  }
  const labels = await loadWebLabels(coreCall);

  if (method === "permissions.request" && permissionId === "notifications") {
    return requestBrowserNotifications<T>(labels, request?._preparedNotification);
  }

  if (method === "permissions.requestAll") {
    return requestAllBrowserPermissions<T>(labels, coreCall, request);
  }

  if (method === "adhan.sound.addCustom") {
    return addBrowserCustomAdhanSound<T>(labels, coreCall);
  }

  if (method === "adhan.sound.preview") {
    return previewBrowserAdhanSound<T>(labels, coreCall, request?.id);
  }

  if (method === "adhan.sound.removeCustom") {
    return removeBrowserCustomAdhanSound<T>(labels, coreCall, request?.id);
  }

  if (
    method === "location.refresh" ||
    (method === "permissions.request" && (!permissionId || permissionId === "location"))
  ) {
    return refreshBrowserGps<T>(labels, coreCall, request?._preparedLocation);
  }

  if (method === "location.reverseGeocode") {
    return reverseGeocodeBrowserLocation<T>(labels, coreCall, request?.latitude, request?.longitude);
  }

  return undefined;
}

/** Performs user/browser/network waits before entering the serialized repository transaction. */
export async function prepareWebPlatformPayload(method: string, payload: unknown, coreCall: BrowserCoreCall): Promise<unknown> {
  if (typeof window === "undefined") return payload;
  const request = (payload && typeof payload === "object" ? payload : {}) as PlatformPayload;
  const permissionId = request.id?.toLowerCase();
  const needsLocation = method === "location.refresh" || method === "permissions.requestAll" ||
    (method === "permissions.request" && (!permissionId || permissionId === "location"));
  const needsNotification = method === "permissions.requestAll" ||
    (method === "permissions.request" && permissionId === "notifications");
  if (!needsLocation && !needsNotification) return payload;

  const prepared: PlatformPayload = { ...request };
  if (needsLocation) {
    const labels = await loadWebLabels(coreCall);
    if (!navigator.geolocation) throw new Error(label(labels, "webGeolocationUnavailable"));
    const position = await getCurrentBrowserPosition(labels);
    prepared._preparedLocation = {
      latitude: position.coords.latitude,
      longitude: position.coords.longitude,
      address: await reverseAddress(position.coords.latitude, position.coords.longitude),
    };
  }
  if (needsNotification && "Notification" in window) {
    prepared._preparedNotification = await Notification.requestPermission();
  }
  return prepared;
}

async function applyBrowserPermissionState(snapshot: Record<string, unknown>): Promise<Record<string, unknown>> {
  const locationGranted = await queryBrowserPermission("geolocation");
  const notificationGranted = "Notification" in window && Notification.permission === "granted";
  const permissions = snapshot.permissions ?? snapshot;
  const container = Array.isArray(permissions)
    ? { items: permissions as Array<Record<string, unknown>> }
    : permissions as { items?: Array<Record<string, unknown>> };
  if (!Array.isArray(container.items)) return snapshot;
  const items = container.items.map((item) => {
    const id = String(item.id ?? "").toLowerCase();
    const isGranted = id === "location" ? locationGranted : id === "notifications" ? notificationGranted : false;
    return { ...item, isGranted };
  });
  const updatedPermissions = { ...container, items };
  if (Array.isArray(snapshot.permissions)) return { ...snapshot, permissions: items };
  return snapshot.permissions ? { ...snapshot, permissions: updatedPermissions } : { ...snapshot, items };
}

async function queryBrowserPermission(name: PermissionName): Promise<boolean> {
  try {
    return !!navigator.permissions && (await navigator.permissions.query({ name })).state === "granted";
  } catch {
    return false;
  }
}

async function requestBrowserNotifications<T>(labels: WebLabels, prepared?: NotificationPermission): Promise<BridgeResponse<T>> {
  if (!("Notification" in window)) {
    return { ok: false, error: label(labels, "webNotificationsUnavailable") };
  }

  const permission = prepared ?? await withTimeout(Notification.requestPermission(), 15000, label(labels, "webNotificationPermissionTimedOut"));
  if (permission !== "granted") {
    return { ok: false, error: label(labels, "webNotificationPermissionDenied") };
  }

  return {
    ok: true,
    data: { ok: true, permission, platform: "web" } as T,
  };
}

async function refreshBrowserGps<T>(labels: WebLabels, coreCall: BrowserCoreCall, prepared?: PreparedLocation): Promise<BridgeResponse<T>> {
  if (!navigator.geolocation) {
    return { ok: false, error: label(labels, "webGeolocationUnavailable") };
  }

  const position: PreparedLocation = prepared ?? (await getCurrentBrowserPosition(labels).then(async (value) => ({
    latitude: value.coords.latitude,
    longitude: value.coords.longitude,
    address: await reverseAddress(value.coords.latitude, value.coords.longitude),
  })));
  const current = await coreCall<LocationSettings>(SETTINGS_SNAPSHOT_METHOD, { section: "locations" });
  if (!current?.ok) {
    return { ok: false, error: current?.error ?? label(labels, "webCoreLocationLoadFailed") };
  }

  const next: LocationSettings = {
    ...current.data,
    useGps: true,
    latitude: position.latitude,
    longitude: position.longitude,
    city: "",
    country: "",
    countryName: "",
  };

  const address = position.address;
  const finalLocation = {
    ...next,
    city: address?.city ?? "",
    country: address?.countryCode ?? "",
    countryName: address?.country ?? "",
  };
  const saved = await coreCall<ConfirmedLocation>("settings.update", {
    section: "locations",
    field: "value",
    value: finalLocation,
  });
  if (!saved?.ok) {
    return { ok: false, error: saved?.error ?? label(labels, "webLocationSaveFailed") };
  }

  return {
    ok: true,
    data: {
      ok: true,
      action: "refreshGps",
      platform: "web",
      location: saved.data.calculated ?? saved.data.value ?? finalLocation,
    } as T,
  };
}

async function reverseGeocodeBrowserLocation<T>(labels: WebLabels, coreCall: BrowserCoreCall, latitude?: number, longitude?: number): Promise<BridgeResponse<T>> {
  if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) {
    return { ok: false, error: label(labels, "webInvalidCoordinates") };
  }

  const current = await coreCall<LocationSettings>(SETTINGS_SNAPSHOT_METHOD, { section: "locations" });
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
  const saved = await coreCall<ConfirmedLocation>("settings.update", { section: "locations", field: "value", value: next });
  if (!saved?.ok) {
    return { ok: false, error: saved?.error ?? label(labels, "webLocationSaveFailed") };
  }

  return { ok: true, data: (saved.data.calculated ?? saved.data.value ?? next) as T };
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

async function requestAllBrowserPermissions<T>(labels: WebLabels, coreCall: BrowserCoreCall, prepared?: PlatformPayload): Promise<BridgeResponse<T>> {
  const results: string[] = [];
  let ok = true;

  const location = await refreshBrowserGps<unknown>(labels, coreCall, prepared?._preparedLocation);
  if (location.ok) {
    results.push(label(labels, "webLocationPermissionReady"));
  } else {
    ok = false;
    results.push(location.error || label(labels, "webLocationPermissionFailed"));
  }

  if ("Notification" in window) {
    const notification = await requestBrowserNotifications<unknown>(labels, prepared?._preparedNotification);
    if (notification.ok) {
      results.push(label(labels, "webNotificationPermissionReady"));
    } else {
      ok = false;
      results.push(notification.error || label(labels, "webNotificationPermissionFailed"));
    }
  } else {
    results.push(label(labels, "webNotificationsUnavailable"));
  }

  const data: Record<string, unknown> = {
      ok,
      action: "requestAllPermissions",
      platform: "web",
      message: results.join(" "),
  };

  if (!ok) {
    return { ok: false, error: results.join(" ") };
  }

  const snapshot = await coreCall<Record<string, unknown>>("onboarding.getSnapshot", {});
  if (snapshot?.ok) {
    const updated = await applyBrowserPermissionState(snapshot.data);
    const permissions = updated.permissions;
    const container = permissions as { items?: unknown[] } | undefined;
    data.permissions = permissions;
    data.items = Array.isArray(container?.items) ? container.items : Array.isArray(permissions) ? permissions : undefined;
  }

  return {
    ok: true,
    data: data as T,
  };
}

async function addBrowserCustomAdhanSound<T>(labels: WebLabels, coreCall: BrowserCoreCall): Promise<BridgeResponse<T>> {
  const picked = await pickAndStoreBrowserAdhanSound(labels);
  if (!picked) return { ok: true, data: { cancelled: true, platform: "web" } as T };

  const current = await coreCall<AdhanSettings>(SETTINGS_SNAPSHOT_METHOD, { section: "adhan" });
  if (!current?.ok) return { ok: false, error: current?.error ?? label(labels, "status_error") };

  const sounds = ensureDefaultSound(current.data.sounds ?? [])
    .filter((sound) => sound.id !== picked.id)
    .map((sound) => ({ ...sound, selected: false }));
  const next: AdhanSettings = {
    ...current.data,
    sounds: [...sounds, { id: picked.id, label: picked.label, selected: true, isCustom: true, canPreview: true }],
  };
  const saved = await coreCall<ConfirmedAdhan>("settings.update", { section: "adhan", field: "value", value: next });
  if (!saved?.ok) return { ok: false, error: saved?.error ?? label(labels, "status_error") };

  return { ok: true, data: { ok: true, platform: "web", sound: picked, projection: saved.data.projection ?? saved.data.value ?? next } as T };
}

async function previewBrowserAdhanSound<T>(labels: WebLabels, coreCall: BrowserCoreCall, id?: string): Promise<BridgeResponse<T>> {
  if (!id) return { ok: false, error: "Sound id is required." };
  const current = await coreCall<AdhanSettings>(SETTINGS_SNAPSHOT_METHOD, { section: "adhan" });
  const volume = current?.ok ? current.data.volume ?? 100 : 100;
  try {
    await playBrowserAdhanSound(id, volume);
    return { ok: true, data: { ok: true, platform: "web", id } as T };
  } catch (error) {
    return { ok: false, error: error instanceof Error ? error.message : label(labels, "status_error") };
  }
}

async function removeBrowserCustomAdhanSound<T>(labels: WebLabels, coreCall: BrowserCoreCall, id?: string): Promise<BridgeResponse<T>> {
  if (!id) return { ok: false, error: "Sound id is required." };
  await removeBrowserAdhanSound(id);
  const current = await coreCall<AdhanSettings>(SETTINGS_SNAPSHOT_METHOD, { section: "adhan" });
  if (!current?.ok) return { ok: false, error: current?.error ?? label(labels, "status_error") };

  let removedWasSelected = false;
  const remaining = ensureDefaultSound(current.data.sounds ?? []).filter((sound) => {
    if (sound.id !== id) return true;
    removedWasSelected = sound.selected;
    return false;
  });
  const selectedExists = remaining.some((sound) => sound.selected);
  const nextSounds = remaining.map((sound, index) => ({
    ...sound,
    selected: selectedExists ? sound.selected : removedWasSelected ? index === 0 : sound.selected,
  }));
  const next: AdhanSettings = { ...current.data, sounds: nextSounds };
  const saved = await coreCall<ConfirmedAdhan>("settings.update", { section: "adhan", field: "value", value: next });
  if (!saved?.ok) return { ok: false, error: saved?.error ?? label(labels, "status_error") };

  return { ok: true, data: { ok: true, platform: "web", projection: saved.data.projection ?? saved.data.value ?? next } as T };
}

function ensureDefaultSound(sounds: AdhanSound[]): AdhanSound[] {
  if (sounds.length) return sounds;
  return [{ id: "adhan_default", label: "Default", selected: true, isCustom: false, canPreview: true }];
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

async function loadWebLabels(coreCall: BrowserCoreCall): Promise<WebLabels> {
  const response = await coreCall<WebLabels>("app.getLocalization", {});
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
