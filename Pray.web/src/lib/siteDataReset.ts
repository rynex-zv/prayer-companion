import { isBridgeReady, mauiCall } from "@/client/legacyClient";

/** Clears only reconstructable web/native caches. Authoritative IndexedDB and settings are deliberately preserved. */
export async function clearApplicationCaches(): Promise<void> {
  if (isBridgeReady()) {
    const nativeResult = await mauiCall("mauiWebber.clearSiteData");
    if (!nativeResult.ok) throw new Error(nativeResult.error);
  }

  await Promise.allSettled([
    unregisterServiceWorkers(),
    clearCacheStorage(),
  ]);
  window.sessionStorage.clear();
  window.location.reload();
}

async function unregisterServiceWorkers(): Promise<void> {
  if (!("serviceWorker" in navigator)) return;
  const registrations = await navigator.serviceWorker.getRegistrations();
  await Promise.all(registrations.map((registration) => registration.unregister()));
}

async function clearCacheStorage(): Promise<void> {
  if (!("caches" in window)) return;
  const keys = await window.caches.keys();
  await Promise.all(keys.map((key) => window.caches.delete(key)));
}
