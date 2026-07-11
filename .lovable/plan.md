## Scope
Five product fixes + one internal build-automation task.

### 1) Active alarm screen in React (`/alarm`)
- New `Pray.web/src/routes/alarm.tsx` polling `alarm.getSnapshot`, calling `alarm.snooze` / `alarm.stop` via `mauiCall`.
- All labels from Core i18n snapshot — no hardcoded strings.
- Add `alarm.*` methods in `PrayAdFree.Core` (`WebCoreRpcDispatcher`, `WebCatalog`, snapshot factory) so `core-contract.json` regenerates with them.
- Native MAUI (`AdhanSnoozePage`, Android `AlarmActivity`) becomes a thin host that opens `/alarm` in MauiWebber and forwards platform actions (wake screen, stop audio). Business logic stays in `AdhanPlaybackService`.

### 2) Desktop viewport clamp
- In `AppShell.tsx`, clamp scrollable content on `md:` so pages never overflow the phone frame. Inner content: `overflow-y-auto`, `max-h: min(880px, 90vh)`.

### 3) Islamic-style custom scrollbar (global)
- Global rules in `Pray.web/src/styles.css`: WebKit `::-webkit-scrollbar` + Firefox `scrollbar-*`. Thin track, primary-colored thumb, geometric arabesque detail, rounded ends. Applied to `html, body` and every overflow container, all devices.

### 4) Tasbih bead redesign
- Replace flat circles in `tasbih.tsx` with layered beads: radial gradient body, inner highlight, etched geometric ring (SVG mask or CSS). Keep no-animation rule (no bump / zoom / vibration). Only color/opacity swap active vs inactive.

### 5) Qibla map — great-circle arrow
- Rewrite `QiblaMap.tsx`:
  - Sample great-circle waypoints via slerp between user and Kaaba (21.4225, 39.8262).
  - Project to Web Mercator → curved SVG polyline.
  - Arrow terminates on Kaaba marker when it's on-screen; clamps to viewport edge along the great-circle tangent when it's off-screen.
  - Higher default zoom + zoom controls; recompute on zoom/pan. No new deps.

### 6) Internal: reliable build automation (for the agent, not a user feature)
- One script (`Pray.web/scripts/build-all.mjs`, `bun run build:all`) that does DLL build → contract/WASM gen → Vite build in the right order, with clean + fail-fast logs.
- Point `lovable.toml` `[run].build` at it so the Lovable build path never skips a step.
- Purpose: stop re-debugging build steps every turn. Not surfaced in the app UI.

## Order
Phase A (fast, React-only): 2, 3, 4.
Phase B: 6 — get the build script working end-to-end.
Phase C: 1 — Core `alarm.*` RPC + regenerated contract + React `/alarm` page. Native host wiring documented.
Phase D: 5 — great-circle map arrow.
