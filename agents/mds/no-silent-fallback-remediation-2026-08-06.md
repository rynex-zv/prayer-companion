# No-silent-fallback remediation — 2026-08-06

## Policy

Missing optional PATCH fields preserve their current value. A field that is present but malformed, unsupported, out of range, or aimed at a missing entity fails the RPC and is logged. It must never be substituted, clamped, ignored, or returned as an unchanged success.

## Confirmed defects repaired

1. Unknown time zones no longer use the device zone.
2. Unknown countries under Auto no longer use Muslim World League implicitly.
3. Undefined calculation methods and high-latitude rules now fail.
4. Jafari and Tehran are not approximated by sunset Maghrib; this engine cannot represent their required Maghrib angles, so they are explicitly unsupported.
5. Portugal and Jordan Maghrib adjustments and Umm Al-Qura Ramadan Isha adjustment are represented.
6. Invalid Qibla modes, Settings sections, languages, themes, accents, text sizes, enums, numbers, booleans, reminder units/directions, and native JSON value types fail explicitly.
7. Unknown Tasbih presets/items/actions, invalid move directions, boundary moves, non-positive targets, and removal of the last preset/item fail explicitly.
8. Corrupt persisted settings/Adhan JSON is retained for diagnosis and fails; it is not overwritten with defaults.
9. Native notification reminder parsing is performed once per mutation, not twice.
10. Browser-extension errors are separated from application runtime errors; unknown or app-origin errors remain failures.
11. GPS errors use localized backend labels rather than leaking translation keys.
12. A bootstrap failure now replaces the Skeleton with an explicit localized error and Retry action.
13. Windows bridge response/listener and console hooks are registered once; the duplicate native trace/resolve path was removed.
14. Windows uses `https://app.prayadfree.local/`; `file:` navigation is not used.

## Calculation-method audit

- The UI now exposes 23 verified choices, including Auto and Custom.
- The calculation engine is a read-only provenance label, not a misleading one-option selector.
- Jafari and Tehran are excluded until the shared engine supports their Maghrib-angle and midnight contracts exactly.
- Turkey and Dubai are labeled experimental/approximate in every shipped locale.
- Primary references: AlAdhan method catalog/API and BatoulApps Adhan method documentation.

## Final deterministic evidence

- .NET: **180 passed, 0 failed**.
- TypeScript, runtime-defect tests, and architecture checks: passed.
- Embedded phone/WebView bundle: rebuilt successfully.
- Windows Debug automation build: **0 warnings, 0 errors**.
- Windows run `windows-2026-08-06T00-33-26-408Z`: **9 passed, 0 failed**.
- Page contract: **521 assertions**, 0 warnings.
- Native bridge: **193 calls**, slowest **57 ms**, **0** above 300 ms.
- Resolve accounting: **193 starts / 193 ends**, 0 unresolved responses.
- Current-session log: 0 `file:` origin errors, 0 `ReleaseBlocker`, 0 `found:false`.

## Remaining external acceptance

Android device/emulator execution has not been completed in this run. The same shared code and test-mode source compile, but no Android production-readiness claim is made without a device report.
