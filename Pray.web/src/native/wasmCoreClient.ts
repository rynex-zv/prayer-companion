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

type StatefulWasmCall = (stateJson: string, method: string, payloadJson: string) => string;

export type WasmExecutionResponse<T> = BridgeResponse<T> & { state?: string };

let loadPromise: Promise<StatefulWasmCall | undefined> | undefined;
let lastLoadError = "";

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

  loadPromise ??= (async () => {
    try {
      const dotnetUrl = resolveWasmFrameworkUrl("dotnet.js");
      const mod = (await import(/* @vite-ignore */ dotnetUrl)) as DotnetModule;
      const runtime = await mod.dotnet.create();
      const config = runtime.getConfig();
      const exports = await runtime.getAssemblyExports(config.mainAssemblyName);
      const bridge = (((exports.PrayAdFree as Record<string, unknown>)?.WebBridge as Record<string, unknown>)?.WebRpcBridge as Record<string, unknown>);
      const call = bridge?.CallWithState as StatefulWasmCall | undefined;
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
