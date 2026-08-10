# Windows automated acceptance — final Debug build

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

- Final native run: `windows-2026-08-06T00-33-26-408Z`.
- Passed: **9**; failed: **0**.
- Page/control scenario: **521 assertions** across onboarding and every registered route.
- All **193** native bridge calls completed; slowest was `app.bootstrap=57 ms`; **0 calls exceeded 300 ms**.
- Resolve accounting was **193 starts / 193 ends** with 0 `found:false`.
- Current-session log scan: **0** unsafe `file:` URLs, failed scenarios, release blockers, or duplicate response listeners.
- All 180 .NET tests, TypeScript, runtime-defect tests, architecture checks, embedded production build, and Windows Debug build passed.

##Faild+why

- None in the final Windows automated route/control/data-call matrix.
- Android device execution was not run because `adb devices` returned no connected device. Android test-mode source compilation passed; full APK packaging timed out in the local toolchain and was stopped without claiming success.

##things to fix

- Run the same suite on a connected Android device or emulator and retain its two generated reports.
- Keep new controls supplied with an accessible name and stable `data-selector-name`; the page contract now fails when either is missing.

##remarks

- Native passed report: `C:\Users\Rynex\AppData\Local\User Name\com.rynex.prayer.automation\Data\AutomationReports\windows-2026-08-06T00-33-26-408Z\passed.md`.
- Native failed report confirms zero failures in the same directory.
- Persistent native evidence: `C:\Users\Rynex\Desktop\PrayAdFreeLogs\PrayAdFree-events.log`.
- The sibling Markdown files contain the per-page input, actions, tested controls, failures, fixes, and remarks.
