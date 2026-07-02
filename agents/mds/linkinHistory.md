# Linkin History

Status meanings: `BROKEN`, `PARTIAL`, `WORKING`, `NOT VERIFIED`.
Do not delete this file; append/update rows as checks are completed.

| Area | Route | Screenshot | Language | Buttons | Inputs | Theme | Backend tracking | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|
| Today | `/` | WORKING | PARTIAL | WORKING | n/a | WORKING | WORKING | PARTIAL | Windows WebView no longer white; `windows-today-rpc-working.png` captured. MAUI RPC confirmed for `today.getSnapshot`, `app.getShellSnapshot`, and `renderComplete`. Language still needs full route audit. |
| Calendar | `/calendar` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | n/a | NOT VERIFIED | NOT VERIFIED | BROKEN | Initial log created; runtime screenshot pending. |
| Qibla | `/qibla` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | n/a | NOT VERIFIED | NOT VERIFIED | BROKEN | Initial log created; runtime screenshot pending. |
| Tasbih | `/tasbih` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | n/a | NOT VERIFIED | NOT VERIFIED | BROKEN | Initial log created; runtime screenshot pending. |
| Settings index | `/settings` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | n/a | NOT VERIFIED | NOT VERIFIED | BROKEN | Initial log created; runtime screenshot pending. |
| Settings locations | `/settings/locations` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | BROKEN | `settings.patch` currently no-op in MAUI. |
| Settings theme | `/settings/theme` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | BROKEN | `settings.patch` and `app.setTheme` currently no-op in MAUI. |
| Settings adhan | `/settings/adhan` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | BROKEN | `settings.patch`/`settings.invoke` currently no-op in MAUI. |
| Settings notifications | `/settings/notifications` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | BROKEN | `settings.patch`/`settings.invoke` currently no-op in MAUI. |
| Settings permissions | `/settings/permissions` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | BROKEN | Permission actions currently no-op through web handler. |
| Settings alarm reminders | `/settings/alarms` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | BROKEN | `settings.patch` currently no-op in MAUI. |
| Settings tasbih | `/settings/tasbih` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | BROKEN | `settings.invoke` currently no-op in MAUI. |
| Settings about | `/settings/about` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | n/a | NOT VERIFIED | NOT VERIFIED | BROKEN | About actions currently no-op in MAUI. |
| Onboarding | `/onboarding` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | BROKEN | Initial log created; runtime screenshot pending. |

## 2026-07-02 progress update

Evidence captured:
- Main page screenshots exist under `agents/mds/screenshots/`: `today.png`, `calendar.png`, `qibla.png`, `tasbih.png`, `settings.png`.
- Settings subpage screenshot capture is still failing/hanging in the local browser automation, matching the reported subpage problem; those rows remain not working/not verified.
- `settings.patch`, `settings.invoke`, `app.setLanguage`, `app.setTheme`, and `onboarding.complete` were no-ops in `PrayAdFree/Services/WebAppRpcHandler.cs`; this was confirmed from source and patched.
- Windows build now passes after patching settings state and rebuilding phone assets.

Current verified commands:
- `npm run typecheck` in `Pray.web`: PASS.
- `npm run build:phone` in `Pray.web`: PASS.
- `dotnet build PrayAdFree/PrayAdFree.csproj -f net10.0-windows10.0.19041.0 --no-restore`: PASS.

Current status by requirement:
- Settings state persistence: PARTIAL. Backend now saves location, theme/language, adhan basics, notifications, alarm reminders, onboarding completion, and tasbih preset/item edits. Runtime interaction screenshots still pending.
- Language: PARTIAL. Settings index, Locations, and Theme now read shell labels; many other pages still contain hardcoded English and must stay BROKEN/PARTIAL until replaced and screenshot-checked.
- Theme: PARTIAL. `app.setTheme` and `settings.patch.theme` now save/apply theme and trigger shell refresh. Runtime screenshot check pending.
- Inputs: PARTIAL. Backend accepts major settings input patches; every visible input still needs route-by-route runtime verification.
- Screenshots: PARTIAL. Main 5 pages captured; 8 settings subpages and onboarding still pending.

Rows not marked WORKING yet because the user requested all rows be verified with screenshots and checks before completion.

## 2026-07-02 Android screenshot evidence

- Captured linked-phone screenshot: agents/mds/screenshots/android-current.png.
- The screenshot is on Settings Adhan and shows mixed Arabic/English: Arabic sound names with English headings/buttons/fields (Adhan, Adhan sound, Add custom sound, Test notification, Calculation, Method, Madhhab, High latitude rule, Offsets).
- Therefore language for Settings Adhan remains BROKEN until labels are converted to shell/localization data and rechecked.

## 2026-07-02 Windows white screen fix

- Reproduced the Windows blank WebView cause with the generated `file://` bundle: external module scripts were blocked, so React did not mount.
- Fixed `Pray.web/scripts/build.mjs` for phone builds to inline JS and CSS into `index.html`; corrected inlined CSS font URLs to `assets/...`.
- Added a phone HTML bridge bootstrap before React starts so `window.mauiWebber` exists immediately and the frontend does not fall back to mocks on Windows.
- Verified Windows MAUI build: `dotnet build PrayAdFree/PrayAdFree.csproj -f net10.0-windows10.0.19041.0 --no-restore` PASS with 0 warnings and 0 errors.
- Verified Windows runtime logs after launch: `today.getSnapshot`, `app.getShellSnapshot`, and `renderComplete` were handled through MAUI RPC.
- Captured direct WebView screenshot: `agents/mds/screenshots/windows-today-rpc-working.png`.
- Remaining work: continue route-by-route screenshots and input/language/button checks before marking all rows WORKING.


