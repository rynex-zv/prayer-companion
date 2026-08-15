# App Template Direction

The template should create a project family with generic role folders, not PrayAdFree-specific folder names.

## Visual Studio Project Choices

When the template is packaged for Visual Studio, the developer should choose one of these project shapes:

| Choice | Folders |
| --- | --- |
| `Full App` | `core`, `web.client`, `web.bridge`, `app.host`, `core.tests` |
| `Web Only` | `core`, `web.client`, `web.bridge`, `core.tests` |
| `Native Host Only` | `core`, `app.host`, `core.tests` |

The app name should be applied to namespaces, assembly names, app display names, package identifiers, and bundle identifiers. The role folder names should stay generic.

## Current PrayAdFree Mapping

| Template role | Current path |
| --- | --- |
| `core` | `PrayAdFree.Core` |
| `web.client` | `Pray.web` |
| `web.bridge` | `PrayAdFree.WebBridge` |
| `app.host` | `PrayAdFree` |
| `core.tests` | `PrayAdFree.Tests` |

## Packaging Note

Use this repo as the source for the first template package. The first package should be a .NET/Visual Studio template that preserves role names and replaces only project identity values.

Do not package generated build output, local logs, `bin`, `obj`, `dist`, `.vs`, or embedded web build artifacts as template source.

## Current Template Source

The concrete template source is:

```text
templates/multi-surface-app/content/
```

It contains `.template.config/template.json`, so it can be installed with the .NET template engine and shown by Visual Studio as a project template.

## Local Install

From the repo root:

```powershell
dotnet new install .\templates\multi-surface-app\content
```

Create a full app:

```powershell
dotnet new multi-surface-app -n MyPrayerApp --projectType full-app
```

Create a web-only app:

```powershell
dotnet new multi-surface-app -n MyPrayerWeb --projectType web-only
```

Create a native-host-only app:

```powershell
dotnet new multi-surface-app -n MyPrayerHost --projectType native-host-only
```

Widget infrastructure is deliberately opt-in. Generate it with:

```powershell
dotnet new multi-surface-app -n MyWidgetApp --projectType full-app --widgetSupport cross-platform
```

The default is `--widgetSupport none`, which creates no widget settings, routes, renderer code, or placeholder production UI.

Uninstall the local template:

```powershell
dotnet new uninstall .\templates\multi-surface-app\content
```

## Lovable Handoff

For projects created from this template, tell Lovable:

```text
Use the repo created from the Multi-surface App template. Work only in web.client unless explicitly asked otherwise. Read docs/ARCHITECTURE.md, docs/CONTRACTS.md, docs/LOVABLE.md, and web.client/src/generated before editing. Do not edit generated files, core, web.bridge, app.host, or build output. If UI needs new app data, request a core contract update.
```
