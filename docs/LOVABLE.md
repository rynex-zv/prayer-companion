# Lovable Collaboration Guide

Use this repository as the project source:

```text
PrayAdFree
```

Lovable should work in:

```text
web.client
```

In the current `PrayAdFree` repo, `web.client` is the folder `Pray.web/`.

Lovable should treat these files as readable contracts:

```text
web.client/src/generated/core-contract.json
web.client/src/generated/core-contract.ts
docs/ARCHITECTURE.md
docs/CONTRACTS.md
```

## What Lovable Can Change

Lovable can change:

- React pages in `web.client/src/routes`
- React components in `web.client/src/components`
- CSS and layout in `web.client/src/styles.css`
- web-only UI state and presentation helpers

Lovable can add components, adjust layout, improve forms, and make UI flows easier to maintain.

## What Lovable Must Not Change

Lovable must not hand-edit:

- `web.client/src/generated/*`
- `core/*`
- `web.bridge/*`
- native host code under `app.host/*`
- built hosted web assets

If Lovable needs new app data, labels, defaults, options, validation, or RPC methods, ask for a Core change and regenerate the contract.

## How Lovable Gets Core Data

Lovable does not need DLL access. Core exports a readable contract into `web.client/src/generated`.

Use generated files for:

- method names
- labels
- default values
- supported options
- Core-owned constants
- app catalogs

At runtime the web app still calls the adapter:

```text
React UI -> mauiCall/coreClient -> MAUI bridge or WASM Core bridge
```

## Local Commands

From `web.client`:

```powershell
npm install
npm run typecheck
npm run build
npm run dev
```

From the repo root, regenerate contracts after Core changes:

```powershell
dotnet run --project tools/generate-web-contracts/GenerateWebContracts.csproj
```

## Message for Lovable

Use the `PrayAdFree` repo as the source of the project. Work only inside the `web.client` role unless explicitly asked otherwise; in this repo that means `Pray.web`. Read `docs/ARCHITECTURE.md`, `docs/CONTRACTS.md`, and `web.client/src/generated/core-contract.ts` before editing UI. Do not edit generated files, DLL-facing code, `web.bridge`, or native host code. If a UI needs new app data, ask for a Core contract update instead of hardcoding it.
