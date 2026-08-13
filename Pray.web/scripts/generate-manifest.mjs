import { createHash } from 'node:crypto';
import { readdir, readFile, writeFile } from 'node:fs/promises';
import { join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const distDir = process.env.PRAY_WEB_DIST_DIR || 'dist';
const root = new URL(`../${distDir}/`, import.meta.url);
const rootPath = fileURLToPath(root);
const target = process.argv.includes('--phone') ? 'phone' : 'web';
const versionPath = resolve(rootPath, '..', 'version.web.info');
const contractPath = resolve(rootPath, '..', 'src', 'generated', 'core-contract.json');

async function nextBuildVersion() {
  const requested = Number.parseInt(process.env.PRAY_WEB_BUILD_VERSION ?? '', 10);
  if (Number.isFinite(requested) && requested > 0) {
    await writeFile(versionPath, `${requested}\n`, 'utf8');
    return String(requested);
  }

  let current = 0;
  try {
    const raw = (await readFile(versionPath, 'utf8')).trim();
    current = Number.parseInt(raw, 10);
  } catch {
    current = 0;
  }

  const next = Number.isFinite(current) && current >= 0 ? current + 1 : 1;
  await writeFile(versionPath, `${next}\n`, 'utf8');
  return String(next);
}

async function walk(dir) {
  const entries = await readdir(dir, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) {
      files.push(...await walk(path));
    } else if (
      entry.name !== 'webber-manifest.json' &&
      entry.name !== 'version.web.info' &&
      entry.name !== 'version.web.json' &&
      entry.name !== 'web.config' &&
      !entry.name.endsWith('.br') &&
      !entry.name.endsWith('.gz')
    ) {
      files.push(path);
    }
  }
  return files;
}

const build = await nextBuildVersion();
const cacheEpoch = Number.parseInt(process.env.PRAY_WEB_CACHE_EPOCH ?? '1', 10);
const safeCacheEpoch = Number.isFinite(cacheEpoch) && cacheEpoch >= 0 ? cacheEpoch : 1;
const displayVersion = process.env.PRAY_WEB_VERSION || `0.0.${safeCacheEpoch}.${build}`;
const minimumSupportedVersion = process.env.PRAY_WEB_MIN_VERSION || displayVersion;
const contract = JSON.parse(await readFile(contractPath, 'utf8'));
const versionMetadata = {
  version: displayVersion,
  build: Number(build),
  legacyVersion: build,
  cacheEpoch: safeCacheEpoch,
  minimumSupportedVersion,
  manifest: `/webber-manifest.json?v=${encodeURIComponent(build)}`,
  generatedAt: new Date().toISOString(),
  ...(target === 'web' ? { serviceWorker: `/pray-sw.js?v=${encodeURIComponent(displayVersion)}` } : {})
};
await writeFile(join(rootPath, 'version.web.info'), `${build}\n`, 'utf8');
await writeFile(join(rootPath, 'version.web.json'), `${JSON.stringify(versionMetadata, null, 2)}\n`, 'utf8');

const files = [];
for (const file of await walk(rootPath)) {
  const bytes = await readFile(file);
  files.push({
    path: relative(rootPath, file).replaceAll('\\', '/'),
    sha256: createHash('sha256').update(bytes).digest('hex')
  });
}

files.sort((a, b) => a.path.localeCompare(b.path));
const bundleFiles = files.filter((file) => !file.path.startsWith('downloads/'));

await writeFile(
  join(rootPath, 'webber-manifest.json'),
  JSON.stringify({
    version: build,
    displayVersion,
    cacheVersion: displayVersion,
    minimumSupportedVersion,
    contractVersion: contract.schemaVersion,
    target,
    entry: 'index.html',
    files: bundleFiles
  }, null, 2)
);

if (target === 'web') {
  const precacheUrls = bundleFiles.map((file) => `/${file.path}`);
  if (!precacheUrls.includes('/index.html')) precacheUrls.unshift('/index.html');
  const worker = `const VERSION = ${JSON.stringify(displayVersion)};
const LEGACY_VERSION = ${JSON.stringify(build)};
const CACHE_NAME = "pray-web-" + VERSION;
const CACHE_PREFIX = "pray-web-";
const PRECACHE_URLS = ${JSON.stringify(precacheUrls, null, 2)};

self.addEventListener("install", (event) => event.waitUntil(precacheVersion()));
self.addEventListener("activate", (event) => event.waitUntil(self.clients.claim()));
self.addEventListener("message", (event) => {
  if (event.data?.type === "SKIP_WAITING") self.skipWaiting();
  if (event.data?.type === "COMMIT_VERSION" && event.data.version === VERSION) {
    event.waitUntil(caches.keys().then((keys) => Promise.all(
      keys.filter((key) => key.startsWith(CACHE_PREFIX) && key !== CACHE_NAME).map((key) => caches.delete(key))
    )));
  }
});

self.addEventListener("fetch", (event) => {
  const request = event.request;
  if (request.method !== "GET") return;
  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return;
  if (url.pathname === "/version.web.info" || url.pathname === "/version.web.json" || url.pathname === "/pray-sw.js" || url.pathname === "/webber-manifest.json") {
    event.respondWith(fetch(request, { cache: "no-store" }));
    return;
  }
  if (request.mode === "navigate" || url.pathname === "/" || url.pathname === "/index.html") {
    event.respondWith(shellFirst(request));
    return;
  }
  if (url.pathname.startsWith("/assets/") || url.pathname.startsWith("/wasm/_framework/")) {
    event.respondWith(cacheFirst(request));
  }
});

async function shellFirst(request) {
  const cached = await findShellInVersionCaches(request, true);
  if (cached) return cached;
  try {
    const response = await fetch(request, { cache: "no-store" });
    if (response.ok) (await caches.open(CACHE_NAME)).put(request, response.clone()).catch(() => {});
    return response;
  } catch (error) {
    const cached = await findInVersionCaches(request) || await findShellInVersionCaches(request, false);
    if (cached) return cached;
    throw error;
  }
}

async function precacheVersion() {
  const cache = await caches.open(CACHE_NAME);
  for (const path of PRECACHE_URLS) {
    const response = await fetch(path, { cache: "reload" });
    if (!response.ok) throw new Error("Precache failed: " + path + " HTTP " + response.status);
    await cache.put(new Request(path), response.clone());
    if (path === "/index.html") await cache.put(new Request("/"), response.clone());
  }
}

async function cacheFirst(request) {
  const current = await caches.open(CACHE_NAME);
  const cached = await current.match(request);
  if (cached) return withCacheStatus(cached, "hit");
  try {
    const response = await fetch(request);
    if (response.ok) current.put(request, response.clone()).catch(() => {});
    return withCacheStatus(response, "miss");
  } catch (error) {
    const previous = await findInVersionCaches(request);
    if (previous) return withCacheStatus(previous, "stale");
    throw error;
  }
}

function withCacheStatus(response, value) {
  const headers = new Headers(response.headers);
  headers.set("X-Pray-Cache", value);
  return new Response(response.body, {
    status: response.status,
    statusText: response.statusText,
    headers
  });
}

async function findInVersionCaches(request) {
  const keys = await versionCacheKeys(true);
  for (const key of keys) {
    const match = await (await caches.open(key)).match(request, { ignoreVary: true });
    if (match) return match;
  }
  return undefined;
}

async function findShellInVersionCaches(request, preferCurrent) {
  const url = new URL(request.url);
  if (request.mode !== "navigate" && url.pathname !== "/" && url.pathname !== "/index.html") return undefined;
  const keys = await versionCacheKeys(preferCurrent);
  for (const key of keys) {
    const cache = await caches.open(key);
    const match = await cache.match("/") || await cache.match("/index.html");
    if (match) return match;
  }
  return undefined;
}

async function versionCacheKeys(preferCurrent) {
  const keys = (await caches.keys()).filter((key) => key.startsWith(CACHE_PREFIX));
  if (!preferCurrent) return keys.reverse();
  return [
    ...keys.filter((key) => key === CACHE_NAME),
    ...keys.filter((key) => key !== CACHE_NAME).reverse()
  ];
}
`;
  await writeFile(join(rootPath, 'pray-sw.js'), worker, 'utf8');
}
