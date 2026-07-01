import { TEST } from "@/app/TEST";
import { mockHandlers } from "@/mock";

export type BridgeResponse<T = unknown> =
  | { ok: true; data: T }
  | { ok: false; error: string };

export async function mauiCall<T = unknown>(
  method: string,
  payload?: unknown,
): Promise<BridgeResponse<T>> {
  try {
    if (typeof window !== "undefined" && window.mauiWebber) {
      const res = await window.mauiWebber.call(method, payload);
      if (res && typeof res === "object" && "ok" in res) {
        return res as BridgeResponse<T>;
      }
      return { ok: true, data: res as T };
    }

    const handler = mockHandlers[method];
    if (!handler) {
      return { ok: false, error: `No mock for "${method}"` };
    }
    const data = await handler(payload);
    return { ok: true, data: data as T };
  } catch (e) {
    return { ok: false, error: e instanceof Error ? e.message : String(e) };
  }
}

export function isBridgeReady(): boolean {
  return typeof window !== "undefined" && !!window.mauiWebber;
}

export function mauiTrace(name: string, detail: Record<string, unknown> = {}): void {
  if (!isBridgeReady()) {
    return;
  }

  void mauiCall("mauiWebber.trace", {
    name,
    at: typeof performance !== "undefined" ? performance.now() : undefined,
    ...detail,
  });
}

export const BRIDGE_MODE: "maui" | "mock" =
  typeof window !== "undefined" && window.mauiWebber ? "maui" : "mock";

export { TEST };
