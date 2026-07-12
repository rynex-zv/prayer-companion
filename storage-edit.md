# Centralized Application Data Architecture Migration

## Purpose

This is the implementation brief for migrating Prayer Companion from duplicated, multi-owner state to one consistent application-wide architecture. Use it as the primary context in a new Codex chat and execute the migration one phase at a time.

This is not a plan for fixing Adhan, Tasbih, calendar, language, GPS, alarms, or individual pages separately. They are examples of a repository-wide ownership problem. The architecture must apply to every current and future domain.

## Required outcomes

1. One authoritative owner for every durable domain datum.
2. One typed client API and store model for React.
3. MAUI as the native offline backend/API.
4. A browser backend with identical public contracts.
5. WASM/Core for shared rules and calculations, not unrelated mutable state.
6. No competition between backend persistence and browser storage.
7. No ViewModels acting as backend/domain services.
8. Normally one command for one user intent.
9. Successful commands return authoritative resulting state.
10. Useful grouped snapshots instead of many small reads.
11. As few startup calls as possible, normally one bootstrap query.
12. Structured events/invalidation for background changes.
13. Identical native/browser semantics despite platform-specific internals.
14. Explicit separation of durable, cached, confirmed, optimistic, draft, and UI-only state.
15. A model that supports all future domains.

---

## Repository boundaries

| Role | Path | Current responsibility |
| --- | --- | --- |
| Shared Core | `PrayAdFree.Core/` | Models, calculations, defaults, catalogs, generated-contract inputs, browser RPC behavior |
| React client | `Pray.web/` | Routes, components, hooks, client state, runtime calls |
| MAUI host/backend | `PrayAdFree/` | Native host, persistence, services, platform behavior, ViewModels, RPC |
| Browser bridge | `PrayAdFree.WebBridge/` | Loads Core/WASM and dispatches browser calls |
| Tests | `PrayAdFree.Tests/` | Core, contract, cache, scheduling, integration tests |

## Real current flows

### Native

```text
React
  -> useSnapshot/useStoredSnapshot/appStore
  -> mauiCall
  -> MauiWebber
  -> WebAppRpcHandler
  -> ViewModel and/or service
  -> SettingsService / PrayerDataService / platform services
  -> JSON persistence, caches, notifications, alarms, GPS, audio
  -> RPC response
  -> page state and sometimes React localStorage
```

### Browser

```text
React
  -> page state and appStore
  -> mauiCall fallback
  -> webPlatformAdapter and/or WASM bridge
  -> mutable WebCoreRpcDispatcher.WebState
  -> full-state export to localStorage
  -> response copied into React and another localStorage document
```

### Repository evidence

- `Pray.web/src/state/appStore.ts` persists React state under `prayer-companion:app-state:v1`.
- `Pray.web/src/native/wasmCoreClient.ts` persists a second state document under `pray.web.core.state` and exports all WASM state after mutations.
- `Pray.web/src/native/webPlatformAdapter.ts` can perform snapshot -> set -> snapshot -> set -> snapshot for one browser GPS action.
- `Pray.web/src/hooks/useSnapshot.ts` fetches isolated state on component mount.
- `Pray.web/src/hooks/useStoredSnapshot.ts` displays stored data but still requests the backend on mount.
- `PrayAdFree/Services/WebAppRpcHandler.cs` depends directly on Calendar, Qibla, and Tasbih ViewModels.
- `PrayAdFree/Services/TodayWebRpcHandler.cs` builds backend responses from `HomeViewModel` and owns another memory/disk snapshot.
- `PrayAdFree/Services/PrayerDataService.cs` writes settings and broadcasts complete `AppSettings` instances.
- `PrayAdFree/ViewModels/SettingsViewModel.cs` has many property-change paths that schedule complete settings saves.
- `PrayAdFree.Core/Services/WebCoreRpcDispatcher.cs` owns mutable browser `WebState`.
- Startup work is spread across React shell mounting, route hooks, Today preload, MAUI lifecycle callbacks, notification bootstrap, and widget refresh.

These are architectural symptoms. Do not patch only the cited files or examples.

## Current duplicated owners

| State | Current owners/copies |
| --- | --- |
| Settings | `app_settings.json`, loaded `AppSettings`, Settings ViewModel, other ViewModels, React store, WASM state |
| Language | Settings, LocalizationManager, .NET culture, generated contract, shell snapshot, React language object, WASM |
| Theme | Settings, ThemeManager, MAUI resources, React store, DOM state, localStorage |
| Location | Settings, ViewModels, React page/stored snapshots, WASM, GPS/geocoder intermediates |
| Prayer data | API response, prayer cache, PrayerDataService, ViewModels, Today memory/disk snapshots, React |
| Tasbih | Settings, Tasbih ViewModel, React snapshots/cache, WASM |
| Qibla | Settings, ViewModel, RPC-handler fields, React, WASM, sensor observations |
| Permissions | Operating system, permission services, snapshots, React state |
| Alarm/schedule | Platform queues, playback, scheduler, settings, presentation snapshots, React |
| Sync metadata | React `fieldSync`, currently persisted with application copies |

For every migrated value, name one authority and classify all other forms as projection, cache, platform observation, optimistic overlay, draft, or UI-only state.

---

## Target architecture

```text
React components
  -> selectors/actions
  -> centralized Client Store
  -> typed AppClient
  -> selected AppBackend
       -> MauiAppBackend (native)
       -> BrowserAppBackend (browser)
  -> application command/query handlers
  -> domain services and pure Core/WASM functions
  -> authoritative repositories
  -> derived caches and platform ports
  -> revisioned results/events
  -> Client Store
```

Centralized means centralized authority and coordination, not one giant mutable object. Domain modules remain separated while following one ownership and contract model.

## Responsibilities

### React

Own rendering, navigation presentation, form drafts, focus, open/closed state, animations, and temporary validation. React must not own durable settings, calculations, scheduling, permissions, background workflows, or authoritative snapshots. Components must not import `mauiCall` directly.

### Client Store

Owns the latest backend-confirmed projections plus separate optimistic, request, and UI state:

```ts
type ClientState = {
  confirmed: Record<DomainName, unknown>;
  revisions: {
    global: number;
    domains: Record<DomainName, number>;
    eventSequence: number;
  };
  optimistic: Record<string, OptimisticOperation>;
  requests: Record<string, RequestState>;
  ui: UiOnlyState;
};
```

The confirmed partition is a replaceable read model, not another database. Request, sync, optimistic, and UI metadata must not be persisted as domain state.

### AppClient

React uses one API:

```ts
interface AppClient {
  bootstrap(request: BootstrapRequest): Promise<BootstrapResult>;
  query<T>(query: AppQuery<T>): Promise<QueryResult<T>>;
  command<T>(command: AppCommand<T>): Promise<CommandResult<T>>;
  subscribe(listener: (event: AppEvent) => void): () => void;
}
```

It owns serialization, timeouts, cancellation, request IDs, logging, contract negotiation, and subscriptions. It does not implement domain workflows.

### Transport

Native and browser transports carry the same envelopes. Transport must not merge settings, perform GPS workflows, persist state, or decide which methods are mutations.

### MAUI backend

MAUI is the native offline backend/API. It owns repository lifetime, commands, queries, transactions, platform ports, cross-domain effects, background work, scheduling reconciliation, bootstrap projections, and post-commit events. RPC handlers map transport messages to application operations; they never depend on ViewModels.

### Browser backend

The browser implements the same commands, queries, results, errors, revisions, and events. It should use IndexedDB for authoritative structured data and browser APIs as platform ports. A workflow such as location refresh is one backend command even if it internally requests GPS, geocodes, persists, recalculates, and emits events.

### Core/WASM

Core/WASM owns deterministic validation, normalization, prayer/Qibla/Tasbih/calendar calculations, schedule planning, migrations, cache keys, and projection helpers. Inputs and outputs are explicit. It must not act as an extra mutable database.

### Persistence

Persistence is accessed only through backend repositories. Native may initially retain JSON; browser should move to IndexedDB. Repositories are schema-versioned, transactional where possible, complete persistence before command success, and contain no UI/request/sync metadata.

Candidate logical repositories:

```text
SettingsRepository
TasbihRepository
PrayerDataRepository
ScheduleRepository
AppMetadataRepository
```

### Domain services

Perform typed intent-named operations such as `ChangeTheme`, `UpdateLocation`, `IncrementTasbih`, `SaveNotificationPreferences`, and `CompleteOnboarding`. They do not depend on React or ViewModels.

### Caches

Caches are reconstructable and safely deletable. Every cache defines its authoritative input key, calculation/schema version, freshness, invalidation, and recovery behavior.

### Platform services

GPS, permissions, notifications, alarms, sensors, audio, files, lifecycle, and network are backend ports. They return observations or execution results and do not patch repositories directly. Background callbacks enter the same application coordinator used by foreground commands.

### ViewModels

ViewModels are presentation adapters for XAML only. They select projections, expose bindable state, send application commands, and hold UI-only state. They are never repositories, domain services, or RPC dependencies.

---

## Request, result, and event contracts

### Command

```json
{
  "contractVersion": 2,
  "requestId": "uuid",
  "commandId": "uuid",
  "name": "location.refresh",
  "expectedRevision": 41,
  "payload": {}
}
```

`commandId` supports idempotent retries.

### Command result

```json
{
  "ok": true,
  "requestId": "uuid",
  "revision": 42,
  "changedDomains": ["settings", "location", "today", "schedule"],
  "data": {
    "location": {},
    "today": {},
    "scheduleStatus": {}
  },
  "events": [
    {
      "sequence": 918,
      "eventId": "uuid",
      "type": "location.changed",
      "domain": "location",
      "revision": 42,
      "causeRequestId": "uuid"
    }
  ]
}
```

The client applies `data` immediately and does not issue a follow-up snapshot for included state.

### Query

```json
{
  "contractVersion": 2,
  "requestId": "uuid",
  "name": "settings.snapshot",
  "ifRevision": 42,
  "payload": {
    "sections": ["location", "notifications"]
  }
}
```

A query may return a full projection, delta, or `notModified`.

### Events

Every event contains sequence, event ID, timestamp, domain, type, revision, cause request ID, and either a payload or invalidation key.

Suggested vocabulary:

- `domain.changed`
- `domain.invalidated`
- `platform.permissionChanged`
- `alarm.started`, `alarm.updated`, `alarm.stopped`
- `schedule.reconciled`
- `time.boundaryCrossed`
- `backend.resumed`
- `backend.reset`

Events are emitted only after commit. Clients ignore old/duplicate revisions and sequences. High-frequency clock and sensor observations use throttled ephemeral streams, not persistent domain events.

---

## Bootstrap

Add one `app.bootstrap` query returning:

- Contract and persistence schema versions.
- Global and per-domain revisions.
- Shell preferences and startup route/intent.
- Active-language labels or localization version.
- Today/home projection.
- Active alarm summary.
- Onboarding state.
- Essential permission summary.
- Platform capability summary.
- Optional small likely-next projections.

Do not include every country, sound, calendar month, or settings catalog. Static catalogs should be bundled/versioned separately or loaded lazily.

Desired startup:

```text
1. Render the shell from bundled defaults or a disposable cached projection.
2. Establish the selected backend and event channel.
3. Send one app.bootstrap query.
4. Atomically install confirmed projections in the store.
5. Routes select existing store data.
6. Query only missing or stale non-bootstrap projections.
```

Backend work triggered by startup, resume, preload, settings, widget refresh, and scheduling must be coalesced by operation key and authoritative input revision.

---

## Migration phases

Work only on the requested phase unless the user explicitly expands scope. Preserve compatibility needed by later phases.

### Phase 1: Contracts and observability

**Status: DONE (2026-07-12)**

- [x] Versioned command, query, result, typed error, revision, and event envelopes are defined in Shared Core.
- [x] The generated web contract publishes contract/schema versions and the complete RPC classification registry to TypeScript.
- [x] Legacy calls carry UUID request IDs and command IDs without changing existing method responses.
- [x] Client/native correlation logs include operation kind, duration, response size, persistence-write count, and cache outcomes.
- [x] Client instrumentation reports duplicate in-flight queries and command-then-refresh sequences.
- [x] Existing RPCs remain available; compatibility adapters and obsolete calls are explicitly identified in the generated registry.
- [x] Contract serialization, version, typed-error, and classification tests are included in the .NET suite; generated fixtures are type-checked by the web build.

Phase 1 baseline: startup and interaction call counts are now emitted as structured `[pray.bridge]` records, while native completions are emitted as correlated `rpc.completed`/`rpc.failed` records. This preserves the current behavior and provides the measurement source for Phase 2/3 call-budget comparisons.

Objective: establish the shared protocol and measurements without changing feature behavior.

Work:

- Define versioned command, query, result, error, revision, and event envelopes.
- Establish generation/shared fixtures for matching C# and TypeScript contracts.
- Add request/command IDs, durations, response sizes, persistence-write counts, cache outcomes, and correlation logging.
- Classify every existing RPC as command, query, platform operation, compatibility adapter, or obsolete.
- Detect command-then-refresh sequences and duplicate in-flight queries.
- Baseline startup calls and representative interaction calls/writes.
- Keep all old RPCs working.

Exit gates:

- Native and browser pass identical envelope serialization fixtures.
- Existing behavior remains operational.
- One action can be traced across UI, transport, backend, persistence, and scheduling.
- Contract/version/error tests exist.

### Phase 2: Unified React client and store

**Status: DONE (2026-07-12)**

Phase 1.5 safety boundary completed before Phase 2:

- [x] A runtime selects MAUI or browser once per session; failed/timed-out native calls never replay against WASM.
- [x] Transport failures have typed codes and retryability, and diagnostic payloads are redacted.
- [x] Request/command IDs created by `AppClient` flow through the compatibility transport and native correlation logs.

Phase 2 implementation:

- [x] `AppClient` provides bootstrap compatibility, query, command, subscription, cancellation, and normalized result/error contracts over the legacy transport.
- [x] The centralized client store separates confirmed projections, revisions, optimistic operations, request state, and UI-only state; it is memory-only.
- [x] Queries deduplicate by method, normalized payload, and revision while allowing individual subscribers to cancel their wait.
- [x] Successful command data is installed directly into confirmed projections.
- [x] Tasbih and Tasbih Settings use the same `tasbih.snapshot` projection; increment, reset, preset selection, and settings edits no longer issue follow-up reads.
- [x] An architecture check prevents new UI/domain code from importing `mauiWebberClient`; existing unmigrated consumers are an explicit shrinking compatibility allowlist.
- [x] Legacy hooks, the old app store, and old RPC methods remain intentionally available for unmigrated domains.

Objective: give React one access path and one confirmed read model while legacy RPC remains underneath.

Work:

- Implement `AppClient` over existing transports.
- Create confirmed, revision, optimistic, request, and UI store partitions.
- Add typed selectors/actions, in-flight query deduplication, and cancellation.
- Apply command results directly.
- Wrap legacy RPC behind compatibility methods.
- Incrementally migrate components away from direct `mauiCall`.
- Retain legacy hooks until consumers migrate.

Exit gates:

- New UI code cannot import `mauiCall`; enforce this.
- Migrated commands do not immediately refresh returned state.
- Confirmed projections survive route remounts in the shared store.
- Optimistic/request state is separate from confirmed data.

### Phase 3: Bootstrap and event channel

**Status: DONE (2026-07-12)**

- [x] Native and browser implement the same grouped `app.bootstrap` query with contract/schema versions, revisions, startup intent, shell, Today, alarm, onboarding, permissions, and capabilities.
- [x] Shell and initial Today rendering share one deduplicated bootstrap promise; the initial route no longer issues `today.getSnapshot`.
- [x] Bootstrap projections and revision metadata install atomically in the centralized confirmed store.
- [x] Native structured events are pushed through MauiWebber; browser command events flow in-process and through `BroadcastChannel` for tab synchronization.
- [x] Events carry sequence, ID, timestamp, domain, type, revision, cause request ID, and invalidation key; duplicate, old, and out-of-order events are ignored.
- [x] Revision-aware queries send `ifRevision` only for installed projections and support `notModified` results.
- [x] Native resume publishes `backend.resumed` and a targeted Today invalidation; Today refreshes from invalidation instead of a 30-second backend polling loop.
- [x] Static startup call-budget enforcement prevents restoring separate shell or initial Today reads.
- [x] Legacy RPCs and route hooks remain available for domains not yet migrated.

Objective: reduce startup calls and push/invalidate background changes.

Work:

- Implement identical `app.bootstrap` behavior in both runtimes.
- Replace shell plus initial-route reads with bootstrap projections.
- Add structured events over MauiWebber.
- Add browser/in-process events and optional BroadcastChannel synchronization.
- Apply event patches or invalidate exact projection keys.
- Add revision-aware queries and `notModified`.
- Coalesce preload/warmup tasks.

Exit gates:

- Initial shell/home normally requires one application query.
- Background changes reach React without polling loops.
- Duplicate/out-of-order events are harmless.
- Route remounts do not reread valid bootstrap projections.

### Phase 4: Backend application services and repositories

**Status: DONE — transaction, platform build, and native runtime verified (2026-07-12)**

- [x] All native commands cross a serialized application coordinator boundary with command-ID replay protection and expected-revision validation.
- [x] The coordinator owns transaction commit/rollback, revision advancement, and post-commit event publication ordering.
- [x] Settings persistence is exposed to backend consumers through the typed `ISettingsRepository` contract.
- [x] Legacy RPC names remain mapped at the transport boundary while successful commands continue returning their authoritative projection payloads.
- [x] Tests cover idempotent replay, stale revision rejection, commit-before-event ordering, and failure atomicity.
- [x] `WebAppRpcHandler` is transport-only; `NativeAppBackend` owns application dispatch and workflow orchestration.
- [x] `SettingsService` is the native application transaction factory: writes are staged, committed once, and discarded on rollback.
- [x] Scheduling and settings/widget notifications run only after durable commit.
- [x] Equivalent in-flight queries are coalesced by normalized operation input and authoritative revision; completed command replay is bounded.
- [x] Windows MAUI runtime selected the native backend, completed bootstrap, rendered Today, and logged no startup exception.

Objective: put authoritative use cases behind one backend application layer.

Work:

- Introduce an application command/query coordinator.
- Add typed repositories and transaction boundaries.
- Move workflows out of `WebAppRpcHandler`.
- Adapt old RPC methods to new handlers.
- Return authoritative relevant projections from every successful command.
- Publish events after persistence.
- Add idempotency and revision checks.
- Coalesce recalculation, scheduling, and widget invalidation.

Exit gates:

- Migrated use cases have one command handler and persistence boundary.
- RPC handlers contain transport mapping, not domain workflows.
- Successful commands render without follow-up reads.
- Tests verify commit-before-event and failure atomicity.

### Phase 5: Remove ViewModels from backend paths

**Status: DONE — shared application services and native runtime verified (2026-07-12)**

- [x] Native RPC handlers depend on application projection ports rather than concrete ViewModel types or UI namespaces.
- [x] Today, Calendar, Qibla, and Tasbih expose the same query/command surfaces to React and XAML through typed ports.
- [x] Tasbih transport dispatch invokes intent methods instead of MAUI `Command` presentation objects.
- [x] An architecture test prevents RPC-to-ViewModel dependencies from returning.
- [x] Backend projection ports resolve to singleton `Today`, `Calendar`, `Qibla`, and `Tasbih` application services, never ViewModels.
- [x] XAML ViewModels are command/device-feedback adapters over those same application use-case classes and contain no persistence, scheduling, location, or calculation workflows.
- [x] Application services contain no `MainThread`, MAUI `Command`, or device API dependency; transient XAML adapters do not attach app-lifetime subscriptions.
- [x] Today preload/bootstrap refresh and snapshot persistence are serialized, preventing concurrent cache-write failures.
- [x] Architecture tests enforce DI mappings, UI-independent services, and presentation-only adapters; Windows runtime completed native bootstrap/render with an empty exception log.

Objective: make ViewModels presentation-only.

Work:

- Replace `TodayWebRpcHandler -> HomeViewModel` with application queries/projections.
- Remove Calendar, Qibla, and Tasbih ViewModels from RPC dependencies.
- Make XAML ViewModels consume the same backend use cases as React.
- Remove ViewModel persistence and scheduling responsibilities.
- Correct event subscription lifetimes.

Exit gates:

- No RPC handler accepts a ViewModel.
- Domain tests run without UI/main-thread objects.
- React and XAML use the same commands and queries.
- ViewModels contain presentation adaptation only.

### Phase 6: Unified browser backend

**Status: DONE — deterministic browser backend and runtime persistence verified (2026-07-12)**

- [x] `BrowserAppBackend` is the browser runtime's single dispatch, serialization, and persistence boundary.
- [x] Browser authoritative state is hydrated from and committed to an IndexedDB repository transaction.
- [x] The old WASM localStorage record imports once into IndexedDB and is retired.
- [x] WASM no longer reads or writes browser storage; it is invoked as the calculation/contract engine.
- [x] GPS refresh performs one read and one authoritative write, returning the resulting location without a follow-up snapshot.
- [x] Architecture checks prevent WASM persistence and snapshot-set-snapshot browser workflows from returning.
- [x] Browser Core execution is explicit and deterministic: persisted state plus an operation returns data, events, revisions, and replacement state.
- [x] The WASM bridge has no process-global dispatcher; IndexedDB is the only long-lived browser authority and serializes every top-level operation.
- [x] Global/domain revisions survive reload in the repository execution envelope, including revision-aware `notModified` queries.
- [x] Browser platform workflows execute inside one repository transaction and commit once; reverse geocoding no longer performs a follow-up snapshot.
- [x] Legacy raw `WebState` and both retired localStorage documents migrate into schema version 3 without `app.importState`/`app.exportState` persistence calls.
- [x] Fresh-origin and reload tests verified isolated defaults, durable language mutation, no leaked WASM state, and zero browser console errors.
- [x] Release WASM publish, phone bundle, and Windows MAUI build/runtime passed; native bootstrap rendered with an empty exception log.

Objective: replace React + adapter + mutable WASM state with a real browser backend.

Work:

- Implement `BrowserAppBackend` with the MAUI contract.
- Move authoritative browser records to IndexedDB.
- Convert geolocation, notifications, permissions, and geocoding into platform ports.
- Execute each workflow as one backend command.
- Replace mutable `WebCoreRpcDispatcher.WebState` with repositories and pure Core/WASM functions.
- Import old localStorage records during migration.

Exit gates:

- One contract suite passes against native and browser.
- Browser location refresh is one client command.
- WASM behavior has explicit deterministic inputs/outputs.
- Browser data survives reload through one authoritative repository model.

### Phase 7: Persistence and cache consolidation

**Status: DONE — ownership, migrations, cache eviction, and reconciliation verified (2026-07-12)**

- [x] React confirmed, optimistic, request, and sync projections are memory-only and never written as domain data.
- [x] Both legacy localStorage documents import once into the browser repository and are then removed.
- [x] Browser repository records carry an explicit persistence schema version and recover safely from absent/old records.
- [x] Prayer-time derived cache keys include schema/calculation version plus every authoritative calculation input.
- [x] Architecture and migration tests enforce storage ownership and retirement of both legacy keys.
- [x] Browser schema version 4 upgrades old raw/schema records, rejects newer unknown schemas, and rewrites recoverable corrupt state deterministically.
- [x] Calendar view/mode and all React request/confirmed/optimistic state are memory-only.
- [x] Cache clearing removes only reconstructable service-worker, Cache Storage, session, and platform disk-cache data; it preserves IndexedDB, native repositories, cookies, and user-authored state.
- [x] Prayer cache v3 keys exact coordinates, timezone, method, madhhab, high-latitude rule, offsets, angles, and calculation version.
- [x] Today snapshot schema 2 keys date, prayer inputs, language, and clock format; geo cache schema 1 validates expiry; both discard incompatible records.
- [x] Notification schedule reconciliation v2 keys settings, timezone, permission requests, alarm capability/permission state, and custom sound identity.
- [x] Browser reload/cache-eviction, release builds, 144 tests, and the rebuilt Windows runtime were verified without console or exception-log errors.
- [x] Onboarding commits its visible default language before completion, preventing older repository language state from producing mixed-language projections.

Durable ownership and derived-data inventory:

| Datum | Single authority | Deletion/rebuild rule |
|---|---|---|
| Native settings, location, reminders, tasbih, onboarding | `ISettingsRepository` / `SettingsService` JSON transaction | User-authored; cache clearing must preserve it. |
| Browser settings, location, reminders, tasbih, onboarding, persisted revisions | IndexedDB `prayer-companion/repositories/core-state`, schema 4 | User-authored; legacy localStorage is import-only and then removed. |
| Prayer month calculations | `PrayerTimesCache/v3` | Derived; safely regenerated from its complete input key. |
| Today snapshot | Today cache envelope schema 2 | Derived; accepted only when its complete input key matches. |
| Geocoding response | Geo cache document schema 1 | Derived; safely removed when expired, incompatible, or corrupt. |
| Platform notification schedule | OS projection plus reconciliation signature v2 | Derived from authoritative settings and current platform capability; safely reconciled. |
| Calendar view/mode and React projections/requests | Process memory | UI-only; intentionally lost on reload. |

Objective: remove duplicate durable state and formalize caches.

Work:

- Stop persisting React confirmed, sync, request, and optimistic state as domain data.
- Import and retire `prayer-companion:app-state:v1`.
- Import and retire `pray.web.core.state`.
- Add schema-versioned migrations and recovery tests.
- Convert Today, prayer, and geo snapshots into revision-keyed derived caches.
- Track scheduling reconciliation against authoritative input revisions.
- Make caches safely deletable.

Exit gates:

- Each durable datum has one documented repository.
- Cache deletion loses no user-authored data.
- Old storage keys migrate once and are no longer written.
- Cache invalidation covers every authoritative input.

### Phase 8: Legacy removal and enforcement

**Status: PARTIAL — runtime audit reopened (2026-07-12)**

- [x] UI routes, components, and snapshot hooks no longer import the transport; all domain access crosses the client boundary.
- [x] Legacy snapshot hooks are memory-only adapters over `AppClient` and the centralized confirmed store, not data owners.
- [x] Whole-application `AppSettings` instances are no longer broadcast to subscribers.
- [x] Browser persistence is isolated from WASM and React, and old storage keys are migration-only.
- [x] Architecture tests enforce startup budget, transport dependency, ViewModel boundary, persistence ownership, event ordering, revision checks, and command idempotency.
- [x] Compatibility RPC names remain only behind the client/backend compatibility facades for platform-specific features; new code cannot access transport directly.

Audit note: Phases 4, 5, and 6 are complete. `WebCoreRpcDispatcher` remains an ephemeral compatibility implementation inside deterministic `WebCoreExecutionEngine` calls, but it has no process-global or durable authority. Later-phase debt remains: public `app.importState`/`app.exportState` compatibility methods, `legacyClient`, snapshot-hook compatibility names, `settings.invoke`, and generic `settings.setField`. Do not use Phase 4–6 completion to describe those later phases as complete.

Remove when unused:

- Direct route/component `mauiCall`.
- `useSnapshot` and `useStoredSnapshot` as data owners.
- React localStorage as a domain database.
- Full WASM export after mutations and mutable WASM state.
- `settings.invoke` action multiplexing.
- Generic `settings.setField` for meaningful domain operations.
- RPC-to-ViewModel dependencies.
- Command-then-refresh code.
- Whole-app `SettingsChanged` broadcasts.
- Property-change-driven complete settings saves.
- Handler-owned domain state.
- Duplicate lifecycle work.
- Static catalogs embedded repeatedly in dynamic snapshots.

Add enforcement:

- Forbidden-dependency/import architecture tests.
- Native/browser contract parity tests.
- One-command interaction tests.
- Startup call-budget tests.
- Persistence ownership and event ordering/idempotency tests.

---

## Mandatory rules

1. Every durable datum has one named authoritative repository.
2. Every runtime has exactly one active backend.
3. React uses only AppClient and the client store for domain data.
4. Components never call transports directly.
5. Durable changes use typed intent-named commands.
6. One user intent normally sends one command.
7. Successful commands return authoritative relevant state.
8. The client does not reread state returned by a command.
9. Queries are read-only, grouped, deduplicated, and revision-aware.
10. Events are post-commit and revisioned/sequenced.
11. Background work enters through the same application layer.
12. Platform services do not own domain records.
13. Core/WASM functions are deterministic from explicit inputs.
14. ViewModels are presentation adapters only.
15. Durable, cached, confirmed, optimistic, draft, observation, and UI state are distinct.
16. Caches are safely deletable.
17. Static catalogs and live state have separate contracts.
18. Native/browser semantics and typed errors match.
19. Unsupported capabilities return explicit typed results.
20. Full-app snapshots are limited to bootstrap, recovery, backup/export, and diagnostics.
21. Commands are idempotent or command-ID protected.
22. Cross-domain effects are coordinated once in the backend.
23. Lifecycle work is coalesced by key and input revision.
24. Domain modules never depend on React, pages, or ViewModels.
25. Architecture tests prevent regression.

## State classification

| Class | Meaning | Storage |
| --- | --- | --- |
| Authoritative domain state | User/system state that cannot be reconstructed | Backend repository only |
| Derived cache | Reconstructable result of authoritative inputs | Backend cache with key/version |
| Confirmed projection | Latest backend-confirmed rendering model | Client memory; optional disposable cache |
| Optimistic overlay | Temporary pending-command representation | Client memory only |
| Draft | Incomplete uncommitted input | Component/UI store only |
| Platform observation | Current external fact such as permission/sensor | Backend stream; persist only if domain requires |
| UI-only state | Pure presentation state | Component/UI store only |

If one model fits multiple rows, split its responsibilities before migrating it.

---

## Working method for every new phase chat

1. Read this file completely.
2. Inspect the current worktree and relevant code; do not assume earlier phases are unchanged.
3. State the selected phase and its exit gates.
4. Trace affected calls end-to-end before editing.
5. Preserve unrelated user changes.
6. Implement only the selected phase or one clearly bounded slice.
7. Add/update contract and architecture tests.
8. Run relevant .NET tests, TypeScript checks, and web builds.
9. Report call/write behavior before and after when instrumentation exists.
10. Update this document only when an approved architectural decision changes.

Do not begin with page-specific patches, create another permanent state copy, move mutable state into WASM and call it centralization, persist React state to hide latency, or remove compatibility before all callers migrate.

## New-chat prompt

```text
Read storage-edit.md completely and implement Phase N.

Treat storage-edit.md as the architectural source for this task. Inspect the current repository first and verify what previous phases have implemented. Then state the Phase N exit gates and create a focused implementation plan.

Do not solve individual pages independently or advance into later phases unless a safe interface boundary requires it. Preserve compatibility needed by unmigrated code. Implement the phase, add architecture/contract tests, run relevant .NET and React checks, and update storage-edit.md only if an approved implementation decision changes the architecture.

At completion report:
- files and boundaries changed;
- old and new request/state flow;
- tests and builds run;
- exit gates satisfied or still open;
- compatibility code intentionally retained;
- recommended next bounded step.
```

## Definition of completion

The migration is complete only when React has one typed client/store, MAUI and browser implement the same backend contracts, every durable datum has one repository, Core/WASM has no extra mutable database role, commands eliminate normal follow-up snapshots, startup normally uses bootstrap plus genuinely lazy reads, background changes use events/invalidation, ViewModels are absent from backend dependencies, old browser state copies are retired, caches are reconstructable, and tests enforce these boundaries.
