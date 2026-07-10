# PrayAdFree Architecture

PrayAdFree is one repository with three runtime surfaces. The current app still has product-specific folder names, but the template architecture uses generic roles so new projects can be created with clean names.

| Template role | Current path | Responsibility |
| --- | --- | --- |
| `core` | `PrayAdFree.Core` | Business logic, app defaults, labels, validation, prayer data, tasbih data, qibla data, and web RPC behavior |
| `web.client` | `Pray.web` | React user interface and runtime adapter calls |
| `app.host` | `PrayAdFree` | MAUI phone and Windows shell, native permissions, storage, notifications, alerts, calls, email, and hosted web assets |
| `web.bridge` | `PrayAdFree.WebBridge` | Browser WebAssembly connector only |
| `core.tests` | `PrayAdFree.Tests` | Core and contract tests |

## Runtime Flow

```text
React UI
  |
  v
mauiCall / coreClient
  |
  |-- MAUI bridge: phone and Windows native app
  |-- WASM bridge: standalone browser web app
  |-- generated Core contract: Lovable-readable schema and preview constants
  |-- HTTP backend: optional future adapter
```

The UI always talks to one adapter shape. The selected adapter depends on the runtime:

- MAUI app: `window.mauiWebber` handles RPC and native actions.
- Browser app: `PrayAdFree.WebBridge` loads Core through .NET WebAssembly.
- Lovable/design work: generated contract files provide stable method names, labels, defaults, and options.

## Source of Truth

Core remains the source of truth. If the web needs new app data, labels, defaults, validation, or method behavior, add it to the `core` project first. In this repo that is `PrayAdFree.Core`. Then run the contract generator so the `web.client` generated contract updates.

Do not duplicate Core rules in React. React can own presentation state, component layout, form interaction, and web-only capability fallbacks. It should not own prayer math, qibla math, tasbih defaults, app catalogs, settings defaults, or platform-neutral status messages.

## WebBridge Boundary

`web.bridge` should stay small. In this repo that is `PrayAdFree.WebBridge`:

- Parse the incoming method name and JSON payload.
- Call `WebCoreRpcDispatcher` from Core.
- Serialize the response envelope.
- Return clean errors from Core-owned error formatting.

Any app string or feature branch inside WebBridge is a bug. Move it to Core or to the generated web contract.

## Repository Shape

This repository is the canonical `PrayAdFree` repo. Keep all shared source, generated contracts, web UI, native shells, and template tooling here until there is a strong reason to split them.

Generic template ownership:

```text
core/                         Core logic and web RPC dispatcher
web.bridge/                   WASM connector only
web.client/                   Lovable-editable React app
web.client/src/generated/     Generated files from Core; do not hand-edit
app.host/                     MAUI phone and Windows app
core.tests/                   Core and contract tests
tools/generate-web-contracts/ Core-to-web contract generator
docs/                         Architecture and collaboration rules
```

Current repo mapping:

```text
core/       -> PrayAdFree.Core/
web.bridge/ -> PrayAdFree.WebBridge/
web.client/ -> Pray.web/
app.host/   -> PrayAdFree/
core.tests/ -> PrayAdFree.Tests/
```

When refactoring folders, move one role at a time and update build scripts immediately. Do not mix role renames with feature changes.

## Refactor Rule

Move folder-by-folder. Do not reorganize the whole repo in one pass. Each refactor should keep these checks green:

- Core builds.
- WebBridge builds.
- Web typecheck passes.
- Web production build passes.
- The main web routes render through the generated/Core contract.

## Visual Studio Template Direction

The future template should be a project-family template, not a copy of PrayAdFree-specific folder names. When creating a new app in Visual Studio, the template should let the developer choose the project shape, then create folders using generic role names:

- `core`
- `web.client`
- `web.bridge`
- `app.host`
- `core.tests`

The app name should be applied to namespaces, assembly names, package names, display names, and bundle identifiers. It should not replace the role names. For example, a new app called `MyPrayerApp` should still have a `web.client` folder rather than `MyPrayerApp.web` unless the developer explicitly chooses product-specific folders.

Suggested template choices:

- `Full App`: `core`, `web.client`, `web.bridge`, `app.host`, `core.tests`
- `Web Only`: `core`, `web.client`, `web.bridge`, `core.tests`
- `Native Host Only`: `core`, `app.host`, `core.tests`

This keeps the template maintainable and makes Lovable instructions stable across projects.
