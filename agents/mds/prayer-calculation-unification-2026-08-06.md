# Prayer calculation unification — 2026-08-06

## Outcome

- Web, Windows, and Android now resolve prayer times through the same `SharedCoreAdhan` engine in `PrayAdFree.Core`.
- The calculation engine is visible on the Adhan calculation settings page.
- Settings changes invalidate the Windows Today projection immediately; a stale calculation is no longer returned after a method/location change.
- Canonical city coordinates take precedence over reverse-geocode cache entries, so localized country names cannot silently select a nearby coordinate.
- Imsak advance is applied exactly once on every host.
- The method selector contains 23 verified choices; the engine name is now a read-only provenance label rather than a one-choice dropdown.
- Unsupported Jafari/Tehran calculations fail explicitly instead of silently using sunset for Maghrib.

## Exact parity input

- City/country: Amsterdam, NL
- Coordinates: `52.3676, 4.9041`
- Method: Auto (Muslim World League for NL)
- Madhhab: Shafi
- High-latitude rule: Middle of the Night
- Clock: 24 hour

## Exact parity result

| Prayer | Web | Windows |
|---|---:|---:|
| Fajr | 03:20 | 03:20 |
| Sunrise | 06:08 | 06:08 |
| Dhuhr | 13:47 | 13:47 |
| Asr | 17:53 | 17:53 |
| Maghrib | 21:23 | 21:23 |
| Isha | 23:54 | 23:54 |

## Acceptance evidence

- Latest Windows run: `windows-2026-08-06T00-33-26-408Z` — 9 passed, 0 failed, 0 warnings, slowest bridge call 57 ms.
- Latest .NET suite: 180 passed, 0 failed.

- Web run: `Pray.web/automation-results/web-2026-08-05T22-46-25-672Z/` — 9 passed, 0 failed.
- Windows run: `C:\Users\Rynex\AppData\Local\User Name\com.rynex.prayer.automation\Data\AutomationReports\windows-2026-08-05T22-53-07-476Z\` — 9 passed, 0 failed, 0 warnings.
- Windows cold bootstrap: 157 ms, below the 300 ms ceiling.
- .NET tests: 153 passed, 0 failed.
- TypeScript check and Windows Debug automation build: passed.

## Why WebAssembly remains

WebAssembly runs the exact .NET shared core in a browser where MAUI is unavailable. It is no longer a separate prayer-time implementation; it is another host for the same calculation assembly.
