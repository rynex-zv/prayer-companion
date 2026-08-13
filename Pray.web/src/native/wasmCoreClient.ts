import type { BridgeResponse } from "./mauiWebberClient";
import { reportWebBoot, restartWebBoot } from "./webBoot";

type DotnetRuntime = {
  getAssemblyExports: (assemblyName: string) => Promise<Record<string, unknown>>;
  getConfig: () => { mainAssemblyName: string };
};

type DotnetModule = {
  dotnet: {
    create: () => Promise<DotnetRuntime>;
  };
};

type StatefulWasmCall = (stateJson: string, method: string, payloadJson: string) => string;

export type WasmExecutionResponse<T> = BridgeResponse<T> & { state?: string };

let loadPromise: Promise<StatefulWasmCall | undefined> | undefined;
let lastLoadError = "";
let retryWake: (() => void) | undefined;
let listenersAttached = false;
let fetchDiagnosticsInstalled = false;

export async function executeWasmCore<T = unknown>(
  state: string | undefined,
  method: string,
  payload?: unknown,
): Promise<WasmExecutionResponse<T> | undefined> {
  const call = await loadWasmCore();
  if (!call) return undefined;
  return JSON.parse(call(state ?? "", method, JSON.stringify(payload ?? {}))) as WasmExecutionResponse<T>;
}

async function loadWasmCore(): Promise<StatefulWasmCall | undefined> {
  if (typeof window === "undefined") {
    return undefined;
  }

  attachRetryListeners();
  installWasmFetchDiagnostics();
  loadPromise ??= (async () => {
    let attempt = 0;
    for (;;) {
      attempt += 1;
      let runtimeInitializationStarted = false;
      try {
        reportWebBoot("loading", undefined, `WebAssembly attempt ${attempt}`);
        const dotnetUrl = resolveWasmFrameworkUrl("dotnet.js");
        const mod = (await import(/* @vite-ignore */ dotnetUrl)) as DotnetModule;
        runtimeInitializationStarted = true;
        const runtime = await mod.dotnet.create();
        const config = runtime.getConfig();
        const exports = await runtime.getAssemblyExports(config.mainAssemblyName);
        const bridge = (((exports.PrayAdFree as Record<string, unknown>)?.WebBridge as Record<string, unknown>)?.WebRpcBridge as Record<string, unknown>);
        const call = bridge?.CallWithState as StatefulWasmCall | undefined;
        if (typeof call !== "function") {
          throw new Error("The shared calculation engine did not expose its RPC entry point.");
        }

        lastLoadError = "";
        return call;
      } catch (error) {
        lastLoadError = error instanceof Error ? error.message : String(error);
        if (runtimeInitializationStarted && isContaminatedRuntimeError(lastLoadError)) {
          console.warn(`[pray.boot] runtime_restart error=${lastLoadError}`);
          if (restartWebBoot(lastLoadError)) {
            return await new Promise<StatefulWasmCall>(() => undefined);
          }
          throw error;
        }
        const offline = !navigator.onLine;
        const delayMs = offline ? 15000 : Math.min(15000, 750 * (2 ** Math.min(attempt - 1, 5))) + Math.round(Math.random() * 400);
        console.warn(`[pray.boot] wasm_load_failed attempt=${attempt} retryMs=${delayMs} online=${navigator.onLine} error=${lastLoadError}`);
        reportWebBoot(offline ? "offline" : attempt > 2 ? "slow" : "failed", undefined, `${lastLoadError} · retry ${attempt} in ${Math.ceil(delayMs / 1000)}s`);
        await waitForRetry(delayMs);
      }
    }
  })();

  return loadPromise;
}

function isContaminatedRuntimeError(message: string): boolean {
  return /runtime module already loaded|already.*(?:initialized|started)|multiple.*runtime/i.test(message);
}

function installWasmFetchDiagnostics(): void {
  if (fetchDiagnosticsInstalled || typeof window.fetch !== "function") return;
  fetchDiagnosticsInstalled = true;
  const originalFetch = window.fetch.bind(window);
  window.fetch = async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    const requestUrl = typeof input === "string" ? input : input instanceof URL ? input.href : input.url;
    const method = (init?.method ?? (input instanceof Request ? input.method : "GET")).toUpperCase();
    if (method !== "GET" || !requestUrl.includes("/wasm/_framework/")) return originalFetch(input, init);
    let lastError: unknown;
    for (let attempt = 1; attempt <= 3; attempt += 1) {
      const resourceName = safeResourceName(requestUrl);
      try {
        const response = await originalFetch(input, init);
        const cacheStatus = response.headers.get("X-Pray-Cache");
        if (response.ok && (cacheStatus === "hit" || cacheStatus === "stale")) {
          return response;
        }
        if (response.ok) {
          window.__prayBoot?.resource(resourceName, 0, 0);
          return withDownloadProgress(response, resourceName);
        }
        if (![408, 425, 429, 500, 502, 503, 504].includes(response.status)) return response;
        lastError = new Error(`HTTP ${response.status} ${response.statusText}`);
      } catch (error) {
        lastError = error;
      }
      const message = lastError instanceof Error ? lastError.message : String(lastError);
      const delayMs = Math.min(2000, 250 * (2 ** (attempt - 1))) + Math.round(Math.random() * 150);
      console.warn(`[pray.boot] resource_fetch_failed file=${resourceName} attempt=${attempt} retryMs=${delayMs} error=${message}`);
      reportWebBoot(navigator.onLine ? "slow" : "offline", undefined, `${resourceName} · retry ${attempt}/3`);
      if (attempt < 3) await waitForRetry(delayMs);
    }
    throw lastError instanceof Error ? lastError : new Error(String(lastError));
  };
}

function withDownloadProgress(response: Response, resourceName: string): Response {
  const total = Number(response.headers.get("Content-Length") ?? "0");
  if (!response.body || !Number.isFinite(total) || total <= 0) return response;
  const reader = response.body.getReader();
  let loaded = 0;
  const stream = new ReadableStream<Uint8Array>({
    async pull(controller) {
      try {
        const result = await reader.read();
        if (result.done) {
          window.__prayBoot?.resource(resourceName, total, total);
          controller.close();
          return;
        }
        loaded += result.value.byteLength;
        window.__prayBoot?.resource(resourceName, loaded, total);
        controller.enqueue(result.value);
      } catch (error) {
        controller.error(error);
      }
    },
    cancel(reason) {
      return reader.cancel(reason);
    },
  });
  return new Response(stream, {
    status: response.status,
    statusText: response.statusText,
    headers: response.headers,
  });
}

function safeResourceName(value: string): string {
  try {
    return new URL(value, document.baseURI).pathname.split("/").pop() || "WebAssembly resource";
  } catch {
    return "WebAssembly resource";
  }
}

function attachRetryListeners(): void {
  if (listenersAttached) return;
  listenersAttached = true;
  const wake = () => retryWake?.();
  window.addEventListener("pray:boot-retry", wake);
  window.addEventListener("online", wake);
}

function waitForRetry(delayMs: number): Promise<void> {
  return new Promise((resolve) => {
    let settled = false;
    const timeout = window.setTimeout(finish, delayMs);
    function finish() {
      if (settled) return;
      settled = true;
      window.clearTimeout(timeout);
      if (retryWake === finish) retryWake = undefined;
      resolve();
    }
    retryWake = finish;
  });
}

export async function preloadWasmCore(): Promise<void> {
  await loadWasmCore();
}

export function getLastWasmCoreLoadError(): string {
  return lastLoadError;
}

function resolveWasmFrameworkUrl(fileName: string): string {
  const currentScript = document.currentScript instanceof HTMLScriptElement
    ? document.currentScript.src
    : "";
  const bundleScript = currentScript ||
    Array.from(document.scripts)
      .map((script) => script.src)
      .find((src) => /\/assets\/index-[^/]+\.js(?:$|\?)/.test(src)) ||
    document.baseURI;

  return new URL(`../wasm/_framework/${fileName}`, bundleScript).href;
}
