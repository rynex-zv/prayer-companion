import { isBridgeReady, mauiCall } from "@/native/mauiWebberClient";

export type DataRestoreSource = "localStorage" | "backend";

const USER_DATA_KEYS = [
  "prayer-companion:app-state:v1",
  "pray.web.core.state",
] as const;

export async function clearApplicationSiteData(source: DataRestoreSource): Promise<void> {
  const preserved = source === "localStorage" ? preserveUserData() : new Map<string, string>();

  if (isBridgeReady()) {
    const nativeResult = await mauiCall("mauiWebber.clearSiteData");
    if (!nativeResult.ok) {
      throw new Error(nativeResult.error);
    }
  }

  await Promise.allSettled([
    unregisterServiceWorkers(),
    clearCacheStorage(),
    clearIndexedDb(),
    clearOriginPrivateFileSystem(),
  ]);

  clearCookies();
  window.sessionStorage.clear();
  window.localStorage.clear();

  for (const [key, value] of preserved) {
    window.localStorage.setItem(key, value);
  }

  window.location.reload();
}

function preserveUserData(): Map<string, string> {
  const preserved = new Map<string, string>();
  for (const key of USER_DATA_KEYS) {
    const value = window.localStorage.getItem(key);
    if (value !== null) {
      preserved.set(key, value);
    }
  }
  return preserved;
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

async function clearIndexedDb(): Promise<void> {
  if (!("indexedDB" in window) || typeof window.indexedDB.databases !== "function") return;
  const databases = await window.indexedDB.databases();
  await Promise.all(databases.map((database) => new Promise<void>((resolve) => {
    if (!database.name) {
      resolve();
      return;
    }

    const request = window.indexedDB.deleteDatabase(database.name);
    request.onsuccess = () => resolve();
    request.onerror = () => resolve();
    request.onblocked = () => resolve();
  })));
}

async function clearOriginPrivateFileSystem(): Promise<void> {
  const storage = navigator.storage as StorageManager & {
    getDirectory?: () => Promise<FileSystemDirectoryHandle>;
  };
  if (typeof storage.getDirectory !== "function") return;

  const root = await storage.getDirectory();
  const entries = (root as FileSystemDirectoryHandle & {
    keys?: () => AsyncIterableIterator<string>;
  }).keys;
  if (!entries) return;

  for await (const name of entries.call(root)) {
    await root.removeEntry(name, { recursive: true });
  }
}

function clearCookies(): void {
  const hostParts = window.location.hostname.split(".").filter(Boolean);
  const domains = ["", ...hostParts.map((_, index) => `.${hostParts.slice(index).join(".")}`)];
  const paths = ["/", window.location.pathname || "/"];

  for (const cookie of document.cookie.split(";")) {
    const name = cookie.split("=")[0]?.trim();
    if (!name) continue;
    for (const domain of domains) {
      for (const path of paths) {
        document.cookie = `${name}=; Max-Age=0; path=${path}${domain ? `; domain=${domain}` : ""}`;
      }
    }
  }
}
