# Prayer Companion agent entry point

Read these files in order before changing application state, transport, storage, or startup behavior:

1. `../storage-edit.md` — architecture requirements and migration history.
2. `mds/current-architecture.md` — current implementation boundaries and mandatory runtime checks.
3. `mds/linkinHistory.md` — page-by-page verification history. Treat old evidence as historical, not proof for the current build.

The files `mds/old.md`, `mds/new.md`, and `mds/newer.md` are historical UI inventories. They are not current implementation instructions.

Never call a change production-ready from compilation alone. For storage/startup changes, test a cold browser origin, a reload with IndexedDB state, `https://pray.rynex.nl/`, and a freshly rebuilt Windows MAUI executable. Record the exact bundle hash and console/runtime errors.
