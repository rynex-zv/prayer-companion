import fs from "node:fs";
import path from "node:path";

const root = path.resolve(import.meta.dirname, "..");
const src = path.join(root, "src");
const legacyAllowlist = new Set([
  "components/AppShell.tsx", "components/BottomTabs.tsx", "components/SettingsHeader.tsx",
  "hooks/useSnapshot.ts", "hooks/useStoredSnapshot.ts", "lib/siteDataReset.ts",
  "routes/__root.tsx", "routes/alarm.tsx", "routes/calendar.tsx", "routes/index.tsx",
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
  if (relative === "client/appClient.ts" || legacyAllowlist.has(relative)) continue;
  violations.push(relative);
}

if (violations.length) {
  console.error("New UI/domain code must use AppClient, not mauiWebberClient:\n" + violations.map((file) => ` - ${file}`).join("\n"));
  process.exit(1);
}
console.log("Client architecture boundary passed.");

function* walk(directory) {
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    const full = path.join(directory, entry.name);
    if (entry.isDirectory()) yield* walk(full); else yield full;
  }
}
