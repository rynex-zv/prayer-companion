# Windows automated acceptance — final Debug and Release builds (2026-08-10)

##input

- Windows Debug build with `PrayAutomation=true` and isolated automation state.
- Embedded React bundle loaded from `https://app.prayadfree.local/index.html` through WebView2.
- All application routes and enabled controls, plus eight sequential user journeys.
- Same-device frontend/backend ceiling: 300 ms; warning threshold: 200 ms.

##Actions

- Built the embedded phone assets and Windows app from source with one reproducible build command.
- Ran the code-driven React automation sequentially inside the Windows app; no website fallback and no coordinate guessing were used.
- Changed and restored every enabled input supported by each route, exercised navigation and CRUD workflows, and waited for confirmed backend projections.
- Continued after failures, wrote separate passed/failed Markdown reports, fixed the discovered defects, rebuilt, and repeated from clean isolated state.

##Tested

- Final native run: `windows-2026-08-10T21-52-56-946Z`.
- Passed: **10**; failed: **0**; scenario warnings: **0**.
- Page/control contract: **529 assertions**; all scenarios together: **734 assertions**.
- Jafari and Tehran now have exact shared-engine Maghrib-angle tests and each returned six formatted prayer times.
- Release UI Automation: 1 Document, 9/9 interactive controls named, including all five React navigation controls.
- Final automation log: **206** backend completions (maximum 43 ms) and **210** bridge completions (maximum 49 ms); **0 calls above 300 ms**.
- Current Release uses only `https://app.prayadfree.local/`; no website fallback and no unsafe `file:` navigation.
- All **214** .NET tests, TypeScript, embedded production build, Windows Debug automation build, and Windows Release build passed.

##Faild+why

- None in the final Windows automated route/control/data-call matrix.
- Android device execution remains unrun because `adb devices` reports no connected device.
- Store publication is blocked until a private production Android signing key is supplied; the locally generated signed bundle uses the Android Debug certificate and must not be published.

##things to fix

- Run the same suite on a connected Android device or emulator and retain its two generated reports.
- Configure protected production Android signing credentials and verify the resulting release certificate before store upload.
- Keep new controls supplied with an accessible name and stable `data-selector-name`; the page contract now fails when either is missing.

##remarks

- Native passed report: `C:\Users\Rynex\AppData\Local\User Name\com.rynex.prayer.automation\Data\AutomationReports\windows-2026-08-10T21-52-56-946Z\passed.md`.
- Native failed report confirms zero failures in the same directory.
- Persistent native evidence: `C:\Users\Rynex\Desktop\PrayAdFreeLogs\PrayAdFree-events.log`.
- Final Jafari screenshot: `agents/mds/windows-release-visual-2026-08-06/captured/today-jafari-fixed-v2-2026-08-10.png`.
- The sibling Markdown files contain the per-page input, actions, tested controls, failures, fixes, and remarks.
