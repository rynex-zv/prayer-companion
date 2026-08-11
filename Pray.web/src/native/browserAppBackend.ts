import type { BridgeResponse } from "./mauiWebberClient";
import { prepareWebPlatformPayload, tryHandleWebPlatformCall, type BrowserCoreCall } from "./webPlatformAdapter";
import { executeWasmCore, getLastWasmCoreLoadError, preloadWasmCore } from "./wasmCoreClient";
import { coreContract } from "../generated/core-contract";
import { automationEnabled } from "../automation/config";

const DATABASE = import.meta.env.VITE_PRAY_AUTOMATION === "true" && automationEnabled()
  ? "prayer-companion-automation"
  : "prayer-companion";
const STORE = "repositories";
const STATE_KEY = "core-state";
const SCHEMA_VERSION = 4;
let ready: Promise<void> | undefined;
let operationQueue = Promise.resolve();
let volatileState: string | undefined;
let repositoryNeedsUpgrade = false;
let preloadedBootstrap: BridgeResponse<unknown> | undefined;
const kinds = new Map<string, string>(coreContract.rpcContracts.map((item) => [item.name, item.kind]));

export async function callBrowserBackend<T>(method: string, payload?: unknown): Promise<BridgeResponse<T> | undefined> {
  const started = performance.now();
  await (ready ??= migrateLegacyState());
  if (method === "app.bootstrap" && preloadedBootstrap) {
    const response = preloadedBootstrap as BridgeResponse<T>;
    preloadedBootstrap = undefined;
    console.info(`[pray.rpc] ${JSON.stringify({ method, backend: "browser", totalMs: Math.round((performance.now() - started) * 10) / 10, preloaded: true })}`);
    return response;
  }
  // Permission dialogs, GPS acquisition, and reverse-geocoding must not hold the
  // repository lock. Only the deterministic state transition is serialized.
  const preparationState = volatileState ?? await readRecord();
  const readOnlyCoreCall: BrowserCoreCall = (innerMethod, innerPayload) =>
    executeWasmCore(preparationState, innerMethod, withPlatformContext(innerPayload));
  const preparedPayload = await prepareWebPlatformPayload(method, payload, readOnlyCoreCall);
  const operation = operationQueue.then(() => executeOperation<T>(method, preparedPayload), () => executeOperation<T>(method, preparedPayload));
  operationQueue = operation.then(() => undefined, () => undefined);
  const response = await operation;
  const totalMs = performance.now() - started;
  console.info(`[pray.rpc] ${JSON.stringify({ method, backend: "browser", totalMs: Math.round(totalMs * 10) / 10 })}`);
  if (totalMs > 300 && !isInteractiveOperation(method)) console.error(`[pray.rpc] budget_exceeded method=${method} totalMs=${totalMs.toFixed(1)} budgetMs=300`);
  return response;
}

export async function preloadBrowserBackend(): Promise<void> {
  await Promise.all([ready ??= migrateLegacyState(), preloadWasmCore()]);
  preloadedBootstrap ??= await executeOperation("app.bootstrap", {});
}

export { getLastWasmCoreLoadError };

async function executeOperation<T>(method: string, payload?: unknown): Promise<BridgeResponse<T> | undefined> {
  let state = volatileState ?? await readRecord();
  const events: unknown[] = [];
  let stateChanged = false;

  const coreCall: BrowserCoreCall = async <TResult>(innerMethod: string, innerPayload?: unknown) => {
    const response = await executeWasmCore<TResult>(state, innerMethod, withPlatformContext(innerPayload));
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

  if (response.ok && state && (stateChanged || isMutation(method) || repositoryNeedsUpgrade)) {
    await writeRecord(state);
    repositoryNeedsUpgrade = false;
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
      if (legacy.accentColor !== undefined) await apply("settings.update", { section: "theme", field: "accentColor", value: legacy.accentColor });
      if (legacy.textSize !== undefined) await apply("settings.update", { section: "theme", field: "textSize", value: legacy.textSize });
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

function isInteractiveOperation(method: string): boolean {
  return /^(permissions\.|location\.|adhan\.sound\.|external\.|mauiWebber\.(pullRemote|clearSiteData))/.test(method);
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

class RepositorySchemaError extends Error {}

async function readRecord(): Promise<string | undefined> {
  try {
    const db = await openDatabase();
    return await new Promise<string | undefined>((resolve, reject) => {
      const request = db.transaction(STORE, "readonly").objectStore(STORE).get(STATE_KEY);
      request.onsuccess = () => {
        const value = request.result as string | { schemaVersion?: number; data?: string } | undefined;
        if (typeof value === "string") {
          repositoryNeedsUpgrade = true;
          volatileState = value;
          resolve(value);
        } else if (typeof value?.data === "string" && (value.schemaVersion ?? 1) <= SCHEMA_VERSION) {
          repositoryNeedsUpgrade ||= value.schemaVersion !== SCHEMA_VERSION;
          volatileState = value.data;
          resolve(value.data);
        } else if ((value?.schemaVersion ?? 0) > SCHEMA_VERSION) {
          reject(new RepositorySchemaError(`Browser repository schema ${value?.schemaVersion} is newer than supported schema ${SCHEMA_VERSION}.`));
        } else {
          resolve(undefined);
        }
      };
      request.onerror = () => reject(request.error);
    }).finally(() => db.close());
  } catch (error) {
    throw new Error("Browser repository could not be read; refusing volatile-storage fallback.", { cause: error });
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
    throw new Error("Browser repository could not persist; refusing to report a successful mutation.", { cause: error });
  }
}

function withPlatformContext(payload: unknown): Record<string, unknown> {
  const body = payload && typeof payload === "object" && !Array.isArray(payload)
    ? payload as Record<string, unknown>
    : {};
  const timeZoneId = Intl.DateTimeFormat().resolvedOptions().timeZone;
  if (!timeZoneId) throw new Error("Browser time zone is unavailable; prayer calculation cannot continue safely.");
  return { ...body, _platform: { timeZoneId } };
}
