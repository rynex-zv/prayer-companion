# Web degraded-network remediation — 2026-08-13

## input

- Reported production failure in Iraq: the website could remain white or partially rendered under packet loss and extremely limited bandwidth.
- Implement the non-Service-Worker solutions without changing native embedded behavior.

## Actions

1. Added an inline, dependency-free boot shell with loading, slow-network, offline, failed, and retry states.
2. Added bounded entry-script reload recovery plus WebAssembly/resource retries with exponential backoff, jitter, online wake-up, and diagnostics.
3. Enabled IIS static compression and generated cache policies for immutable hashed assets and revalidated WebAssembly framework files.
4. Added deterministic Playwright tests for connection reset, repeated packet loss, extreme bandwidth, offline state, and native-build isolation.
5. Fixed an onboarding race found by the complete automation run: an automatic location refresh can no longer overwrite a manual location edit.
6. After an Iraq/Instagram field failure exposed `Runtime module already loaded`, replaced same-document runtime retries with a bounded clean document restart. The retry button now also performs a clean restart when online.
7. Disabled caching for `/` and `/index.html` so Instagram/WebView must revalidate the entry document and cannot remain pinned to the broken recovery code; hashed assets retain their long immutable cache.
8. Added a version-first web loader and versioned Service Worker. The loader checks `version.web.info` before starting WebAssembly, registers `pray-sw.js?v=<version>` with `updateViaCache: none`, activates the new worker, reloads once when the active version changes, and deletes old version caches only after the new application reports a successful boot.
9. Added real resource progress to the boot UI: current resource name, transferred/total MiB, percentage, and progress bar.
10. Removed the web version/worker loader entirely from the embedded phone HTML.
11. Isolated version-transition automation in `.network-test-dist`; tests no longer edit the IIS-served production `dist` even temporarily.

## Tested

- TypeScript checking: passed.
- Architecture checks: passed.
- Production web build: passed.
- Production phone build: passed.
- .NET tests: 232 passed, 0 failed.
- Full application automation: 10 passed, 0 failed (`web-2026-08-13T00-18-43-899Z`).
- Page contract: 470 assertions passed.
- Final degraded-network suite: passed, exit code `0`.
- Android Instagram-style WebView profile with injected `Runtime module already loaded`: passed; a second main-document navigation was asserted before startup completed.
- Version request before the first WebAssembly request: passed.
- Live worker transition from an old generated version to a new generated version: passed.
- Old cache preserved through update and removed after successful new-version boot: passed.
- Public version and worker version: both `401` in the final production artifact.
- Public `/`, `/version.web.info`, and `/pray-sw.js`: all return `Cache-Control: no-cache`.
- Public automation runner: absent.
- IIS/ARR public path `https://pray.rynex.nl/`:
  - JavaScript: 649,572 bytes uncompressed, 199,550 bytes with gzip.
  - Native WebAssembly: 2,899,148 bytes uncompressed, 1,132,924 bytes with gzip.
  - Hashed assets: `Cache-Control: public, immutable, max-age=31536000`.
  - WebAssembly framework: `Cache-Control: public, must-revalidate, max-age=3600`.
  - Entry document at both the direct IIS and public ARR URLs: `Cache-Control: no-cache`.

## Faild+why

- None remain in the final automated runs.
- Two intermediate failures were detected and repaired before the final pass:
  - The first phone post-processing boundary removed the React root; the marker boundary was corrected and the .NET suite then passed 232/232.
  - Automatic onboarding location resolution raced a manual edit; revision guarding now prevents stale automatic results from overwriting user input.
  - The original network recovery retried a partially loaded runtime inside the same document, producing `Runtime module already loaded` in Iraq. Runtime-start failures now force a clean document restart and are capped at four automatic restarts.

## things to fix

- Deploying or publishing production files was not performed by this task.
- After deployment, repeat the header checks against the public URL and run the degraded-network suite against the deployed artifact.
- A first-time visitor still needs the network to obtain the small entry document and loader; the worker can only protect resources successfully received by that browser.

## remarks

- `https://pray.local.rynex.nl/` is the direct IIS origin; `https://pray.rynex.nl/` is served through ARR and was used to verify the actual public gzip path.
- The direct IIS origin does not return gzip in the tested request, while ARR returns gzip publicly. Recovery does not rely on compression: the packet-loss and extreme-bandwidth tests still exercise the application-level failure path.
- No fallback prayer data or silent success was added. A failed boot remains visible and retryable.
- The extension-origin `contentscript.js` MaxListeners/ObjectMultiplex warnings in the supplied logs come from the injected browser extension, not from the application bundle. Application-owned startup and RPC entries completed successfully in those logs.
