import { tryHandleWebPlatformCall } from "./webPlatformAdapter";
import { getLastWasmCoreLoadError, tryCallWasmCore } from "./wasmCoreClient";
import { coreContract } from "../generated/core-contract";

export type BridgeResponse<T = unknown> =
  | { ok: true; data: T }
  | { ok: false; error: string; errorInfo?: TransportError };
type BridgeFailure = Extract<BridgeResponse<never>, { ok: false }>;

export type TransportErrorCode = "timeout" | "unavailable" | "transport" | "contract" | "cancelled";
export type TransportError = {
  code: TransportErrorCode;
  message: string;
  retryable: boolean;
  backend: BackendKind | "unselected";
};
export type BackendKind = "maui" | "browser";

type SelectedBackend =
  | { kind: "maui"; bridge: NonNullable<Window["mauiWebber"]> }
  | { kind: "browser" };

export async function mauiCall<T = unknown>(
  method: string,
  payload?: unknown,
  options?: { requestId?: string; commandId?: string },
): Promise<BridgeResponse<T>> {
  const callId = nextCallId();
  const requestId = options?.requestId ?? createId();
  const kind = classify(method);
  const commandId = options?.commandId ?? (kind === "command" || kind === "compatibilityAdapter" ? createId() : undefined);
  const correlatedPayload = addCorrelation(payload, requestId, commandId);
  const started = now();
  const timeoutMs = bridgeTimeoutFor(method);
  observeSequence(method, kind, requestId);
  logBridge("start", { callId, requestId, commandId, method, kind, payload, timeoutMs });

  try {
    const backend = await selectBackend();
    if (backend.kind === "maui") {
      try {
        const res = await withBridgeTimeout(backend.bridge.call(method, correlatedPayload), timeoutMs, method, "maui");
        logBridge("success", { callId, requestId, commandId, method, source: "maui", elapsedMs: elapsed(started), responseBytes: byteSize(res) });
        if (res && typeof res === "object" && "ok" in res) {
          return res as BridgeResponse<T>;
        }
        return { ok: true, data: res as T };
      } catch (error) {
        const failure = transportFailure(error, "maui");
        logBridge("failure", {
          callId,
          requestId,
          commandId,
          method,
          source: "maui",
          elapsedMs: elapsed(started),
          errorCode: failure.errorInfo?.code,
        });
        return failure;
      }
    }

    // Browser platform operations and WASM belong to one selected browser backend.
    // Never enter this path after a native failure: that could execute a command twice.
    const webPlatform = await withBridgeTimeout(tryHandleWebPlatformCall<T>(method, correlatedPayload), timeoutMs, method, "web-platform");
    if (webPlatform) {
      logBridge(webPlatform.ok ? "success" : "failure", { callId, method, source: "web-platform", elapsedMs: elapsed(started), error: webPlatform.ok ? undefined : webPlatform.error });
      return webPlatform;
    }

    const wasm = await withBridgeTimeout(tryCallWasmCore<T>(method, correlatedPayload), timeoutMs, method, "wasm");
    if (wasm) {
      logBridge(wasm.ok ? "success" : "failure", { callId, method, source: "wasm", elapsedMs: elapsed(started), error: wasm.ok ? undefined : wasm.error });
      return wasm;
    }

    const detail = getLastWasmCoreLoadError();
    logBridge("failure", { callId, method, source: "none", elapsedMs: elapsed(started), error: detail ?? "Web Core failed to load." });
    return failure("unavailable", detail ? `Web Core failed to load: ${detail}` : "Web Core failed to load.", "browser", true);
  } catch (e) {
    const result = transportFailure(e, selectedBackend?.kind ?? "unselected");
    logBridge("error", { callId, requestId, commandId, method, elapsedMs: elapsed(started), errorCode: result.errorInfo?.code });
    return result;
  }
}

export function isBridgeReady(): boolean {
  return typeof window !== "undefined" && !!window.mauiWebber && hasNativeTransport();
}

export function mauiTrace(name: string, detail: Record<string, unknown> = {}): void {
  if (!isBridgeReady()) {
    return;
  }

  void window.mauiWebber?.call("mauiWebber.trace", {
    name,
    at: typeof performance !== "undefined" ? performance.now() : undefined,
    ...detail,
  }).catch(() => undefined);
}

/** @deprecated Backend selection is asynchronous. Use getSelectedBackendKind for diagnostics. */
export const BRIDGE_MODE: "maui" | "wasm" = typeof window !== "undefined" && window.mauiWebber ? "maui" : "wasm";

let selectedBackend: SelectedBackend | undefined;
let backendSelection: Promise<SelectedBackend> | undefined;

export async function getSelectedBackendKind(): Promise<BackendKind> {
  return (await selectBackend()).kind;
}

async function selectBackend(): Promise<SelectedBackend> {
  if (selectedBackend) return selectedBackend;
  backendSelection ??= (async () => {
    const bridge = await withBridgeTimeout(resolveBridge(), 2500, "backend.select", "resolveBridge").catch(() => undefined);
    selectedBackend = bridge && hasNativeTransport() ? { kind: "maui", bridge } : { kind: "browser" };
    logBridge("backend-selected", { backend: selectedBackend.kind });
    return selectedBackend;
  })();
  return backendSelection;
}

async function resolveBridge(): Promise<Window["mauiWebber"] | undefined> {
  if (typeof window === "undefined") {
    return undefined;
  }

  if (window.mauiWebber) {
    return window.mauiWebber;
  }

  if (!shouldWaitForBridge()) {
    return undefined;
  }

  await new Promise<void>((resolve) => {
    const timeout = window.setTimeout(done, 1500);

    function done() {
      window.clearTimeout(timeout);
      window.removeEventListener("mauiwebber:ready", done);
      resolve();
    }

    window.addEventListener("mauiwebber:ready", done, { once: true });
  });

  return window.mauiWebber;
}

function shouldWaitForBridge(): boolean {
  if (typeof window === "undefined") {
    return false;
  }

  return import.meta.env.MODE === "phone" || window.location.protocol === "file:";
}

function hasNativeTransport(): boolean {
  if (typeof window === "undefined") {
    return false;
  }

  const nativeWindow = window as Window & {
    chrome?: { webview?: { postMessage?: unknown } };
    mauiWebberNative?: { postMessage?: unknown };
  };

  return Boolean(
    nativeWindow.mauiWebberNative?.postMessage ||
    nativeWindow.chrome?.webview?.postMessage ||
    window.location.protocol === "file:" ||
    import.meta.env.MODE === "phone",
  );
}

function bridgeTimeoutFor(method: string): number {
  if (method === "mauiWebber.pullRemote") {
    return 50000;
  }

  if (method === "mauiWebber.clearSiteData") {
    return 30000;
  }

  if (method === "mauiWebber.trace") {
    return 5000;
  }

  if (method === "settings.invoke") {
    return 20000;
  }

  return 15000;
}

function withBridgeTimeout<T>(promise: Promise<T>, timeoutMs: number, method: string, source: string): Promise<T> {
  if (typeof window === "undefined") {
    return promise;
  }

  return new Promise((resolve, reject) => {
    const timeout = window.setTimeout(() => {
      reject(new Error(`${method} timed out while waiting for ${source}.`));
    }, timeoutMs);

    promise.then(
      (value) => {
        window.clearTimeout(timeout);
        resolve(value);
      },
      (error) => {
        window.clearTimeout(timeout);
        reject(error);
      },
    );
  });
}

let bridgeCallCounter = 0;

function nextCallId(): number {
  bridgeCallCounter += 1;
  return bridgeCallCounter;
}

function now(): number {
  return typeof performance !== "undefined" ? performance.now() : Date.now();
}

function elapsed(started: number): number {
  return Math.round(now() - started);
}

function logBridge(event: string, detail: Record<string, unknown>): void {
  const payload = { event, ...redact(detail) };
  console.info(`[pray.bridge] ${JSON.stringify(payload)}`);
  if (event !== "start") {
    mauiTrace(`bridge.${event}`, payload);
  }
}

function redact(detail: Record<string, unknown>): Record<string, unknown> {
  const safe = { ...detail };
  delete safe.payload;
  delete safe.data;
  return safe;
}

function transportFailure(error: unknown, backend: BackendKind | "unselected"): BridgeFailure {
  const message = error instanceof Error ? error.message : String(error);
  const code: TransportErrorCode = /timed out/i.test(message) ? "timeout" : "transport";
  return failure(code, message, backend, code === "timeout");
}

function failure(code: TransportErrorCode, message: string, backend: BackendKind | "unselected", retryable: boolean): BridgeFailure {
  return { ok: false, error: message, errorInfo: { code, message, retryable, backend } };
}

type OperationKind = "command" | "query" | "platformOperation" | "compatibilityAdapter" | "obsolete";
const rpcKinds = new Map<string, string>(coreContract.rpcContracts.map((item) => [item.name, item.kind]));
const inFlightQueries = new Map<string, { count: number; requestId: string }>();
const lastCommandByDomain = new Map<string, { method: string; at: number; requestId: string }>();

function classify(method: string): OperationKind {
  return (rpcKinds.get(method) as OperationKind | undefined) ?? "compatibilityAdapter";
}

function observeSequence(method: string, kind: OperationKind, requestId: string): void {
  const domain = method.split(".", 1)[0];
  if (kind === "command" || kind === "compatibilityAdapter") {
    lastCommandByDomain.set(domain, { method, at: now(), requestId });
    return;
  }
  if (kind !== "query") return;
  const active = inFlightQueries.get(method);
  if (active) logBridge("duplicate-query", { method, requestId, originalRequestId: active.requestId, duplicateCount: active.count + 1 });
  inFlightQueries.set(method, { count: (active?.count ?? 0) + 1, requestId: active?.requestId ?? requestId });
  const command = lastCommandByDomain.get(domain);
  if (command && now() - command.at < 2000) logBridge("command-then-refresh", { method, requestId, commandMethod: command.method, commandRequestId: command.requestId });
  if (typeof window === "undefined") return;
  window.setTimeout(() => {
    const current = inFlightQueries.get(method);
    if (!current) return;
    if (current.count <= 1) inFlightQueries.delete(method);
    else inFlightQueries.set(method, { ...current, count: current.count - 1 });
  }, bridgeTimeoutFor(method));
}

function addCorrelation(payload: unknown, requestId: string, commandId?: string): unknown {
  const body = payload && typeof payload === "object" && !Array.isArray(payload) ? payload as Record<string, unknown> : {};
  return { ...body, _rpc: { contractVersion: coreContract.contractVersion, requestId, commandId } };
}

function createId(): string {
  return globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function byteSize(value: unknown): number {
  try { return new TextEncoder().encode(JSON.stringify(value)).length; } catch { return 0; }
}
