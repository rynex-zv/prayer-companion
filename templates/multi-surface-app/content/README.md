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

## Rule

The app name belongs in namespaces, assembly names, package IDs, display names, and bundle IDs. It should not replace role folder names.

## Lovable

Lovable should work only in `web.client` unless explicitly asked otherwise. It should read generated contracts from `web.client/src/generated` and must not edit Core, WebBridge, native host code, or generated files.
