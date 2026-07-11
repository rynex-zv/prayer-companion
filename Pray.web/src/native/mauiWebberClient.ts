import { tryHandleWebPlatformCall } from "./webPlatformAdapter";
import { getLastWasmCoreLoadError, tryCallWasmCore } from "./wasmCoreClient";

export type BridgeResponse<T = unknown> =
  | { ok: true; data: T }
  | { ok: false; error: string };

export async function mauiCall<T = unknown>(
  method: string,
  payload?: unknown,
): Promise<BridgeResponse<T>> {
  const callId = nextCallId();
  const started = now();
  const timeoutMs = bridgeTimeoutFor(method);
  logBridge("start", { callId, method, payload, timeoutMs });

  try {
    const bridge = await withBridgeTimeout(resolveBridge(), 2500, method, "resolveBridge");
    if (bridge) {
      try {
        const res = await withBridgeTimeout(bridge.call(method, payload), mauiTimeoutFor(method, timeoutMs), method, "maui");
        logBridge("success", { callId, method, source: "maui", elapsedMs: elapsed(started) });
        if (res && typeof res === "object" && "ok" in res) {
          return res as BridgeResponse<T>;
        }
        return { ok: true, data: res as T };
      } catch (error) {
        logBridge("failure", {
          callId,
          method,
          source: "maui",
          elapsedMs: elapsed(started),
          error: error instanceof Error ? error.message : String(error),
        });
      }
    }

    const webPlatform = await withBridgeTimeout(tryHandleWebPlatformCall<T>(method, payload), timeoutMs, method, "web-platform");
    if (webPlatform) {
      logBridge(webPlatform.ok ? "success" : "failure", { callId, method, source: "web-platform", elapsedMs: elapsed(started), error: webPlatform.ok ? undefined : webPlatform.error });
      return webPlatform;
    }

    const wasm = await withBridgeTimeout(tryCallWasmCore<T>(method, payload), timeoutMs, method, "wasm");
    if (wasm) {
      logBridge(wasm.ok ? "success" : "failure", { callId, method, source: "wasm", elapsedMs: elapsed(started), error: wasm.ok ? undefined : wasm.error });
      return wasm;
    }

    const detail = getLastWasmCoreLoadError();
    logBridge("failure", { callId, method, source: "none", elapsedMs: elapsed(started), error: detail ?? "Web Core failed to load." });
    return {
      ok: false,
      error: detail ? `Web Core failed to load: ${detail}` : "Web Core failed to load.",
    };
  } catch (e) {
    const error = e instanceof Error ? e.message : String(e);
    logBridge("error", { callId, method, elapsedMs: elapsed(started), error });
    return { ok: false, error };
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

export const BRIDGE_MODE: "maui" | "wasm" =
  typeof window !== "undefined" && window.mauiWebber ? "maui" : "wasm";

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

function mauiTimeoutFor(method: string, defaultTimeoutMs: number): number {
  if (!isImplicitPhoneBridgeWithoutNativeTransport()) {
    return defaultTimeoutMs;
  }

  if (method === "mauiWebber.pullRemote") {
    return 2500;
  }

  return 800;
}

function isImplicitPhoneBridgeWithoutNativeTransport(): boolean {
  if (typeof window === "undefined" || import.meta.env.MODE !== "phone") {
    return false;
  }

  const nativeWindow = window as Window & {
    chrome?: { webview?: { postMessage?: unknown } };
    mauiWebberNative?: { postMessage?: unknown };
  };

  return window.location.protocol !== "file:" &&
    !nativeWindow.mauiWebberNative?.postMessage &&
    !nativeWindow.chrome?.webview?.postMessage;
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
  const payload = { event, ...detail };
  console.info(`[pray.bridge] ${JSON.stringify(payload)}`);
  if (event !== "start") {
    mauiTrace(`bridge.${event}`, payload);
  }
}
