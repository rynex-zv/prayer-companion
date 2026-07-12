import fs from "node:fs";
import path from "node:path";

const root = path.resolve(import.meta.dirname, "..");
const src = path.join(root, "src");
const violations = [];
for (const file of walk(src)) {
  if (!/\.(ts|tsx)$/.test(file)) continue;
  const relative = path.relative(src, file).replaceAll("\\", "/");
  const source = fs.readFileSync(file, "utf8");
  if (source.includes("localStorage") && relative !== "native/browserAppBackend.ts") {
    violations.push(`${relative} (localStorage ownership)`);
    continue;
  }
  if (!source.includes("native/mauiWebberClient")) continue;
  if (relative === "client/appClient.ts" || relative === "client/legacyClient.ts" || relative === "client/telemetry.ts") continue;
  violations.push(relative);
}
if (violations.length) {
  console.error("New UI/domain code must use AppClient, not mauiWebberClient:\n" + violations.map((file) => ` - ${file}`).join("\n"));
  process.exit(1);
}
const todayRoute = fs.readFileSync(path.join(src, "routes/index.tsx"), "utf8");
const appStore = fs.readFileSync(path.join(src, "state/appStore.ts"), "utf8");
if (todayRoute.includes("today.getSnapshot") || todayRoute.includes("mauiCall(")) {
  console.error("The initial Today route must render from app.bootstrap, not issue a route snapshot call.");
  process.exit(1);
}
if (!appStore.includes("appClient.bootstrap") || appStore.includes('mauiCall<ShellSnapshot>("app.getShellSnapshot")')) {
  console.error("Shell startup must use the single grouped app.bootstrap query.");
  process.exit(1);
}
if (appStore.includes("localStorage") || appStore.includes("saveState(") || appStore.includes("loadState(")) {
  console.error("React confirmed/request/sync state must remain memory-only.");
  process.exit(1);
}
const wasmClient = fs.readFileSync(path.join(src, "native/wasmCoreClient.ts"), "utf8");
const platformAdapter = fs.readFileSync(path.join(src, "native/webPlatformAdapter.ts"), "utf8");
if (wasmClient.includes("localStorage") || wasmClient.includes("persistState")) {
  console.error("WASM is a deterministic engine and must not own browser persistence.");
  process.exit(1);
}
const browserBackend = fs.readFileSync(path.join(src, "native/browserAppBackend.ts"), "utf8");
const calendarRoute = fs.readFileSync(path.join(src, "routes/calendar.tsx"), "utf8");
const cacheReset = fs.readFileSync(path.join(src, "lib/siteDataReset.ts"), "utf8");
if (browserBackend.includes("localStorage.setItem")) {
  console.error("Legacy localStorage keys are migration inputs only and must never be written.");
  process.exit(1);
}
if (browserBackend.includes("app.importState") || browserBackend.includes("app.exportState")) {
  console.error("Browser authority must pass explicit state through the deterministic WASM boundary.");
  process.exit(1);
}
if (!wasmClient.includes("CallWithState") || wasmClient.includes("tryCallWasmCore")) {
  console.error("WASM calls must use the explicit state-in/state-out execution contract.");
  process.exit(1);
}
if ((platformAdapter.match(/settings\.getSnapshot/g) ?? []).length > 2) {
  console.error("Browser platform workflows must execute as one repository transaction without follow-up snapshots.");
  process.exit(1);
}
if (calendarRoute.includes("localStorage")) {
  console.error("Calendar presentation state must remain memory-only.");
  process.exit(1);
}
if (cacheReset.includes("indexedDB") || cacheReset.includes("localStorage") || cacheReset.includes("clearIndexedDb")) {
  console.error("Cache eviction must never delete or rewrite authoritative browser repositories.");
  process.exit(1);
}
console.log("Client architecture boundary passed.");

function* walk(directory) {
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    const full = path.join(directory, entry.name);
    if (entry.isDirectory()) yield* walk(full); else yield full;
  }
}
