# 11 — Degraded web network startup and recovery

## Input

- Production web build served over HTTP.
- Entry-script connection reset.
- Repeated WebAssembly packet loss.
- A partially initialized `.NET` runtime returning `Runtime module already loaded` inside an Android Instagram-style WebView.
- Extreme bandwidth profile: 256 KiB/s download, 64 KiB/s upload, and 700 ms latency.
- Offline browser state.
- Existing Service Worker/cache at one version followed by a newer `version.web.json` and worker build.
- Phone-embedded build used as the platform-isolation control.

## Actions

- Open the application while the entry JavaScript request is aborted once.
- Confirm the standalone boot UI appears before React or WebAssembly is available.
- Allow the capped automatic page retry and wait for the application to recover.
- Abort the native WebAssembly request twice and delay the remaining WebAssembly requests.
- Confirm resource retry diagnostics are emitted and the application completes startup.
- Inject `Runtime module already loaded` after runtime initialization begins, verify a full document restart occurs, and verify the application then completes startup.
- Load under the extreme-bandwidth profile and verify the boot UI stays visible with concrete WebAssembly/data-file progress before startup completes.
- Switch the browser offline and verify an explicit offline state is rendered.
- Verify `version.web.json` is requested before WebAssembly.
- Activate the current versioned worker.
- Publish a newer version during the session.
- Verify the newer worker is fully staged into `pray-web-<version>` while the old worker remains active.
- Apply the staged update through the web update API, then verify the new worker becomes active and removes old `pray-web-*` caches only after successful application startup.
- Verify native download artifacts under `downloads/` are not included in `webber-manifest.json` and are not precached by the Service Worker.
- Inspect the phone build for the absence of remote-web boot and retry code.

## Tested

- Entry-script reset recovery: passed.
- Repeated WebAssembly packet-loss recovery: passed.
- Contaminated/partially loaded runtime recovery in the Instagram mobile profile: passed.
- Extreme-bandwidth startup: passed within the 180-second acceptance window.
- Offline status: passed.
- Version-first staged Service Worker installation and user-applied version transition: passed.
- Old cache cleanup after new-version startup commit: passed.
- Native download artifacts excluded from app-runtime caching: passed.
- Version-transition test artifact isolation from the IIS/public `dist`: passed.
- Resource filename, transferred bytes, total bytes, and progress percentage under throttling: passed.
- Web-only isolation from Android/Windows embedded assets: passed.
- Final command: `npm --prefix Pray.web run test:degraded-network`.
- Final exit code: `0`.

## Faild+why

- None in the final run.

## things to fix

- No remaining defect from this scenario.
- No remaining defect from version/worker replacement in the final run.

## remarks

- The loading and recovery UI is inline and does not depend on the React bundle or WebAssembly runtime.
- Browser resource failures are logged with the failed resource name and attempt number.
- The Service Worker file and registration URL both contain the web display/cache version, and worker script updates bypass the HTTP cache.
- Destructive version-transition simulation runs against `.network-test-dist`, which is removed afterward; it never mutates the IIS-served production directory.
- Navigation and the version/worker files are network-revalidated; successfully fetched hashed assets and WebAssembly resources are retained per version.
- `version.web.info` remains numeric for legacy native hosts; `version.web.json` carries the cache version, build, epoch, and worker URL.
- The recovery block is stripped from the phone-embedded build and therefore does not alter native Android or Windows startup.
