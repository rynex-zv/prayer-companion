# Prayer Companion agent entry point

Read these files in order before changing application state, transport, storage, or startup behavior:

1. `../storage-edit.md` — architecture requirements and migration history.
2. `mds/current-architecture.md` — current implementation boundaries and mandatory runtime checks.
3. `mds/linkinHistory.md` — page-by-page verification history. Treat old evidence as historical, not proof for the current build.

The files `mds/old.md`, `mds/new.md`, and `mds/newer.md` are historical UI inventories. They are not current implementation instructions.

Never call a change production-ready from compilation alone. For storage/startup changes, test a cold browser origin, a reload with IndexedDB state, `https://pray.rynex.nl/`, and a freshly rebuilt Windows MAUI executable. Record the exact bundle hash and console/runtime errors.

Phase 4 is complete. Preserve the native boundary documented in `mds/current-architecture.md`: transport metadata stays in `WebAppRpcHandler`, workflows stay in `NativeAppBackend`, durable mutations use `ApplicationCoordinator` plus the repository transaction, and external effects/events occur after commit.

Phase 5 is complete. Backend projection ports must resolve to application services under `Services/`, never classes under `ViewModels/`. XAML ViewModels may adapt commands, binding, and device feedback only; do not put persistence, scheduling, location acquisition, calculations, or app-lifetime subscriptions back into them.

Phase 6 is complete. The browser must load repository state, call `CallWithState`, and commit the returned replacement state. Never restore a static WASM dispatcher, browser `app.importState`/`app.exportState` persistence, or platform snapshot-set-snapshot workflows.

Phase 7 is complete. Browser authority is IndexedDB schema 4 and native authority is the settings repository transaction. Cache clearing must preserve both. Treat prayer v3, Today v2, geo v1, and the platform notification schedule as reconstructable projections with complete input keys; do not persist React/calendar UI state or add new localStorage writers.
