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
  return response;
}

async function loadWasmCore(): Promise<WasmCall | undefined> {
  if (typeof window === "undefined") {
    return undefined;
  }

  loadPromise ??= (async () => {
    try {
      const dotnetUrl = resolveWasmFrameworkUrl("dotnet.js");
      const mod = (await import(/* @vite-ignore */ dotnetUrl)) as DotnetModule;
      const runtime = await mod.dotnet.create();
      const config = runtime.getConfig();
      const exports = await runtime.getAssemblyExports(config.mainAssemblyName);
      const bridge = (((exports.PrayAdFree as Record<string, unknown>)?.WebBridge as Record<string, unknown>)?.WebRpcBridge as Record<string, unknown>);
      const call = bridge?.Call as WasmCall | undefined;
      if (typeof call !== "function") {
        return undefined;
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
