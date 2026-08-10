export type RuntimeObservation = {
  message: string;
  source: "application" | "browser-extension";
};

const EXTENSION_URL = /(?:chrome|moz|safari-web)-extension:\/\/[^\s)]+/i;
const ANY_URL = /(?:chrome|moz|safari-web)-extension:\/\/[^\s)]+|https?:\/\/[^\s)]+|file:\/\/[^\s)]+/i;

export function observeRuntimeValue(value: unknown, sourceUrl?: string): RuntimeObservation {
  const message = describeRuntimeValue(value);
  const firstUrl = sourceUrl?.match(ANY_URL)?.[0] ?? message.match(ANY_URL)?.[0] ?? "";
  return {
    message,
    source: EXTENSION_URL.test(firstUrl) ? "browser-extension" : "application",
  };
}

export function describeRuntimeValue(value: unknown): string {
  if (value instanceof Error) return value.stack ?? value.message;
  if (typeof value === "string") return value;
  try { return JSON.stringify(value); } catch { return String(value); }
}
