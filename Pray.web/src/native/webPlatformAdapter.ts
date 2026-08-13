import type { BridgeResponse } from "./mauiWebberClient";
import { pickAndStoreBrowserAdhanSound, playBrowserAdhanSound, removeBrowserAdhanSound, stopActiveBrowserAdhanSound } from "./browserAdhanSounds";
import { automationRuntimeActive } from "../automation/config";
import { canReuseConfirmedGpsLocation, resolveAutomaticLocationSource } from "./locationResumePolicy";

export type BrowserCoreCall = <T>(method: string, payload?: unknown) => Promise<BridgeResponse<T> | undefined>;

type PreparedLocation = {
  source: "gps" | "ip";
  latitude: number;
  longitude: number;
  timeZoneId?: string;
  locationSource?: "gps" | "ip" | "manual" | "";
  address: Awaited<ReturnType<typeof reverseAddress>>;
};
type PlatformPayload = {
  id?: string; latitude?: number; longitude?: number; source?: "auto" | "gps" | "ip";
  operationId?: string; to?: string; number?: string; url?: string;
  _preparedLocation?: PreparedLocation;
  _preparedNotification?: NotificationPermission;
};

type LocationSettings = {
  useGps: boolean;
  latitude: number;
  longitude: number;
  timeZoneId?: string;
  locationSource?: "gps" | "ip" | "manual" | "";
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
    method === "permissions.request" ||
    method === "location.refresh" ||
    method === "location.reverseGeocode" ||
    method === "adhan.sound.addCustom" ||
    method === "adhan.sound.preview" ||
    method === "adhan.sound.stopPreview" ||
    method === "adhan.sound.removeCustom" ||
    method === "alarm.test" ||
    method === "notification.test" ||
    method.startsWith("external.") ||
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

  if (method === "permissions.request" && permissionId !== "location") {
    return { ok: false, error: label(labels, "webNativeActionUnavailable") };
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

  if (method === "adhan.sound.stopPreview") {
    stopActiveBrowserAdhanSound();
    return { ok: true, data: { ok: true, platform: "web" } as T };
  }

  if (method === "adhan.sound.removeCustom") {
    return removeBrowserCustomAdhanSound<T>(labels, coreCall, request?.id);
  }

  if (method === "notification.test") {
    return testBrowserNotification<T>(labels);
  }

  if (method === "alarm.test") {
    return { ok: false, error: label(labels, "webNativeActionUnavailable") };
  }

  if (method.startsWith("external.")) {
    return launchBrowserExternalIntent<T>(method, request, labels);
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
    let source = request.source;
    if (method === "location.refresh" && (!source || source === "auto")) {
      const permissions = await readBrowserPermissionStates();
      if (permissions.location === "granted") {
        source = "gps";
      } else if (permissions.location === "denied") {
        source = "ip";
      } else {
        // Some mobile browsers briefly report `prompt` or do not implement
        // Permissions.query after a tab resumes. A previously confirmed GPS
        // preference is stronger evidence than that transient API result.
        const current = await coreCall<LocationSettings>(SETTINGS_SNAPSHOT_METHOD, { section: "locations" });
        source = resolveAutomaticLocationSource(permissions.location, current?.ok ? current.data : undefined);
      }
    }
    prepared._preparedLocation = source === "ip"
      ? await getBrowserIpLocation(labels)
      : await getBrowserGpsLocation(labels);
  }
  if (needsNotification && "Notification" in window) {
    prepared._preparedNotification = await Notification.requestPermission();
  }
  return prepared;
}

async function applyBrowserPermissionState(snapshot: Record<string, unknown>): Promise<Record<string, unknown>> {
  const states = await readBrowserPermissionStates();
  const permissions = snapshot.permissions ?? snapshot;
  const container = Array.isArray(permissions)
    ? { items: permissions as Array<Record<string, unknown>> }
    : permissions as { items?: Array<Record<string, unknown>> };
  if (!Array.isArray(container.items)) return snapshot;
  const items = container.items.map((item) => {
    const id = String(item.id ?? "").toLowerCase();
    const permissionState = id === "location" ? states.location : id === "notifications" ? states.notifications : "unsupported";
    return { ...item, isGranted: permissionState === "granted", permissionState };
  });
  const updatedPermissions = { ...container, items };
  if (Array.isArray(snapshot.permissions)) return { ...snapshot, permissions: items };
  return snapshot.permissions ? { ...snapshot, permissions: updatedPermissions } : { ...snapshot, items };
}

export type BrowserPermissionStates = {
  location: PermissionState | "unsupported";
  notifications: NotificationPermission | "unsupported";
};

export async function readBrowserPermissionStates(): Promise<BrowserPermissionStates> {
  let location: BrowserPermissionStates["location"] = "unsupported";
  try {
    location = navigator.permissions
      ? (await navigator.permissions.query({ name: "geolocation" })).state
      : "unsupported";
  } catch {
    location = "unsupported";
  }
  return {
    location,
    notifications: "Notification" in window ? Notification.permission : "unsupported",
  };
}

export function watchBrowserPermissionChanges(listener: (states: BrowserPermissionStates) => void, emitInitial = true): () => void {
  let disposed = false;
  let last = "";
  let initialized = false;
  let locationStatus: PermissionStatus | undefined;
  const publish = async () => {
    if (disposed) return;
    const states = await readBrowserPermissionStates();
    const serialized = JSON.stringify(states);
    if (serialized !== last) {
      last = serialized;
      const shouldEmit = initialized || emitInitial;
      initialized = true;
      if (shouldEmit) listener(states);
    }
  };
  void navigator.permissions?.query({ name: "geolocation" }).then((status) => {
    if (disposed) return;
    locationStatus = status;
    status.addEventListener("change", publish);
    void publish();
  }).catch(() => publish());
  const onVisible = () => { if (document.visibilityState === "visible") void publish(); };
  window.addEventListener("focus", publish);
  document.addEventListener("visibilitychange", onVisible);
  void publish();
  return () => {
    disposed = true;
    locationStatus?.removeEventListener("change", publish);
    window.removeEventListener("focus", publish);
    document.removeEventListener("visibilitychange", onVisible);
  };
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
  const position: PreparedLocation = prepared ?? await getBrowserGpsLocation(labels);
  const current = await coreCall<LocationSettings>(SETTINGS_SNAPSHOT_METHOD, { section: "locations" });
  if (!current?.ok) {
    return { ok: false, error: current?.error ?? label(labels, "webCoreLocationLoadFailed") };
  }

  const next: LocationSettings = {
    ...current.data,
    useGps: position.source === "gps",
    latitude: position.latitude,
    longitude: position.longitude,
    timeZoneId: position.timeZoneId ?? current.data.timeZoneId,
    locationSource: position.source,
    city: "",
    country: "",
    countryName: "",
  };

  const address = position.address;
  if (position.source === "gps" && !hasConfirmedAddress(address)) {
    // Reverse geocoding is an external best-effort lookup. Never commit fresh
    // coordinates with an empty country: Auto calculation requires the
    // country code, so that partial write would turn a healthy GPS state into
    // an unusable one when a tab resumes.
    if (canReuseConfirmedGpsLocation(current.data)) {
      return {
        ok: true,
        data: {
          ok: true,
          action: "refreshLocation",
          platform: "web",
          changed: false,
          location: current.data,
        } as T,
      };
    }
    return { ok: false, error: label(labels, "locationAddressUnavailable") };
  }
  const finalLocation = {
    ...next,
    city: address?.city ?? "",
    country: address?.countryCode ?? "",
    countryName: address?.country ?? "",
  };
  if (sameConfirmedLocation(current.data, finalLocation)) {
    return {
      ok: true,
      data: {
        ok: true,
        action: "refreshLocation",
        platform: "web",
        changed: false,
        location: current.data,
      } as T,
    };
  }
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
      action: "refreshLocation",
      platform: "web",
      changed: true,
      location: saved.data.calculated ?? saved.data.value ?? finalLocation,
    } as T,
  };
}

function sameConfirmedLocation(current: LocationSettings, next: LocationSettings): boolean {
  return current.locationSource === next.locationSource
    && current.useGps === next.useGps
    && Math.abs(current.latitude - next.latitude) < 0.00001
    && Math.abs(current.longitude - next.longitude) < 0.00001
    && (current.country ?? "") === (next.country ?? "")
    && (current.countryName ?? "") === (next.countryName ?? "")
    && (current.city ?? "") === (next.city ?? "");
}

async function reverseGeocodeBrowserLocation<T>(labels: WebLabels, coreCall: BrowserCoreCall, latitude?: number, longitude?: number): Promise<BridgeResponse<T>> {
  if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) {
    return { ok: false, error: label(labels, "webInvalidCoordinates") };
  }

  const current = await coreCall<LocationSettings>(SETTINGS_SNAPSHOT_METHOD, { section: "locations" });
  if (!current?.ok) {
    return { ok: false, error: current?.error ?? label(labels, "webCoreLocationLoadFailed") };
  }

  const address = await reverseAddressWithRetry(latitude!, longitude!);
  const next = {
    ...current.data,
    useGps: false,
    latitude: latitude!,
    longitude: longitude!,
    locationSource: "manual",
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

async function getBrowserGpsLocation(labels: WebLabels): Promise<PreparedLocation> {
  if (!navigator.geolocation) throw new Error(label(labels, "webGpsUnavailable"));
  const position = await getCurrentBrowserPosition(labels);
  return {
    source: "gps",
    latitude: position.coords.latitude,
    longitude: position.coords.longitude,
    address: await reverseAddressWithRetry(position.coords.latitude, position.coords.longitude),
  };
}

async function reverseAddressWithRetry(latitude: number, longitude: number): Promise<Awaited<ReturnType<typeof reverseAddress>>> {
  for (let attempt = 0; attempt < 2; attempt += 1) {
    const address = await reverseAddress(latitude, longitude);
    if (address?.countryCode || address?.city) return address;
    if (attempt === 0) await new Promise((resolve) => window.setTimeout(resolve, 250));
  }
  return null;
}

async function getBrowserIpLocation(labels: WebLabels): Promise<PreparedLocation> {
  const response = await withTimeout(fetch("https://ipapi.co/json/", {
    headers: { Accept: "application/json" },
    cache: "no-store",
  }), 12000, label(labels, "webGpsTimedOut"), labels);
  if (!response.ok) {
    throw new Error(label(labels, "webGpsUnavailable"));
  }

  const payload = await response.json() as {
    city?: string;
    region?: string;
    country_name?: string;
    country_code?: string;
    latitude?: number;
    longitude?: number;
    timezone?: string;
  };
  if (!hasUsableCoordinates(payload.latitude, payload.longitude)) {
    throw new Error(label(labels, "webInvalidCoordinates"));
  }

  return {
    source: "ip",
    latitude: payload.latitude!,
    longitude: payload.longitude!,
    timeZoneId: typeof payload.timezone === "string" ? payload.timezone : undefined,
    address: {
      city: payload.city ?? payload.region ?? "",
      country: payload.country_name ?? "",
      countryCode: (payload.country_code ?? "").toUpperCase(),
    },
  };
}

function hasUsableCoordinates(latitude?: number, longitude?: number): boolean {
  return Number.isFinite(latitude) && Number.isFinite(longitude) &&
    Math.abs(latitude ?? 0) <= 90 && Math.abs(longitude ?? 0) <= 180 &&
    (Math.abs(latitude ?? 0) > 0.000001 || Math.abs(longitude ?? 0) > 0.000001);
}

function hasConfirmedAddress(address?: { city?: string; country?: string; countryCode?: string } | null): boolean {
  return Boolean(address?.countryCode && (address.city || address.country));
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
  if (automationRuntimeActive()) {
    return { ok: false, error: "File selection is not performed by unattended automation." };
  }
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

async function testBrowserNotification<T>(labels: WebLabels): Promise<BridgeResponse<T>> {
  if (!("Notification" in window) || Notification.permission !== "granted") {
    return { ok: false, error: label(labels, "webNotificationPermissionDenied") };
  }
  const notification = new Notification(label(labels, "testNotification"));
  window.setTimeout(() => notification.close(), 3000);
  return { ok: true, data: { platform: "web", delivered: true } as T };
}

async function launchBrowserExternalIntent<T>(method: string, request: PlatformPayload | undefined, labels: WebLabels): Promise<BridgeResponse<T>> {
  let href: string;
  if (method === "external.openEmail") {
    if (!request?.to) return { ok: false, error: "Email address is required." };
    href = `mailto:${encodeURIComponent(request.to)}`;
  } else if (method === "external.call") {
    if (!request?.number) return { ok: false, error: "Phone number is required." };
    href = `tel:${encodeURIComponent(request.number)}`;
  } else if (method === "external.reportIssue") {
    href = "mailto:rynex@rynex.nl?subject=Pray%20Ad%20Free%20Issue";
  } else if (method === "external.openUrl") {
    let parsed: URL;
    try {
      parsed = new URL(request?.url ?? "");
    } catch {
      return { ok: false, error: "A valid URL is required." };
    }
    if (parsed.protocol !== "https:" && parsed.protocol !== "http:") {
      return { ok: false, error: "Only HTTP(S) URLs can be opened." };
    }
    href = parsed.href;
  } else {
    return { ok: false, error: label(labels, "webNativeActionUnavailable") };
  }

  if (!automationRuntimeActive()) {
    const anchor = document.createElement("a");
    anchor.href = href;
    anchor.target = "_blank";
    anchor.rel = "noopener noreferrer";
    anchor.hidden = true;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
  }
  return { ok: true, data: { platform: "web", launched: true } as T };
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
