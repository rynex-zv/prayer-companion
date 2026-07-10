# Web Contracts

The web contract is the readable boundary between the `core` role and the `web.client` role.

Lovable cannot inspect .NET DLLs, so Core exports a small generated contract into the web app. The generated files tell the React app and Lovable which RPC methods exist, which labels/defaults/options are available, and which Core-owned constants are safe to use for UI work.

## Generated Files

The generator writes to:

```text
web.client/src/generated/core-contract.json
web.client/src/generated/core-contract.ts
```

In the current `PrayAdFree` repo, `web.client` maps to `Pray.web`.

These files are generated. Do not hand-edit them. Change Core first, then run:

```powershell
dotnet run --project tools/generate-web-contracts/GenerateWebContracts.csproj
```

## Contract Contents

The contract includes:

- method names exposed by `WebCoreRpcDispatcher`
- supported languages
- shell tabs
- localized labels
- theme and accent defaults
- qibla options
- adhan defaults
- notification defaults
- countries and known places
- about/support info
- permission/action labels

The contract is intentionally not a second implementation. It is exported data from Core so the web can preview and render without copying business rules.

## Change Workflow

1. Add or change app behavior in the `core` project. In this repo that is `PrayAdFree.Core`.
2. Update `WebCoreRpcDispatcher` if the web needs a new RPC method.
3. Run `tools/generate-web-contracts`.
4. Use the generated TypeScript/JSON from `web.client`.
5. Run targeted checks:

```powershell
dotnet build .\PrayAdFree.Core\PrayAdFree.Core.csproj
dotnet build .\PrayAdFree.WebBridge\PrayAdFree.WebBridge.csproj
cd .\Pray.web
npm run typecheck
npm run build
```

Do not use a full MAUI build for ordinary web/Core contract work unless the change touches MAUI native code.

## Rules

- Generated contract files may contain data exported from Core.
- React components may use generated types and Core-owned constants.
- React components must call runtime adapters for live data.
- WebBridge must not contain app catalogs or hardcoded user-facing app logic.
- If Lovable needs a value that is not generated, add it to Core and the generator.
