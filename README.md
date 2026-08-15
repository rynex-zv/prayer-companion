# Prayer Companion / Pray Ad Free

A cross-platform prayer companion focused on accurate prayer times, adhan, Qibla, Islamic calendar utilities, tasbih, alarms, notifications, widgets, and a clean ad-free experience.

> **Project status:** active development. The default development branch is `Alpha`.

## Features

- Daily prayer times with next-prayer countdown and progress
- Location-aware prayer calculations
- Configurable calculation method, madhhab, and high-latitude rules
- Adhan and alarm settings
- Qibla direction
- Islamic / Hijri calendar features
- Tasbih counter
- Notification and permission settings
- Theme and location settings
- Native widget support
- Shareable prayer-time information
- Cross-platform application targets

## Technology

The project is split into native, shared-core, web UI, and test layers.

- **.NET 10 / .NET MAUI** for the native application
- **C#** shared application/core logic
- **React 19 + TypeScript** for the embedded/web UI
- **Vite** for frontend builds
- **TanStack Router / React Query**
- **Tailwind CSS**
- Native targets currently include **Android, iOS, macOS (Mac Catalyst), and Windows**

### Repository layout

```text
PrayAdFree/              .NET MAUI application
PrayAdFree.Core/         shared core logic, contracts, models and services
PrayAdFree.Tests/        automated tests
Pray.web/                React / TypeScript frontend
PrayAdFree.WebBridge/    bridge between native and web layers
MauiWebber/              native/web hosting integration
native-widgets/          native widget implementations/providers
tools/                   repository tooling
docs/                    project documentation
```

## Requirements

- .NET SDK **10.0.110** or compatible latest patch
- Node.js + npm for the `Pray.web` frontend
- Platform-specific MAUI workloads for the target you want to build

## Build

Clone the repository:

```bash
git clone https://github.com/rynex-zv/prayer-companion.git
cd prayer-companion
git checkout Alpha
```

Install frontend dependencies:

```bash
cd Pray.web
npm install
cd ..
```

Build the solution:

```bash
dotnet build PrayAdFree.slnx
```

The MAUI project can also build the phone frontend automatically when its frontend dependencies are installed.

## Contributing

Contributions are welcome, but **changes are not accepted directly into the protected development branch without review**.

Please read [CONTRIBUTING.md](CONTRIBUTING.md) before submitting changes.

The intended workflow is:

1. Fork the repository or create your own branch.
2. Make and test your changes.
3. Open a Pull Request targeting `Alpha`.
4. Wait for maintainer review.
5. Changes are merged only after they are reviewed and accepted by the project maintainer.

Do not assume that a submitted Pull Request will be merged.

## License

This repository is licensed under the **GNU Affero General Public License v3.0 (AGPL-3.0)**. See [LICENSE.txt](LICENSE.txt).

In short, the AGPL allows people to use, study, modify, and redistribute the software, but modified versions distributed or made available to users over a network can carry source-code disclosure obligations under the license.

The license governs use of the source code. It does **not** grant anyone permission to bypass repository review rules or merge changes into this repository.

## Maintainer

Maintained by **Rynex (`@rynex-zv`)**.

For bugs, feature proposals, or improvements, please use GitHub Issues and Pull Requests.