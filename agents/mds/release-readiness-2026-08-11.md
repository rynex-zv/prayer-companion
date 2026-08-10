# Release readiness report — 2026-08-11

## Outcome

Windows now renders the React application and real prayer times for the user's persisted Jafari method. The final Windows automation run passed every scenario and the Release accessibility tree is populated. Android APK and AAB artifacts build successfully, but they are not store-ready because no production signing key is configured and no Android device is connected for installation acceptance.

## Repaired

- Replaced Windows `file:` navigation with the stable `https://app.prayadfree.local/` virtual host and removed live-site fallback.
- Attached a windowed WebView2 controller so semantic React controls appear in Windows UI Automation.
- Added file-backed native/runtime diagnostics, operation/request IDs, backend/persistence/round-trip timings, and explicit release blockers.
- Changed interactive system operations to acknowledge quickly and report truthful asynchronous completion or failure.
- Removed silent persisted-state fallbacks and duplicate/refresh request patterns.
- Unified native and browser prayer calculations in the shared .NET/WebAssembly core with explicit location time zones.
- Added exact Jafari (Fajr 16°, Maghrib 4°, Isha 14°) and Tehran (Fajr 17.7°, Maghrib 4.5°, Isha 14°) support without substituting sunset or another method.
- Invalidated the cached Today error projection after calculation-engine support changed.
- Fixed the Today repair-button localization and Tasbih localization keys.

## Windows evidence

- Automation run: `windows-2026-08-10T21-52-56-946Z`.
- Scenarios: 10 passed, 0 failed, 0 warnings.
- Assertions: 734 total; the page contract alone completed 529 assertions.
- Calls: 206 backend completions, maximum 43 ms; 210 frontend/backend bridge completions, maximum 49 ms; 0 above 300 ms.
- Log scan: 0 `ReleaseBlocker`, 0 unsafe `file:` navigation, 0 calls above the ceiling.
- Release UI Automation: 1 Document; 9 interactive controls; 9 named controls.
- Jafari Release render: six prayer timings and no `today:error`.
- .NET: 214 passed, 0 failed.
- TypeScript: passed.
- Windows Release build: passed with 0 errors. The clean build still reports 338 XamlC compiled-binding optimization warnings in legacy native XAML pages.

## Android artifacts

- `artifacts/android-release-2026-08-11/com.rynex.prayer-Signed.apk`
  - SHA-256: `4D90DE56FA12D89EFA99C8CDC716BD8D545D6E3E4E034D7B9369C755DD20A162`
- `artifacts/android-release-2026-08-11/com.rynex.prayer-Signed.aab`
  - SHA-256: `69E23B8D8BFAA823343E33DAD37292D2F63AFDA95EFB9DD0C100D3A6AE1B8160`
- Both signatures verify technically.
- Both are signed by `CN=Android Debug, O=Android, C=US`; these files must not be uploaded as production releases.

## Remaining blockers

- Supply/configure a protected Android production signing key and rebuild both artifacts.
- Connect an Android device or emulator and run the same automation/install/restart matrix; `adb devices` currently returns no device.
- Physical delivery acceptance remains required for notification delivery, alarm screen, audio playback, GPS, and OS permission prompts. Their automation-mode acknowledgement/completion contracts pass, but simulation is not physical-device evidence.
- The 338 legacy XamlC warnings are not runtime failures, but should be removed before enforcing a warning-free native build policy.

## Reports

- Per-page reports: `agents/mds/windows-acceptance-2026-07-13/`.
- Automation scenario documentation: `agents/mds/automation-scenarios/`.
- Passed report: `C:\Users\Rynex\AppData\Local\User Name\com.rynex.prayer.automation\Data\AutomationReports\windows-2026-08-10T21-52-56-946Z\passed.md`.
- Failed report in the same directory confirms zero failures.
- Final visual: `agents/mds/windows-release-visual-2026-08-06/captured/today-jafari-fixed-v2-2026-08-10.png`.

## Machine state

- Windows master output level set to 0%; mute is enabled.
- No PrayAdFree test process is intentionally left running.
