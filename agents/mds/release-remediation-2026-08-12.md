# Release remediation — 2026-08-12

## Scope

Shared React/WebAssembly, Android MAUI, and Windows host fixes for compass sensing, location lifecycle, Adhan playback, permissions, Tasbih layout/localization, prayer-day rollover, downloads, and automation contracts.

## Compass heading fix

- Qibla bearing remains the astronomical bearing calculated from the selected coordinates.
- Device heading is now a separate live sensor value.
- Android MAUI starts and stops the native compass with the Qibla route lifecycle.
- Android magnetic heading is corrected to true north using local geomagnetic declination.
- Web prefers `AbsoluteOrientationSensor`; it falls back to absolute `DeviceOrientation` only and rejects relative-only orientation data.
- Circular filtering is used across the 0/360 boundary; ordinary arithmetic averaging is not used for headings.
- The UI exposes the measured device-heading number so it can be compared against the phone's system compass.

## Other repaired behavior

- Separated Qibla display/visual modes from sensor reading/filter modes in the shared state contract.
- Prevented missing catalog arrays from crashing Locations and Permissions routes.
- Windows-only notification controls are hidden on Android and web.
- Added explicit Adhan preview stop and collapsed per-prayer sound overrides.
- Preserved a confirmed location when a transient resume refresh fails.
- Suppressed the full-screen location chooser for transient failures when a usable location exists.
- After Isha, Today uses the next day's real prayer projection instead of relabeling passed current-day rows.
- Fixed Tasbih localization keys and small-screen overflow.
- Native About checks the remote artifact manifest and exposes platform-specific downloads.
- Download artifact names include both native and embedded-web versions.

## Verification

- TypeScript typecheck: passed.
- Architecture checks: passed.
- Production web build: passed.
- .NET tests: 232 passed, 0 failed.
- Android Release publish: passed.
- Windows x64 Release framework-dependent publish: passed.
- Web automation: 10 passed, 0 failed, 0 warnings.
- Page-contract automation: 470 assertions.
- Same-device automation bridge calls observed below the 300 ms ceiling.

## Packaged artifacts

- Android: `PrayAdFree-Android-0.0.501-web378.apk`
- Windows: `PrayAdFree-Windows-x64-0.0.501-web378.zip`

## Remaining physical acceptance

Automated and desktop tests cannot certify a real magnetometer. On a physical Android device, compare the app's displayed device heading with the system compass while rotating through north, east, south, west, and across 359°/0°. This is a required release acceptance check, not an automated pass claim.
