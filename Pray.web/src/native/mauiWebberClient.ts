import { callBrowserBackend, getLastWasmCoreLoadError } from "./browserAppBackend";
import { coreContract } from "../generated/core-contract";

export type BridgeResponse<T = unknown> =
  | { ok: true; data: T; events?: unknown[] }
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
  options?: { requestId?: string; commandId?: string; domain?: string; expectedRevision?: number },
): Promise<BridgeResponse<T>> {
  const callId = nextCallId();
  const requestId = options?.requestId ?? createId();
  const kind = classify(method);
  const commandId = options?.commandId ?? (kind === "command" || kind === "compatibilityAdapter" ? createId() : undefined);
  const correlatedPayload = addCorrelation(payload, requestId, commandId, options?.domain, options?.expectedRevision);
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
    const browser = await withBridgeTimeout(callBrowserBackend<T>(method, correlatedPayload), timeoutMs, method, "browser");
    if (browser) {
      logBridge(browser.ok ? "success" : "failure", { callId, method, source: "browser", elapsedMs: elapsed(started), error: browser.ok ? undefined : browser.error });
      return browser;
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
    const expectsNative = shouldWaitForBridge();
    const bridge = await withBridgeTimeout(resolveBridge(), expectsNative ? 10000 : 2500, "backend.select", "resolveBridge").catch(() => undefined);
    if (expectsNative && (!bridge || !hasNativeTransport())) {
      throw new Error("Native host detected, but the MAUI bridge did not become available.");
    }
    selectedBackend = bridge && hasNativeTransport() ? { kind: "maui", bridge } : { kind: "browser" };
    logBridge("backend-selected", { backend: selectedBackend.kind });
    return selectedBackend;
  })();
  try {
    return await backendSelection;
  } catch (error) {
    backendSelection = undefined;
    throw error;
  }
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
    const timeout = window.setTimeout(done, 8000);

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

  return import.meta.env.MODE === "phone" || window.location.protocol === "file:" || window.location.hostname === "app.prayadfree.local";
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
    window.location.hostname === "app.prayadfree.local" ||
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
  if (typeof window !== "undefined") {
    const callId = typeof detail.callId === "number" ? detail.callId : undefined;
    window.__prayRpcPendingCalls ??= new Set<number>();
    if (event === "start" && callId !== undefined) window.__prayRpcPendingCalls.add(callId);
    if (["success", "failure", "error"].includes(event) && callId !== undefined) window.__prayRpcPendingCalls.delete(callId);
    window.dispatchEvent(new CustomEvent("pray:rpc-timing", { detail: payload }));
  }
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
  const command = lastCommandByDomain.get(domain);
  if (command && now() - command.at < 2000) logBridge("command-then-refresh", { method, requestId, commandMethod: command.method, commandRequestId: command.requestId });
}

function addCorrelation(payload: unknown, requestId: string, commandId?: string, domain?: string, expectedRevision?: number): unknown {
  const body = payload && typeof payload === "object" && !Array.isArray(payload) ? payload as Record<string, unknown> : {};
  return { ...body, _rpc: { contractVersion: coreContract.contractVersion, requestId, commandId, domain, expectedRevision } };
}

function createId(): string {
  return globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function byteSize(value: unknown): number {
  try { return new TextEncoder().encode(JSON.stringify(value)).length; } catch { return 0; }
}
