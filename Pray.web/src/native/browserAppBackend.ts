import type { BridgeResponse } from "./mauiWebberClient";
import { tryHandleWebPlatformCall, type BrowserCoreCall } from "./webPlatformAdapter";
import { executeWasmCore, getLastWasmCoreLoadError } from "./wasmCoreClient";
import { coreContract } from "../generated/core-contract";

const DATABASE = "prayer-companion";
const STORE = "repositories";
const STATE_KEY = "core-state";
const SCHEMA_VERSION = 3;
let ready: Promise<void> | undefined;
let operationQueue = Promise.resolve();
let volatileState: string | undefined;
const kinds = new Map<string, string>(coreContract.rpcContracts.map((item) => [item.name, item.kind]));

export async function callBrowserBackend<T>(method: string, payload?: unknown): Promise<BridgeResponse<T> | undefined> {
  await (ready ??= migrateLegacyState());
  const operation = operationQueue.then(() => executeOperation<T>(method, payload), () => executeOperation<T>(method, payload));
  operationQueue = operation.then(() => undefined, () => undefined);
  return operation;
}

export { getLastWasmCoreLoadError };

async function executeOperation<T>(method: string, payload?: unknown): Promise<BridgeResponse<T> | undefined> {
  let state = await readRecord();
  const events: unknown[] = [];
  let stateChanged = false;

  const coreCall: BrowserCoreCall = async <TResult>(innerMethod: string, innerPayload?: unknown) => {
    const response = await executeWasmCore<TResult>(state, innerMethod, innerPayload);
    if (response?.ok && response.state) {
      stateChanged ||= response.state !== state;
      state = response.state;
      if (response.events) events.push(...response.events);
    }
    return response;
  };

  const platform = await tryHandleWebPlatformCall<T>(method, payload, coreCall);
  const response = platform ?? await coreCall<T>(method, payload);
  if (!response) return undefined;

  if (response.ok && state && (stateChanged || isMutation(method))) {
    await writeRecord(state);
    console.info(`[pray.storage] committed method=${method} bytes=${state.length}`);
  }
  return response.ok && events.length > 0 ? { ...response, events } : response;
}

async function migrateLegacyState(): Promise<void> {
  const existing = await readRecord();
  console.info(`[pray.storage] hydrate source=${existing ? "indexeddb" : "empty"}`);
  if (existing) {
    retireLegacyKeys();
    return;
  }

  let state = localStorage.getItem("pray.web.core.state") ?? undefined;
  const legacyAppState = localStorage.getItem("prayer-companion:app-state:v1");
  try {
    if (legacyAppState) {
      const legacy = JSON.parse(legacyAppState) as {
        language?: string;
        themeMode?: string;
        accentColor?: string;
        textSize?: number;
        onboardingCompleted?: boolean;
      };
      const apply = async (method: string, payload: unknown) => {
        const response = await executeWasmCore(state, method, payload);
        if (response?.ok && response.state) state = response.state;
      };
      if (legacy.language) await apply("app.setLanguage", { language: legacy.language });
      if (legacy.themeMode) await apply("app.setTheme", { theme: legacy.themeMode });
      if (legacy.accentColor !== undefined) await apply("settings.setField", { section: "theme", field: "accentColor", value: legacy.accentColor });
      if (legacy.textSize !== undefined) await apply("settings.setField", { section: "theme", field: "textSize", value: legacy.textSize });
      if (legacy.onboardingCompleted) await apply("onboarding.complete", {});
    } else if (state) {
      const normalized = await executeWasmCore(state, "app.getShellSnapshot", {});
      if (normalized?.ok && normalized.state) state = normalized.state;
    }
    if (state) await writeRecord(state);
  } finally {
    retireLegacyKeys();
  }
}

function retireLegacyKeys(): void {
  localStorage.removeItem("pray.web.core.state");
  localStorage.removeItem("prayer-companion:app-state:v1");
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
  try {
    const db = await openDatabase();
    return await new Promise<string | undefined>((resolve, reject) => {
      const request = db.transaction(STORE, "readonly").objectStore(STORE).get(STATE_KEY);
      request.onsuccess = () => {
        const value = request.result as string | { schemaVersion?: number; data?: string } | undefined;
        if (typeof value === "string") resolve(value);
        else if (typeof value?.data === "string" && (value.schemaVersion ?? 1) <= SCHEMA_VERSION) resolve(value.data);
        else resolve(undefined);
      };
      request.onerror = () => reject(request.error);
    }).finally(() => db.close());
  } catch (error) {
    console.warn("Browser repository is using volatile storage because IndexedDB is unavailable.", error);
    return volatileState;
  }
}

async function writeRecord(value: string): Promise<void> {
  volatileState = value;
  try {
    const db = await openDatabase();
    await new Promise<void>((resolve, reject) => {
      const transaction = db.transaction(STORE, "readwrite");
      transaction.objectStore(STORE).put({ schemaVersion: SCHEMA_VERSION, data: value }, STATE_KEY);
      transaction.oncomplete = () => resolve();
      transaction.onerror = () => reject(transaction.error);
      transaction.onabort = () => reject(transaction.error);
    }).finally(() => db.close());
  } catch (error) {
    console.warn("Browser repository could not persist to IndexedDB; state is volatile.", error);
  }
}
