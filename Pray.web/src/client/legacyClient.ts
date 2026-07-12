import { appClient } from "./appClient";
export { isBridgeReady, mauiTrace } from "@/native/mauiWebberClient";

/** Compatibility facade for intent names not yet given a domain-specific typed wrapper. */
export async function mauiCall<T = unknown>(name: string, payload?: unknown) {
  const result = await appClient.command<T>({ name, payload, domain: name.split(".", 1)[0] });
  return result.ok
    ? { ok: true as const, data: result.data }
    : { ok: false as const, error: result.error.message, errorInfo: result.error };
}
