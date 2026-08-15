# Third-party credits and notices

Prayer Companion / Pray Ad Free uses open-source libraries, fonts, icons, and other third-party material. Those components remain the property of their respective copyright holders and are governed by their own licenses.

This file is an attribution and compliance index for the project's direct dependencies and bundled assets. It is not a replacement for the complete license text supplied by each upstream project, and downstream distributors should preserve the complete notices shipped with the dependency packages they redistribute.

## Native / .NET components

| Component | Use | License / notice |
| --- | --- | --- |
| .NET MAUI and related .NET package components | Cross-platform native application framework, maps, HTTP/logging and shared runtime libraries | MIT. Copyright .NET Foundation and Contributors. |
| Microsoft Windows App SDK | Windows application runtime/platform integration | MIT. Microsoft and contributors; preserve the upstream package notices. |
| Adhan (`Adhan` 0.9.0) | Prayer-time calculation | MIT. Copyright (c) 2019 davidpet86. |
| Plugin.LocalNotification (`Plugin.LocalNotification` 12.0.0) | Local notifications | MIT. Copyright (c) 2018 Elvin (Tharindu). |

Microsoft/.NET packages may also ship their own third-party-notice files. Preserve those package notices when redistributing the corresponding binaries or source.

## Web UI and frontend components

| Component | Use | License / notice |
| --- | --- | --- |
| React / React DOM | Web UI | MIT. Copyright Meta Platforms, Inc. and affiliates. |
| TanStack Query | Client data/state management | MIT. Copyright (c) 2021-present Tanner Linsley. |
| TanStack Router | Routing | MIT. Copyright (c) 2021-present Tanner Linsley. |
| Tailwind CSS | Styling | MIT. Copyright (c) Tailwind Labs, Inc. |
| Vite | Frontend build tooling | MIT. Copyright (c) 2019-present, VoidZero Inc. and Vite contributors. |
| `clsx` | Class-name utility | MIT. Copyright (c) Luke Edwards. |
| `tailwind-merge` | Tailwind class merging | MIT. Copyright (c) 2021 Dany Castillo. |
| `tw-animate-css` | CSS animation utilities | MIT. Copyright (c) 2025 Wombosvideo. |
| Lucide / `lucide-react` | UI icons | ISC for Lucide; Feather-derived icons identified by Lucide remain MIT, Copyright (c) 2013-present Cole Bemis. |
| Fontsource packaging | Self-hosted web-font packages | MIT for Fontsource packaging/tooling; each font itself keeps its own font license. |

Build-only and development-only packages are included here when they are a direct part of the project toolchain. Whether a particular package's notice must accompany a specific binary depends on what that binary actually redistributes.

## Fonts

The project bundles or consumes the following fonts. Their font licenses and copyright notices must be preserved when the font files are redistributed.

| Font | License / required credit |
| --- | --- |
| Cairo | SIL Open Font License 1.1. Copyright 2009 The Cairo Project Authors. |
| Noto Naskh Arabic | SIL Open Font License 1.1. Copyright 2022 The Noto Project Authors. |
| Open Sans | SIL Open Font License 1.1. Copyright 2020 The Open Sans Project Authors. |
| Amiri | SIL Open Font License 1.1. Copyright 2010-2022 The Amiri Project Authors. |
| Inter | SIL Open Font License 1.1. Copyright 2020 The Inter Project Authors. |

The SIL Open Font License permits embedding and redistribution subject to its conditions, including preservation of its copyright and license notice with redistributed font software.

## Built-in adhan recordings — rights verification required

The repository currently contains nine packaged built-in adhan MP3 files (`adhan_builtin_01.mp3` through `adhan_builtin_09.mp3`). The repository does not currently document a verified source, recording copyright owner, performer/reciter attribution, or redistribution license for those files.

There is also an additional root-level adhan MP3 whose filename references the reciter Islam Sobhi. A filename alone is not evidence of a redistribution license or permission.

**Do not treat credit alone as permission to redistribute these recordings.** Before a public app-store, website, or binary release containing them, verify the source and rights for every recording and then add the exact required attribution/license here. If redistribution rights cannot be verified, remove or replace the recording with audio that has documented permission.

## Project attribution is separate

The open-source credits above do not replace the attribution requirement for Prayer Companion / Pray Ad Free project-authored material. See [ATTRIBUTION.md](ATTRIBUTION.md).

## Keeping this file current

When adding a dependency, font, icon set, audio recording, translation source, image, dataset, or other third-party material:

1. verify that its license is compatible with the project and intended distribution;
2. retain the upstream copyright/license notice where required;
3. add the component, source, copyright holder, license, and any special attribution requirement to this file;
4. for media assets, keep evidence of the source and redistribution permission;
5. do not assume that something being downloadable from the internet makes it free to redistribute.
