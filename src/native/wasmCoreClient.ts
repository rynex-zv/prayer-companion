import type { BridgeResponse } from "./mauiWebberClient";

type DotnetRuntime = {
  getAssemblyExports: (assemblyName: string) => Promise<Record<string, unknown>>;
  getConfig: () => { mainAssemblyName: string };
};

type DotnetModule = {
  dotnet: {
    create: () => Promise<DotnetRuntime>;
  };
};

type WasmCall = (method: string, payloadJson: string) => string;

const STORAGE_KEY = "pray.web.core.state";
let loadPromise: Promise<WasmCall | undefined> | undefined;
let lastLoadError = "";

export async function tryCallWasmCore<T = unknown>(
  method: string,
  payload?: unknown,
): Promise<BridgeResponse<T> | undefined> {
  const call = await loadWasmCore();
  if (!call) {
    return undefined;
  }

  const raw = call(method, JSON.stringify(payload ?? {}));
  const response = JSON.parse(raw) as BridgeResponse<T>;
  if (response.ok && isMutatingMethod(method)) {
    persistState(call);
  }

  return response;
}

async function loadWasmCore(): Promise<WasmCall | undefined> {
  if (typeof window === "undefined") {
    return undefined;
  }

  loadPromise ??= (async () => {
    try {
      const dotnetUrl = new URL("./wasm/_framework/dotnet.js", document.baseURI).href;
      const mod = (await import(/* @vite-ignore */ dotnetUrl)) as DotnetModule;
      const runtime = await mod.dotnet.create();
      const config = runtime.getConfig();
      const exports = await runtime.getAssemblyExports(config.mainAssemblyName);
      const bridge = (((exports.PrayAdFree as Record<string, unknown>)?.WebBridge as Record<string, unknown>)?.WebRpcBridge as Record<string, unknown>);
      const call = bridge?.Call as WasmCall | undefined;
      if (typeof call !== "function") {
        return undefined;
      }

      const saved = window.localStorage.getItem(STORAGE_KEY);
      if (saved) {
        call("app.importState", JSON.stringify({ state: saved }));
      }

      return call;
    } catch (error) {
      lastLoadError = error instanceof Error ? error.message : String(error);
      return undefined;
    }
  })();

  return loadPromise;
}

export function getLastWasmCoreLoadError(): string {
  return lastLoadError;
}

function persistState(call: WasmCall) {
  try {
    const exported = JSON.parse(call("app.exportState", "{}")) as BridgeResponse<string>;
    if (exported.ok && typeof exported.data === "string") {
      window.localStorage.setItem(STORAGE_KEY, exported.data);
    }
  } catch {
    // Persistence is best-effort; the in-memory WASM state still keeps this session working.
  }
}

function isMutatingMethod(method: string): boolean {
  return method.startsWith("settings.") ||
    method.startsWith("tasbih.") ||
    method.startsWith("qibla.") ||
    method.startsWith("calendar.") ||
    method === "app.setLanguage" ||
    method === "app.setTheme" ||
    method === "onboarding.complete";
}
