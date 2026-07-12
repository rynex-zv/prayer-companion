import type { BridgeResponse } from "./mauiWebberClient";
import { tryHandleWebPlatformCall } from "./webPlatformAdapter";
import { getLastWasmCoreLoadError, tryCallWasmCore } from "./wasmCoreClient";
import { coreContract } from "../generated/core-contract";

const DATABASE = "prayer-companion";
const STORE = "repositories";
const STATE_KEY = "core-state";
const SCHEMA_VERSION = 2;
let ready: Promise<void> | undefined;
let mutationQueue = Promise.resolve();
const kinds = new Map<string, string>(coreContract.rpcContracts.map((item) => [item.name, item.kind]));

export async function callBrowserBackend<T>(method: string, payload?: unknown): Promise<BridgeResponse<T> | undefined> {
  await (ready ??= hydrate());
  const operation = async () => {
    const platform = await tryHandleWebPlatformCall<T>(method, payload);
    if (platform) return platform;
    const response = await tryCallWasmCore<T>(method, payload);
    if (response?.ok && isMutation(method)) await persist();
    return response;
  };
  if (!isMutation(method)) return operation();
  const result = mutationQueue.then(operation, operation);
  mutationQueue = result.then(() => undefined, () => undefined);
  return result;
}

export { getLastWasmCoreLoadError };

async function hydrate(): Promise<void> {
  let state = await readRecord();
  if (!state) {
    state = localStorage.getItem("pray.web.core.state") ?? undefined;
    if (state) {
      await writeRecord(state);
      localStorage.removeItem("pray.web.core.state");
    }
  }
  if (state) await tryCallWasmCore("app.importState", { state });
  const legacyAppState = localStorage.getItem("prayer-companion:app-state:v1");
  if (legacyAppState) {
    try {
      const legacy = JSON.parse(legacyAppState) as { language?: string; themeMode?: string; accentColor?: string; textSize?: number; onboardingCompleted?: boolean };
      if (legacy.language) await tryCallWasmCore("app.setLanguage", { language: legacy.language });
      if (legacy.themeMode) await tryCallWasmCore("app.setTheme", { theme: legacy.themeMode });
      if (legacy.accentColor !== undefined) await tryCallWasmCore("settings.setField", { section: "theme", field: "accentColor", value: legacy.accentColor });
      if (legacy.textSize !== undefined) await tryCallWasmCore("settings.setField", { section: "theme", field: "textSize", value: legacy.textSize });
      if (legacy.onboardingCompleted) await tryCallWasmCore("onboarding.complete", {});
      await persist();
    } finally {
      localStorage.removeItem("prayer-companion:app-state:v1");
    }
  }
}

async function persist(): Promise<void> {
  const exported = await tryCallWasmCore<string>("app.exportState", {});
  if (exported?.ok && typeof exported.data === "string") await writeRecord(exported.data);
}

function isMutation(method: string): boolean {
  const kind = kinds.get(method);
  return kind === "command" || kind === "compatibilityAdapter";
}

function openDatabase(): Promise<IDBDatabase> {
  return new Promise<IDBDatabase>((resolve, reject) => {
    const request = indexedDB.open(DATABASE, 1);
    request.onupgradeneeded = () => {
      if (!request.result.objectStoreNames.contains(STORE)) request.result.createObjectStore(STORE);
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

async function readRecord(): Promise<string | undefined> {
  const db = await openDatabase();
  return new Promise<string | undefined>((resolve, reject) => {
    const request = db.transaction(STORE, "readonly").objectStore(STORE).get(STATE_KEY);
    request.onsuccess = () => {
      const value = request.result as string | { schemaVersion?: number; data?: string } | undefined;
      if (typeof value === "string") resolve(value);
      else resolve(value?.schemaVersion === SCHEMA_VERSION ? value.data : undefined);
    };
    request.onerror = () => reject(request.error);
  }).finally(() => db.close());
}

async function writeRecord(value: string): Promise<void> {
  const db = await openDatabase();
  return new Promise<void>((resolve, reject) => {
    const transaction = db.transaction(STORE, "readwrite");
    transaction.objectStore(STORE).put({ schemaVersion: SCHEMA_VERSION, data: value }, STATE_KEY);
    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error);
    transaction.onabort = () => reject(transaction.error);
  }).finally(() => db.close());
}
