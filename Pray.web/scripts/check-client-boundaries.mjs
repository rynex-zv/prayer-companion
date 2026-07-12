import fs from "node:fs";
import path from "node:path";

const root = path.resolve(import.meta.dirname, "..");
const src = path.join(root, "src");
const legacyAllowlist = new Set([
  "components/AppShell.tsx", "components/BottomTabs.tsx", "components/SettingsHeader.tsx",
  "hooks/useSnapshot.ts", "hooks/useStoredSnapshot.ts", "lib/siteDataReset.ts",
  "routes/__root.tsx", "routes/alarm.tsx", "routes/calendar.tsx",
  "routes/onboarding.tsx", "routes/qibla.tsx", "routes/settings.about.tsx",
  "routes/settings.adhan.tsx", "routes/settings.locations.tsx", "routes/settings.notifications.tsx",
  "routes/settings.permissions.tsx", "routes/settings.tsx", "state/appStore.ts",
]);

const violations = [];
for (const file of walk(src)) {
  if (!/\.(ts|tsx)$/.test(file)) continue;
  const relative = path.relative(src, file).replaceAll("\\", "/");
  const source = fs.readFileSync(file, "utf8");
  if (!source.includes("native/mauiWebberClient")) continue;
  if (relative === "client/appClient.ts" || relative === "client/telemetry.ts" || legacyAllowlist.has(relative)) continue;
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
const wasmClient = fs.readFileSync(path.join(src, "native/wasmCoreClient.ts"), "utf8");
const platformAdapter = fs.readFileSync(path.join(src, "native/webPlatformAdapter.ts"), "utf8");
if (wasmClient.includes("localStorage") || wasmClient.includes("persistState")) {
  console.error("WASM is a deterministic engine and must not own browser persistence.");
  process.exit(1);
}
if ((platformAdapter.match(/settings\.getSnapshot/g) ?? []).length > 3) {
  console.error("Browser platform workflows must not use snapshot-set-snapshot chains.");
  process.exit(1);
}
console.log("Client architecture boundary passed.");

function* walk(directory) {
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    const full = path.join(directory, entry.name);
    if (entry.isDirectory()) yield* walk(full); else yield full;
  }
}
