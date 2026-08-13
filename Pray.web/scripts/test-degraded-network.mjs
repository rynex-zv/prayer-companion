import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { existsSync } from "node:fs";
import { cp, readFile, rm, writeFile } from "node:fs/promises";
import { resolve } from "node:path";
import { chromium } from "playwright-core";

const root = process.cwd();
const requestedPort = Number.parseInt(process.env.PRAY_NETWORK_TEST_PORT ?? "", 10);
const port = Number.isFinite(requestedPort) && requestedPort > 0
  ? requestedPort
  : 4187 + Math.floor(Math.random() * 1000);
const baseUrl = `http://127.0.0.1:${port}`;
const skipBuild = process.argv.includes("--skip-build");
const testDist = resolve(root, ".network-test-dist");

if (!skipBuild) await run(process.execPath, [resolve(root, "scripts", "build.mjs")]);
await rm(testDist, { recursive: true, force: true });
await cp(resolve(root, "dist"), testDist, { recursive: true });

const phoneHtml = existsSync(resolve(root, "dist-phone", "index.html"))
  ? await readFile(resolve(root, "dist-phone", "index.html"), "utf8")
  : "";
if (phoneHtml) {
  assert.doesNotMatch(phoneHtml, /id="pray-boot"/, "The remote-web boot overlay must not be included in the embedded phone build.");
  assert.doesNotMatch(phoneHtml, /pray\.boot\.assetRetries/, "Remote-web asset retry logic must not be included in the embedded phone HTML.");
  assert.doesNotMatch(phoneHtml, /serviceWorker|version\.web\.info|version\.web\.json|pray-sw\.js/, "Web update logic must not be included in the embedded phone HTML.");
  assert.match(phoneHtml, /data-pray-phone-style="assets\/main\.css"/, "The embedded phone build must inline the app stylesheet.");
  assert.doesNotMatch(phoneHtml, /<link\b[^>]*rel=["']stylesheet["']/i, "The embedded phone build must not depend on external stylesheet links.");
}
const bundleManifest = JSON.parse(await readFile(resolve(testDist, "webber-manifest.json"), "utf8"));
assert.equal(
  (bundleManifest.files ?? []).some((file) => String(file.path || "").startsWith("downloads/")),
  false,
  "The remote web bundle manifest must not make native hosts download APK/ZIP artifacts as app runtime files.",
);
assert.doesNotMatch(
  await readFile(resolve(testDist, "pray-sw.js"), "utf8"),
  /\/downloads\//,
  "The service worker must not precache native download artifacts.",
);

const preview = spawn(process.execPath, [resolve(root, "node_modules", "vite", "bin", "vite.js"), "preview", "--host", "127.0.0.1", "--port", String(port), "--strictPort"], {
  cwd: root,
  stdio: ["ignore", "pipe", "pipe"],
  env: { ...process.env, PRAY_WEB_OUTDIR: "../.network-test-dist" },
});
preview.stdout.pipe(process.stdout);
preview.stderr.pipe(process.stderr);

let browser;
try {
  await waitForServer(baseUrl);
  browser = await chromium.launch({ executablePath: findBrowser(), headless: true });
  await testVersionFirstWorkerUpdate(browser);
  await testForeignExtensionFailureIgnored(browser);
  await testEntryAssetRecovery(browser);
  await testPartiallyLoadedRuntimeRecovery(browser);
  await testWasmPacketLossRecovery(browser);
  await testExtremeBandwidth(browser);
  await testOfflineStatus(browser);
  await testWarmRefreshWorksFromServiceWorkerCache(browser);
  process.stdout.write("degraded network tests passed\n");
} finally {
  if (browser) await browser.close();
  await stopProcess(preview);
  await rm(testDist, { recursive: true, force: true });
}

async function testWarmRefreshWorksFromServiceWorkerCache(browser) {
  const context = await browser.newContext();
  const page = await context.newPage();
  try {
    await page.goto(baseUrl, { waitUntil: "domcontentloaded" });
    await page.waitForFunction(() => document.querySelector("#pray-boot")?.getAttribute("data-visible") === "false", null, { timeout: 120_000 });
    await page.waitForFunction(() => navigator.serviceWorker.controller !== null, null, { timeout: 30_000 });
    const hasFrameworkCache = await page.evaluate(async () => {
      const keys = (await caches.keys()).filter((key) => key.startsWith("pray-web-"));
      for (const key of keys) {
        const cache = await caches.open(key);
        const requests = await cache.keys();
        if (requests.some((request) => new URL(request.url).pathname.startsWith("/wasm/_framework/"))) return true;
      }
      return false;
    });
    assert.equal(hasFrameworkCache, true, "The first load must cache WebAssembly framework resources.");

    await stopProcess(preview);
    await page.reload({ waitUntil: "domcontentloaded" });
    await page.waitForFunction(() => document.querySelector("#pray-boot")?.getAttribute("data-visible") === "false", null, { timeout: 120_000 });
    assert.equal(await page.evaluate(() => window.prayerCompanion?.isReady() === true), true, "A warm refresh must start from service worker cache while offline.");
  } finally {
    await context.close();
  }
}

async function testForeignExtensionFailureIgnored(browser) {
  const context = await browser.newContext({ serviceWorkers: "block" });
  const page = await context.newPage();
  let mainDocumentNavigations = 0;
  page.on("framenavigated", (frame) => {
    if (frame === page.mainFrame()) mainDocumentNavigations += 1;
  });
  await page.goto(baseUrl, { waitUntil: "domcontentloaded" });
  await page.waitForFunction(() => document.querySelector("#pray-boot")?.getAttribute("data-visible") === "false", null, { timeout: 120_000 });
  const navigationsBefore = mainDocumentNavigations;
  await page.evaluate(() => {
    const foreignStyle = document.createElement("link");
    foreignStyle.rel = "stylesheet";
    foreignStyle.href = "internet-extension://lfjb.../static/css/styles.css";
    document.head.appendChild(foreignStyle);
    foreignStyle.dispatchEvent(new Event("error"));
  });
  await page.waitForTimeout(3500);
  assert.equal(mainDocumentNavigations, navigationsBefore, "A browser-extension asset failure must not reload the application.");
  assert.equal(await page.locator("#pray-boot").getAttribute("data-visible"), "false", "A browser-extension asset failure must not reopen the boot overlay.");
  await context.close();
}

async function testVersionFirstWorkerUpdate(browser) {
  const versionPath = resolve(testDist, "version.web.info");
  const metadataPath = resolve(testDist, "version.web.json");
  const workerPath = resolve(testDist, "pray-sw.js");
  const originalVersionFile = await readFile(versionPath, "utf8");
  const originalMetadataFile = await readFile(metadataPath, "utf8");
  const originalWorker = await readFile(workerPath, "utf8");
  const originalMetadata = JSON.parse(originalMetadataFile);
  const currentVersion = String(originalMetadata.version || originalVersionFile.trim());
  const currentBuild = Number(originalMetadata.build || originalVersionFile.trim() || "0");
  const nextBuild = currentBuild + 1000;
  const nextVersion = `0.0.99.${nextBuild}`;
  const context = await browser.newContext();
  const page = await context.newPage();
  const requestOrder = [];
  page.on("request", (request) => {
    const url = request.url();
    if (url.includes("version.web.json")) requestOrder.push("version");
    if (url.includes("/wasm/_framework/")) requestOrder.push("wasm");
  });
  try {
    await page.goto(baseUrl, { waitUntil: "domcontentloaded" });
    await page.waitForFunction(() => document.querySelector("#pray-boot")?.getAttribute("data-visible") === "false", null, { timeout: 120_000 });
    assert.ok(requestOrder.indexOf("version") >= 0, "The version metadata file was not requested.");
    assert.ok(requestOrder.indexOf("wasm") < 0 || requestOrder.indexOf("version") < requestOrder.indexOf("wasm"), "The version metadata check must happen before WebAssembly starts.");
    await page.waitForFunction((expected) => navigator.serviceWorker.ready.then((registration) => registration.active?.scriptURL.includes(`v=${expected}`)), currentVersion);

    await writeFile(versionPath, `${nextBuild}\n`, "utf8");
    await writeFile(metadataPath, `${JSON.stringify({
      ...originalMetadata,
      version: nextVersion,
      build: nextBuild,
      legacyVersion: String(nextBuild),
      cacheEpoch: 99,
      minimumSupportedVersion: nextVersion,
      serviceWorker: `/pray-sw.js?v=${encodeURIComponent(nextVersion)}`,
      generatedAt: new Date().toISOString(),
    }, null, 2)}\n`, "utf8");
    await writeFile(workerPath, originalWorker
      .replace(`const VERSION = ${JSON.stringify(currentVersion)};`, `const VERSION = ${JSON.stringify(nextVersion)};`)
      .replace(`const LEGACY_VERSION = ${JSON.stringify(String(currentBuild))};`, `const LEGACY_VERSION = ${JSON.stringify(String(nextBuild))};`), "utf8");
    await page.reload({ waitUntil: "domcontentloaded" });
    await page.waitForFunction(() => document.querySelector("#pray-boot")?.getAttribute("data-visible") === "false", null, { timeout: 120_000 });
    await page.waitForFunction((expected) => window.__prayWebUpdate?.getSnapshot().status === "ready" && window.__prayWebUpdate.getSnapshot().availableVersion === expected, nextVersion, { timeout: 180_000 });
    assert.equal(
      await page.evaluate(() => navigator.serviceWorker.ready.then((registration) => new URL(registration.active?.scriptURL || location.href).searchParams.get("v"))),
      currentVersion,
      "The active service worker must not switch before the user accepts the staged update.",
    );
    await page.waitForFunction(async (expected) => {
      return (await caches.keys()).includes(`pray-web-${expected}`);
    }, nextVersion, { timeout: 15_000 });

    await page.evaluate(() => {
      void window.__prayWebUpdate?.apply();
    });
    await page.waitForURL((url) => url.searchParams.get("pray-version") === nextVersion, { timeout: 30_000 });
    await page.waitForFunction(() => document.querySelector("#pray-boot")?.getAttribute("data-visible") === "false", null, { timeout: 120_000 });
    await page.waitForFunction((expected) => navigator.serviceWorker.ready.then((registration) => registration.active?.scriptURL.includes(`v=${expected}`)), nextVersion, { timeout: 30_000 });
    await page.waitForFunction(async (expected) => {
      const versionCaches = (await caches.keys()).filter((key) => key.startsWith("pray-web-"));
      return versionCaches.includes(`pray-web-${expected}`) && versionCaches.every((key) => key === `pray-web-${expected}`);
    }, nextVersion, { timeout: 30_000 });

    await writeFile(versionPath, originalVersionFile, "utf8");
    await writeFile(metadataPath, originalMetadataFile, "utf8");
    await writeFile(workerPath, originalWorker, "utf8");
  } finally {
    await writeFile(versionPath, originalVersionFile, "utf8");
    await writeFile(metadataPath, originalMetadataFile, "utf8");
    await writeFile(workerPath, originalWorker, "utf8");
    await context.close();
  }
}

async function testPartiallyLoadedRuntimeRecovery(browser) {
  const context = await browser.newContext({
    serviceWorkers: "block",
    viewport: { width: 412, height: 915 },
    isMobile: true,
    hasTouch: true,
    userAgent: "Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 Chrome/126.0 Mobile Safari/537.36 Instagram 342.0.0.0.0 Android",
  });
  const page = await context.newPage();
  let injectedFailure = false;
  let mainDocumentNavigations = 0;
  const bootWarnings = [];
  page.on("framenavigated", (frame) => {
    if (frame === page.mainFrame()) mainDocumentNavigations += 1;
  });
  page.on("console", (message) => {
    if (message.text().includes("[pray.boot]")) bootWarnings.push(message.text());
  });
  await page.route("**/wasm/_framework/dotnet.js", async (route) => {
    if (!injectedFailure) {
      injectedFailure = true;
      await route.fulfill({
        status: 200,
        contentType: "text/javascript",
        body: 'export const dotnet={create:()=>Promise.reject(new Error("Runtime module already loaded"))};',
      });
      return;
    }
    await route.continue();
  });
  await page.goto(baseUrl, { waitUntil: "domcontentloaded" });
  await page.waitForFunction(() => document.querySelector("#pray-boot")?.getAttribute("data-visible") === "false", null, { timeout: 120_000 });
  assert.equal(injectedFailure, true, "The partially loaded runtime failure was not exercised.");
  assert.ok(mainDocumentNavigations >= 2, "A contaminated runtime must restart the whole document.");
  assert.ok(bootWarnings.some((line) => line.includes("runtime_restart")), "The runtime restart must be diagnosed.");
  await page.waitForFunction(() => !/[?&]pray-restart=/.test(location.href));
  assert.doesNotMatch(page.url(), /[?&]pray-restart=/, "The recovery query marker must be removed after a successful boot.");
  await context.close();
}

async function testEntryAssetRecovery(browser) {
  const context = await browser.newContext({ serviceWorkers: "block" });
  const page = await context.newPage();
  let failedEntry = false;
  await page.route("**/assets/index-*.js", async (route) => {
    if (!failedEntry) {
      failedEntry = true;
      await route.abort("connectionreset");
      return;
    }
    await route.continue();
  });
  const started = Date.now();
  await page.goto(baseUrl, { waitUntil: "domcontentloaded" });
  await page.locator("#pray-boot[data-visible='true']").waitFor({ state: "visible", timeout: 500 });
  assert.ok(Date.now() - started < 1500, "Boot feedback must be visible immediately and independently of the entry module.");
  await page.waitForFunction(() => document.querySelector("#pray-boot")?.getAttribute("data-visible") === "false", null, { timeout: 120_000 });
  assert.equal(failedEntry, true, "The entry asset failure was not exercised.");
  await context.close();
}

async function testWasmPacketLossRecovery(browser) {
  const context = await browser.newContext({ serviceWorkers: "block" });
  const page = await context.newPage();
  let aborted = 0;
  const bootWarnings = [];
  page.on("console", (message) => {
    if (message.text().includes("[pray.boot]")) bootWarnings.push(message.text());
  });
  await page.route("**/wasm/_framework/**", async (route) => {
    const url = route.request().url();
    if (aborted < 2 && /dotnet\.native\.[^/]+\.wasm(?:\?|$)/.test(url)) {
      aborted += 1;
      await route.abort("connectionreset");
      return;
    }
    await new Promise((resolvePromise) => setTimeout(resolvePromise, 35));
    await route.continue();
  });
  await page.goto(baseUrl, { waitUntil: "domcontentloaded" });
  await page.locator("#pray-boot[data-visible='true']").waitFor({ state: "visible", timeout: 5000 });
  await page.waitForFunction(() => document.querySelector("#pray-boot")?.getAttribute("data-visible") === "false", null, { timeout: 120_000 });
  assert.equal(aborted, 2, "Repeated packet-loss failures were not exercised.");
  assert.ok(bootWarnings.some((line) => line.includes("resource_fetch_failed") || line.includes("wasm_load_failed")), "The failed WASM resource must be diagnosed.");
  await context.close();
}

async function testExtremeBandwidth(browser) {
  const context = await browser.newContext({ serviceWorkers: "block" });
  const page = await context.newPage();
  const cdp = await context.newCDPSession(page);
  await cdp.send("Network.enable");
  await cdp.send("Network.emulateNetworkConditions", {
    offline: false,
    latency: 700,
    downloadThroughput: 256 * 1024,
    uploadThroughput: 64 * 1024,
    connectionType: "cellular2g",
  });
  await page.goto(baseUrl, { waitUntil: "domcontentloaded", timeout: 30_000 });
  await page.locator("#pray-boot[data-visible='true']").waitFor({ state: "visible", timeout: 5000 });
  await page.waitForFunction(() => {
    const detail = document.querySelector("#pray-boot-detail")?.textContent ?? "";
    const progress = Number(document.querySelector("#pray-boot-progress")?.getAttribute("aria-valuenow") ?? "0");
    return /\.(?:wasm|dat)/i.test(detail) && progress > 0;
  }, null, { timeout: 120_000 });
  await page.waitForFunction(() => document.querySelector("#pray-boot")?.getAttribute("data-visible") === "false", null, { timeout: 180_000 });
  await cdp.send("Network.emulateNetworkConditions", {
    offline: false,
    latency: 0,
    downloadThroughput: -1,
    uploadThroughput: -1,
  });
  await context.close();
}

async function testOfflineStatus(browser) {
  const context = await browser.newContext({ serviceWorkers: "block" });
  const page = await context.newPage();
  await page.goto(baseUrl, { waitUntil: "domcontentloaded" });
  await context.setOffline(true);
  await page.locator("#pray-boot").evaluate((element) => {
    element.hidden = false;
    element.dataset.visible = "true";
  });
  await page.evaluate(() => window.dispatchEvent(new Event("offline")));
  await page.waitForFunction(() => document.querySelector("#pray-boot")?.getAttribute("data-state") === "offline");
  await context.close();
}

function run(command, args) {
  return new Promise((resolvePromise, rejectPromise) => {
    const child = spawn(command, args, { cwd: root, stdio: "inherit" });
    child.once("error", rejectPromise);
    child.once("exit", (code) => code === 0 ? resolvePromise() : rejectPromise(new Error(`${command} exited with ${code}`)));
  });
}

async function waitForServer(url) {
  const deadline = Date.now() + 30_000;
  while (Date.now() < deadline) {
    try {
      const response = await fetch(url);
      if (response.ok) return;
    } catch {}
    await new Promise((resolvePromise) => setTimeout(resolvePromise, 100));
  }
  throw new Error(`Preview server did not start at ${url}`);
}

async function stopProcess(processHandle) {
  if (!processHandle.pid || processHandle.exitCode !== null) return;
  if (process.platform === "win32") {
    await new Promise((resolvePromise) => {
      const stop = spawn("taskkill", ["/pid", String(processHandle.pid), "/T", "/F"], { stdio: "ignore" });
      stop.once("error", resolvePromise);
      stop.once("exit", resolvePromise);
    });
  } else {
    processHandle.kill("SIGTERM");
  }
}

function findBrowser() {
  const candidates = process.platform === "win32"
    ? [
        "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
        "C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe",
        "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
      ]
    : ["/usr/bin/microsoft-edge", "/usr/bin/google-chrome", "/usr/bin/chromium"];
  const browserPath = candidates.find(existsSync);
  if (!browserPath) throw new Error("No supported Chromium browser was found.");
  return browserPath;
}
