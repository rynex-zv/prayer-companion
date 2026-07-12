# Current application architecture and verification instructions

Updated: 2026-07-12 after Phase 4 production verification.

## Runtime boundaries

- React domain reads/writes enter through `Pray.web/src/client/appClient.ts`. UI compatibility calls may temporarily use `client/legacyClient.ts`; UI code must never import `native/mauiWebberClient.ts`.
- `mauiWebberClient.ts` selects one backend for the session. A phone/file-hosted MAUI runtime must wait for the native bridge and must never fall back to the browser backend.
- The browser backend owns browser persistence in IndexedDB database `prayer-companion`, store `repositories`. If IndexedDB is unavailable it may continue with explicitly logged volatile state.
- React confirmed, request, optimistic, and UI state is memory-only.
- The old keys `pray.web.core.state` and `prayer-companion:app-state:v1` are migration inputs only. Newer IndexedDB authority always wins; stale React state must not overwrite it.
- Core/WASM still supplies the compatibility state serializer during migration. Do not describe it as fully stateless until `app.importState`/`app.exportState` and mutable `WebState` are actually removed.
- Native durable settings are owned by `ISettingsRepository`/`SettingsService`. `SettingsService` also supplies the application transaction: writes remain staged until commit and disappear on rollback.
- `WebAppRpcHandler` is a transport adapter only. It parses RPC/query metadata, measures the call, and delegates a `NativeAppOperation` to `NativeAppBackend`.
- `NativeAppBackend` owns native application dispatch. Mutating operations cross `ApplicationCoordinator`, which checks expected revisions, deduplicates command IDs with a bounded replay cache, commits before effects/events, and returns authoritative projections.
- Settings scheduling and `PrayerDataService.SettingsChanged`/widget invalidation are post-commit behavior. Never move them back into repository `Save` before transaction completion.
- Equivalent native queries are coalesced by normalized input plus the current authoritative revision.

## Startup invariants

- The shell must render safely before `app.bootstrap` completes using bundled English labels. `getLabel` must never throw during cold startup or inside an error boundary.
- Initial shell/Today state comes from one `app.bootstrap` query.
- Native hosts must select `maui`; web hosts must select `browser`. Log and fail explicitly if a detected native host has no bridge.
- A successful build is not a runtime check. Verify the script hash actually loaded by the live site and the embedded manifest version actually copied into the MAUI app.

## Required verification for storage/client changes

1. `npm run typecheck`
2. `npm run check:architecture`
3. `dotnet test PrayAdFree.Tests/PrayAdFree.Tests.csproj --no-restore`
4. `npm run build` and browser cold-start/reload checks on local preview.
5. Load `https://pray.rynex.nl/`, confirm the new asset hash, navigate representative routes, and inspect new console errors.
6. `npm run build:phone`, rebuild Windows MAUI with the frontend build disabled only after assets are synced, launch the executable, and inspect `Desktop/PrayAdFreeLogs` for `console.error`, `window.error`, `unhandledrejection`, backend selection, bootstrap completion, and runtime health.
7. Do not reuse old screenshots or logs as current proof.

## Known compatibility debt

- `legacyClient.ts`, `settings.invoke`, generic `settings.setField`, snapshot-hook names, mutable browser `WebState`, and full state import/export remain compatibility debt.
- The migration document previously marked these boundaries complete too aggressively. Remove each only with runtime parity and migration tests.
