import type { WebUpdateApi, WebUpdateSnapshot, WebVersionMetadata } from "./native/webUpdateTypes";

const isRemoteWeb =
  (window.location.protocol === "http:" || window.location.protocol === "https:") &&
  window.location.hostname !== "app.prayadfree.local";

const emptySnapshot: WebUpdateSnapshot = {
  status: "idle",
  currentVersion: "",
  latestVersion: "",
  availableVersion: "",
  required: false,
  error: "",
};

let metadata: WebVersionMetadata | undefined;
let snapshot: WebUpdateSnapshot = emptySnapshot;
let registrationPromise: Promise<ServiceWorkerRegistration | undefined> | undefined;
const listeners = new Set<(value: WebUpdateSnapshot) => void>();

async function loadApplication(): Promise<void> {
  if (isRemoteWeb) {
    await initializeRemoteWebUpdate();
  }
  await import("./main");
}

async function initializeRemoteWebUpdate(): Promise<void> {
  installUpdateApi();
  setSnapshot({ status: "checking", error: "" });
  window.__prayBoot?.update("loading", undefined, "version.web.json");

  try {
    metadata = await fetchVersionMetadata();
    setSnapshot({
      status: "checking",
      latestVersion: metadata.version,
      availableVersion: "",
      required: false,
      error: "",
    });
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    console.warn(`[pray.update] version_check_failed error=${message}`);
    setSnapshot({ status: "error", error: message });
    window.__prayBoot?.update(navigator.onLine ? "slow" : "offline", undefined, `version.web.json · ${message}`);
    return;
  }

  if (!("serviceWorker" in navigator)) {
    setSnapshot({
      status: "unsupported",
      currentVersion: metadata.version,
      latestVersion: metadata.version,
      availableVersion: "",
    });
    return;
  }

  registrationPromise = stageServiceWorkerUpdate(metadata).catch((error) => {
    const message = error instanceof Error ? error.message : String(error);
    console.warn(`[pray.update] service_worker_update_failed version=${metadata?.version ?? "unknown"} error=${message}`);
    setSnapshot({ status: "error", error: message });
    return undefined;
  });
}

function installUpdateApi(): void {
  const api: WebUpdateApi = {
    getSnapshot: () => snapshot,
    subscribe: (listener) => {
      listeners.add(listener);
      listener(snapshot);
      return () => listeners.delete(listener);
    },
    apply: applyStagedUpdate,
    checkNow: async () => {
      metadata = await fetchVersionMetadata();
      registrationPromise = stageServiceWorkerUpdate(metadata);
      await registrationPromise;
    },
  };
  window.__prayWebUpdate = api;
}

function setSnapshot(next: Partial<WebUpdateSnapshot>): void {
  snapshot = { ...snapshot, ...next };
  for (const listener of listeners) {
    listener(snapshot);
  }
}

async function fetchVersionMetadata(): Promise<WebVersionMetadata> {
  const response = await fetch(`/version.web.json?check=${Date.now()}`, {
    cache: "no-store",
    headers: { "Cache-Control": "no-cache" },
  });
  if (response.ok && !response.headers.get("content-type")?.toLowerCase().includes("html")) {
    const value = await response.json() as Partial<WebVersionMetadata>;
    const version = typeof value.version === "string" ? value.version.trim() : "";
    if (version) {
      return {
        version,
        build: typeof value.build === "number" ? value.build : undefined,
        legacyVersion: typeof value.legacyVersion === "string" ? value.legacyVersion : undefined,
        cacheEpoch: typeof value.cacheEpoch === "number" ? value.cacheEpoch : undefined,
        minimumSupportedVersion: typeof value.minimumSupportedVersion === "string" ? value.minimumSupportedVersion : version,
        serviceWorker: typeof value.serviceWorker === "string" ? value.serviceWorker : `/pray-sw.js?v=${encodeURIComponent(version)}`,
        manifest: typeof value.manifest === "string" ? value.manifest : undefined,
        generatedAt: typeof value.generatedAt === "string" ? value.generatedAt : undefined,
      };
    }
  }

  const legacyResponse = await fetch(`/version.web.info?check=${Date.now()}`, {
    cache: "no-store",
    headers: { "Cache-Control": "no-cache" },
  });
  if (!legacyResponse.ok || legacyResponse.headers.get("content-type")?.toLowerCase().includes("html")) {
    throw new Error(`HTTP ${legacyResponse.status}`);
  }
  const legacyVersion = (await legacyResponse.text()).trim();
  if (!/^\d+$/.test(legacyVersion)) throw new Error("Invalid web version");
  return {
    version: legacyVersion,
    build: Number(legacyVersion),
    legacyVersion,
    minimumSupportedVersion: legacyVersion,
    serviceWorker: `/pray-sw.js?v=${encodeURIComponent(legacyVersion)}`,
  };
}

async function stageServiceWorkerUpdate(nextMetadata: WebVersionMetadata): Promise<ServiceWorkerRegistration | undefined> {
  const existingRegistration = await navigator.serviceWorker.getRegistration("/");
  const activeVersion = readWorkerVersion(existingRegistration?.active ?? navigator.serviceWorker.controller);
  const currentVersion = activeVersion || nextMetadata.version;
  const updateRequired = isVersionBefore(currentVersion, nextMetadata.minimumSupportedVersion || nextMetadata.version);
  setSnapshot({
    currentVersion,
    latestVersion: nextMetadata.version,
    required: updateRequired,
    availableVersion: "",
    status: activeVersion && activeVersion !== nextMetadata.version ? "downloading" : "current",
    error: "",
  });

  const registration = await navigator.serviceWorker.register(resolveWorkerUrl(nextMetadata), {
    scope: "/",
    updateViaCache: "none",
  });

  if (!registration.active && !navigator.serviceWorker.controller) {
    void activateInitialWorker(registration, nextMetadata.version);
    return registration;
  }

  if ((readWorkerVersion(registration.active) || activeVersion) === nextMetadata.version) {
    setSnapshot({
      status: "current",
      currentVersion: nextMetadata.version,
      latestVersion: nextMetadata.version,
      availableVersion: "",
      required: false,
      error: "",
    });
    void registration.update().catch(() => undefined);
    return registration;
  }

  setSnapshot({
    status: "downloading",
    currentVersion,
    latestVersion: nextMetadata.version,
    availableVersion: "",
    required: updateRequired,
    error: "",
  });
  await registration.update();
  const staged = await waitForWaitingWorkerVersion(registration, nextMetadata.version, 180_000);
  if (!staged) {
    throw new Error(`Timed out staging web version ${nextMetadata.version}`);
  }
  setSnapshot({
    status: "ready",
    currentVersion,
    latestVersion: nextMetadata.version,
    availableVersion: nextMetadata.version,
    required: updateRequired,
    error: "",
  });
  return registration;
}

async function activateInitialWorker(registration: ServiceWorkerRegistration, version: string): Promise<void> {
  const staged = await waitForWaitingWorkerVersion(registration, version, 180_000);
  if (!staged) return;
  staged.postMessage({ type: "SKIP_WAITING" });
  await waitForControllerVersion(version, 30_000);
  setSnapshot({
    status: "current",
    currentVersion: version,
    latestVersion: version,
    availableVersion: "",
    required: false,
    error: "",
  });
}

async function applyStagedUpdate(): Promise<void> {
  const targetVersion = snapshot.availableVersion || metadata?.version || "";
  if (!targetVersion) return;
  setSnapshot({ status: "applying", error: "" });
  let registration = await registrationPromise;
  registration ??= await navigator.serviceWorker.getRegistration("/");
  if (!registration) {
    throw new Error("No service worker registration is available.");
  }

  const waiting = await findOrStageWaitingWorker(registration, targetVersion);
  if (!waiting) {
    throw new Error(`Web version ${targetVersion} is not fully cached yet.`);
  }

  sessionStorage.setItem("pray.web.commitVersion", targetVersion);
  sessionStorage.setItem("pray.web.resumeUrl", location.href);
  waiting.postMessage({ type: "SKIP_WAITING" });
  await waitForControllerVersion(targetVersion, 30_000);
  const next = new URL(sessionStorage.getItem("pray.web.resumeUrl") || location.href);
  next.searchParams.set("pray-version", targetVersion);
  location.replace(next.href);
}

async function findOrStageWaitingWorker(registration: ServiceWorkerRegistration, version: string): Promise<ServiceWorker | undefined> {
  if (matchesWorkerVersion(registration.waiting, version)) return registration.waiting ?? undefined;
  if (metadata) {
    registration = await navigator.serviceWorker.register(resolveWorkerUrl(metadata), {
      scope: "/",
      updateViaCache: "none",
    });
    await registration.update();
  }
  return await waitForWaitingWorkerVersion(registration, version, 180_000);
}

function resolveWorkerUrl(value: WebVersionMetadata): string {
  const workerUrl = value.serviceWorker || `/pray-sw.js?v=${encodeURIComponent(value.version)}`;
  return new URL(workerUrl, location.origin).toString();
}

function readWorkerVersion(worker: ServiceWorker | null | undefined): string {
  if (!worker) return "";
  try {
    return new URL(worker.scriptURL).searchParams.get("v") ?? "";
  } catch {
    return "";
  }
}

function matchesWorkerVersion(worker: ServiceWorker | null | undefined, version: string): worker is ServiceWorker {
  return readWorkerVersion(worker) === version;
}

function waitForWaitingWorkerVersion(
  registration: ServiceWorkerRegistration,
  version: string,
  timeoutMs: number,
): Promise<ServiceWorker | undefined> {
  const existing = [registration.waiting, registration.installing].find((worker) => matchesWorkerVersion(worker, version));
  if (existing?.state === "installed") return Promise.resolve(existing);
  return new Promise((resolve) => {
    const timeout = window.setTimeout(() => resolve(undefined), timeoutMs);
    const finish = (worker: ServiceWorker | null | undefined) => {
      if (!matchesWorkerVersion(worker, version) || worker.state !== "installed") return;
      cleanup();
      resolve(worker);
    };
    const onUpdateFound = () => {
      const worker = registration.installing;
      if (!worker) return;
      worker.addEventListener("statechange", () => finish(worker));
      finish(worker);
    };
    const cleanup = () => {
      window.clearTimeout(timeout);
      registration.removeEventListener("updatefound", onUpdateFound);
    };
    registration.addEventListener("updatefound", onUpdateFound);
    const candidate = registration.installing ?? registration.waiting;
    candidate?.addEventListener("statechange", () => finish(candidate));
    finish(candidate);
  });
}

function waitForControllerVersion(version: string, timeoutMs: number): Promise<boolean> {
  if (readWorkerVersion(navigator.serviceWorker.controller) === version) return Promise.resolve(true);
  return new Promise((resolve) => {
    const timeout = window.setTimeout(() => finish(false), timeoutMs);
    const finish = (value: boolean) => {
      window.clearTimeout(timeout);
      navigator.serviceWorker.removeEventListener("controllerchange", onControllerChange);
      resolve(value);
    };
    const onControllerChange = () => {
      if (readWorkerVersion(navigator.serviceWorker.controller) === version) finish(true);
    };
    navigator.serviceWorker.addEventListener("controllerchange", onControllerChange);
  });
}

function isVersionBefore(current: string, minimum: string): boolean {
  const a = parseVersionParts(current);
  const b = parseVersionParts(minimum);
  const length = Math.max(a.length, b.length);
  for (let index = 0; index < length; index += 1) {
    const left = a[index] ?? 0;
    const right = b[index] ?? 0;
    if (left < right) return true;
    if (left > right) return false;
  }
  return false;
}

function parseVersionParts(value: string): number[] {
  const parts = value.match(/\d+/g)?.map((part) => Number(part)).filter(Number.isFinite) ?? [];
  return parts.length ? parts : [0];
}

void loadApplication().catch((error) => {
  const message = error instanceof Error ? error.message : String(error);
  console.error("Application loader failed", error);
  window.__prayBoot?.update(navigator.onLine ? "failed" : "offline", undefined, message);
});
