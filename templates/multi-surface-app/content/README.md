# TemplateApp

This project was created from the `Multi-surface App` template.

The folder names are generic role names. Keep them stable across projects:

| Role | Purpose |
| --- | --- |
| `core` | Business logic, models, validation, labels, defaults, and platform-neutral RPC behavior |
| `web.client` | React UI editable by Lovable |
| `web.bridge` | WebAssembly connector from browser JavaScript to `core` |
| `app.host` | Native MAUI phone/Windows host |
| `core.tests` | Core and contract tests |

When generated with `--widgetSupport cross-platform`, `core/Widgets` is the single source for widget profiles, projections and render trees, `web.client/src/widgets` contains the Core-backed preview surface, and native renderers implement `IWidgetRenderer`. With the default `none`, all of those paths are excluded.

## Rule

The app name belongs in namespaces, assembly names, package IDs, display names, and bundle IDs. It should not replace role folder names.

## Lovable

Lovable should work only in `web.client` unless explicitly asked otherwise. It should read generated contracts from `web.client/src/generated` and must not edit Core, WebBridge, native host code, or generated files.

## Remote web update contract

Every project created from this template should use the same safe web-update shape:

- Fetch `version.web.json` first with cache disabled before loading app code.
- Keep a numeric `version.web.info` only for legacy host compatibility.
- Use a display/cache version like `0.0.<cacheEpoch>.<build>`.
- Do not precache `version.web.json`, `version.web.info`, the service worker, or the host manifest.
- Download a new build into a separate cache in the background.
- Show an Update button only after the new cache is complete.
- Apply the update only after the user accepts it, then reload the same route.
- Delete old app caches only after the new app boots and commits the version.

## Native download contract

Native installer discovery must be independent from the remote web app version:

- Serve the platform download manifest, for example `downloads/manifest.json`, with cache disabled.
- Fetch the download manifest network-first with a cache-busting query.
- Do not hide Android/Windows download links because the installer's embedded web build is older than or equal to the currently loaded web shell.
- Keep native artifact names and labels explicit about both versions, for example `Android-<nativeVersion>-web<embeddedWebBuild>`.
- Do not precache APK, EXE, ZIP, or native download manifests in the service worker or native embedded-web manifest.
