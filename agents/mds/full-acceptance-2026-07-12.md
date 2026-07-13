# Full Web and Windows Acceptance — 2026-07-12

Build under test:

- Live web: `https://pray.rynex.nl/`, asset `index-C8OgqFz1.js`
- Windows: embedded web version 207, asset `index-DhAOFjqM.js`
- Scope: every documented route, all visible safe buttons, every editable input/select, and per-page console/runtime logs.
- Status vocabulary: `WORKING`, `PARTIAL`, `BROKEN`, `BLOCKED`.

Permission buttons are exercised only where the platform exposes a prompt/result. External email/phone/URL actions are checked for invocation without completing communication. Destructive cache/update actions are recorded separately.

## Page matrix

| Runtime | Route | Status | Buttons checked | Inputs changed | Log/result evidence |
|---|---|---|---|---|---|
| Web | `/` | **WORKING** | Refresh and every bottom navigation tab (Today, Calendar, Qibla, Tasbih, Settings). | No editable inputs. | Page rendered current prayer data and all five tabs reached their documented routes; no new page error. |
| Web | `/calendar` | **PARTIAL** | Previous/next month, Gregorian/Hijri, all 31 day cells in the tested month, Year/Month/Week/Day/Today, all 12 year cards, and all 7 week dates. | No editable inputs. | Navigation worked without a console exception. Mixed English/French headings, controls, month names, and weekday names appear simultaneously. |
| Web | `/qibla` | **PARTIAL** | Auto, Manual, Compass, Map, None, Night, Contrast, and both map zoom controls. | No editable inputs. | Modes worked without a console exception. Both map controls are blank buttons with no accessible name/title/ARIA label. |
| Web | `/tasbih` | **WORKING** | Counter, Reset, and all preset choices. | Preset changed through `hundred`, `salawat`, and `after-prayer`, then restored. | Counter/reset and preset changes worked without a page error. |
| Web | `/settings` | **WORKING** | All eight cards: Locations, Theme & Diagnostics, Adhan, Notifications, Permissions, Alarm reminders, Tasbih, About; bottom navigation already covered on `/`. | No editable inputs. | Every card reached its expected child route. |
| Web | `/settings/locations` | **BROKEN** | GPS toggle triggered; Refresh GPS became unreachable after crash; Back not reached. | Country `NL→SA`, city changed, latitude `52.3676→24.7136`, longitude `4.9041→46.6753`, reading mode `compass→map`, filter `none→night`. | Reproduced `TypeError: Cannot read properties of undefined (reading 'find')`; route replaced by root error boundary. This matches the user-supplied console log. |
| Web | `/settings/theme` | **WORKING** | System/Light/Dark, all five accents, text decrease/increase, and Back. | Language selected through `en`, `ar`, `es`, `tr`, `fr` and restored. | Controls persisted and Back returned to Settings; no console error. |
| Web | `/settings/adhan` | **BROKEN** | Add custom sound, sound selection, Imsak Add, Iftar Add; Makkah Play was disabled. | Volume, method, madhhab, high-latitude rule, Fajr/Isha angles, all seven offsets, Iftar delay, Imsak advance, and clock format changed and restored. | Page says Saved, but both reminder Add buttons create no reminder rows. Custom sound gives no useful browser feedback. No console exception, but requested operations fail. |
| Web | `/settings/notifications` | **BROKEN** | Four toggles, Add, Test notification, and Test alarm. | Primary type, lead minutes, vibration strength/pattern, and scope changed/restored. | Selecting `SpecificPrayer` leaves the prayer selector empty, so the reminder cannot be configured. Tests produce no console exception. |
| Web | `/settings/permissions` | **WORKING** | Location Grant, Notifications Grant, Background Unavailable. | No editable inputs. | Buttons were invoked; browser background alarms are explicitly reported unavailable. No console error. |
| Web | `/settings/alarms` | **WORKING** | All three built-in toggles; created, toggled, edited, and removed a temporary reminder. | Temporary reminder name changed and then removed. | CRUD path completed and cleanup verified; no console error. |
| Web | `/settings/tasbih` | **BROKEN** | Preset selection, all six item up/down controls, Add item, Add preset. | Preset name/repeat and every item text/count changed/restored; temporary item/preset fields filled. | Add item produced no item. Add preset changed page status to `Error` and produced no preset; no console exception was emitted. Existing destructive Remove controls were not used because failed Add prevented a reversible test fixture. |
| Web | `/settings/about` | **BROKEN** | Save/reset remote URL, Email, Call, Website, Report issue, Pull latest web version, Clear app cache. | Remote URL changed to the live origin, saved, then reset. | Browser falsely reports `Pulled latest web version` and `Last pulled version: browser` for a native-only operation. Mixed English/French content remains after language changes. Cache clear reloaded safely; no console error. |
| Web | `/onboarding` | **BROKEN** | All five language choices, Back/Next on each step, all four permission buttons, GPS toggle, location permission, Finish. | Country/city and latitude/longitude changed to Netherlands/Amsterdam values. | Permission status remains `0 / 3`; more importantly, Finish leaves the user on step 3 instead of completing/redirecting. No visible error boundary. |
| Web | `/alarm` | **WORKING** | No action buttons exist while inactive. | No editable inputs. | Direct route renders `Aucune alarme active`; mixed locale is tracked separately. Active Snooze/Stop controls require an actually firing alarm and are not present in this state. |

## Windows acceptance boundary

The Windows executable was located and already running. I began the native pass and confirmed the embedded app renders its Today, Calendar, Qibla, Tasbih, and Settings surfaces, but the Computer Use accessibility tree returned `null` for every page. Coordinate-only clicking was then stopped because the window reported concurrent user input and the user explicitly said not to take control of the PC while playing. No Windows button/input result is being represented as verified here.

The requested fresh install was not performed: installing or relaunching would take over/overwrite the active app while the user is using the PC. A clean `dotnet build PrayAdFree\PrayAdFree.csproj --configuration Debug --no-restore` was started for evidence, but its MSBuild workers remained active beyond the command timeout and produced no completion result. No app log files were found under the expected local app-data locations.
| Windows | `/`, `/calendar`, `/qibla`, `/tasbih`, `/settings/*`, `/onboarding`, `/alarm` | **BLOCKED** | Page surfaces were observed, but no element-level button click is claimed. | No Windows input mutation is claimed. | The running executable's WebView returned `accessibility: null`; coordinate actions were stopped after concurrent user input was detected. No Windows page is marked working without a safe, repeatable control/log trace. |

## Per-page control and input checklist

This is the detailed web evidence. `PASS` means the control was exercised and its expected result observed; `FAIL` means it was exercised and did not work; `N/A` means the page had no such control; `BLOCKED` means it was not safely exercised.

| Page | Buttons/controls clicked | Inputs/selects changed | Result |
|---|---|---|---|
| Web `/` | Refresh; Today; Calendar; Qibla; Tasbih; Settings | N/A | PASS — each route rendered and navigation returned to the expected page. |
| Web `/calendar` | Previous month; next month; Gregorian; Hijri; Year; Month; Week; Day; Today; all 31 day buttons; all 12 month cards (including May); all 7 week dates | N/A | PASS for interaction; PARTIAL overall because English and French labels are mixed. |
| Web `/qibla` | Auto; Manual; Compass; Map; None; Night; Contrast; both visible map controls | N/A | PASS behaviorally; FAIL accessibility — both map controls have blank accessible names. |
| Web `/tasbih` | Counter twice; Reset; preset selector choices `hundred`, `salawat`, `after-prayer` | Preset changed and restored | PASS. |
| Web `/settings` | Locations; Theme & Diagnostics; Adhan; Notifications; Permissions; Alarm reminders; Tasbih; About | N/A | PASS — all eight cards reached the expected route. |
| Web `/settings/locations` | GPS toggle; Refresh GPS became unreachable after the crash; Back became unreachable | Country NL→SA; city; latitude `52.3676→24.7136`; longitude `4.9041→46.6753`; reading compass→map; filter none→night | FAIL — error boundary: `Cannot read properties of undefined (reading 'find')`. |
| Web `/settings/theme` | System; Light; Dark; all five accents teal/green/blue/amber/rose; text decrease; text increase; Back | Language `en`, `ar`, `es`, `tr`, `fr`, restored `fr` | PASS — selections and Back worked. |
| Web `/settings/adhan` | Add custom sound; Makkah sound selection; Makkah Play (disabled); Imsak Add; Iftar Add | Volume 80→70→80; method Auto→Karachi→Auto; madhhab Shafi→Hanafi→Shafi; high-latitude Middle→Twilight→Middle; Fajr 18→19→18; Isha 17→18→17; all seven offsets 0→1→0; Iftar delay 0→1→0; Imsak advance 10→11→10; 24h→12h→24h | FAIL — both reminder Add buttons report Saved but create no rows. Play is disabled. |
| Web `/settings/notifications` | Adhan, vibration, hide-on-close, background toggled off/on; Add; Test notification; Test alarm | Primary type changed/restored; minutes 10→11→10; vibration Medium→Strong→Medium; pattern Default→Pulse→Default; scope All→SpecificPrayer→All | FAIL — SpecificPrayer leaves an empty prayer selector, so that reminder cannot be configured. |
| Web `/settings/permissions` | Location Grant; Notifications Grant; Background Unavailable | N/A | PASS for exposed browser capability; background alarms correctly report unavailable. |
| Web `/settings/alarms` | Wudu, qibla, and user-reminder toggles; Add; dynamic toggle; Remove | Temporary reminder name created, edited, then removed | PASS — CRUD and cleanup completed. |
| Web `/settings/tasbih` | Preset selector; all six item up/down controls; Add item; Add preset | Preset name/repeat and all three item text/count fields changed/restored; new item text/count filled; new preset name filled | FAIL — Add item creates no item; Add preset changes page status to Error and creates no preset. |
| Web `/settings/about` | Save; Reset default; Email; Call; Open website; Report issue; Pull latest web version; Clear app cache | Remote URL changed to `https://pray.rynex.nl/`, saved, then reset | FAIL — browser reports a false successful native-version pull; mixed language remains. Cache clear reloads safely. |
| Web `/onboarding` | All language choices; Back/Next; all four Grant permissions buttons; GPS toggle; Grant permissions; Back/Next; Finish | Country/city changed to Netherlands/Amsterdam; latitude/longitude changed to `52.3676`/`4.9041` | FAIL — permission status remains `0 / 3`; Finish stays on step 3. |
| Web `/alarm` | N/A — inactive state has no Snooze/Stop buttons | N/A | PASS for inactive route only; active alarm controls were unavailable without a firing alarm. |
| Windows all routes | BLOCKED — no page-level click is claimed | BLOCKED — no input mutation is claimed | The running WebView returned `accessibility: null`; coordinate automation was stopped after concurrent user input was detected, per the user’s PC-control instruction. |

## Console/log evidence by page

| Page | Console/runtime result |
|---|---|
| `/settings/locations` | Real TypeError in `index-C8OgqFz1.js`: undefined `.find`; repeated `settings.getSnapshot` diagnostics were also observed. |
| `/`, `/calendar`, `/qibla`, `/tasbih`, `/settings/theme`, `/settings/adhan`, `/settings/notifications`, `/settings/permissions`, `/settings/alarms`, `/settings/tasbih`, `/settings/about`, `/onboarding`, `/alarm` | No new JavaScript exception was emitted during the recorded interactions; several failures are silent UI/command failures. |
| Windows | Not verified: no accessibility tree and no safe element-level log correlation after user input invalidated coordinate state. |

## Confirmed defects

1. **Locations crashes after authoritative settings update.** A successful `settings.update` response does not preserve the full Locations projection shape expected by the page; rendering calls `.find` on a missing collection. Repeated refreshes also produce duplicate `settings.getSnapshot` diagnostics.
2. **Adhan reminder creation silently fails.** Both reminder Add actions report Saved but create nothing.
3. **Specific-prayer notifications cannot be configured.** The prayer selector has zero options in `SpecificPrayer` scope.
4. **Tasbih configuration mutations fail.** Add item has no effect and Add preset ends in visible `Error` state.
5. **About exposes a false browser update success.** A native-only pull reports success and records `browser` as the pulled version.
6. **Onboarding cannot complete.** Finish does not advance or redirect from step 3.
7. **Localization state is inconsistent.** Calendar, About, and Alarm mix English and French in a single active language state.
8. **Qibla map controls are inaccessible.** Both zoom buttons have empty accessible names.
9. **Windows native acceptance is blocked.** The embedded WebView exposes no accessibility tree, the user prohibited PC takeover, and the fresh-install/build verification did not complete. This is an acceptance blocker, not a pass.
10. **Windows embedded navigation violates file-origin policy.** The supplied console reports an unsafe attempt to load the same embedded `file:///.../index.html#/settings/locations`; `file:` documents receive unique security origins.
11. **Windows WebView exposes no UI Automation accessibility tree.** Repeated full-window captures return `accessibility: null`, so screen readers and reliable element-level automation cannot address any page control.

## Automated verification

- `Pray.web`: TypeScript check passed; client architecture boundary passed; production build passed and reproduced live asset `index-C8OgqFz1.js`.
- `.NET`: 145/145 tests passed (`PrayAdFree.Tests`, `net10.0`).
- These green checks do not cover the live runtime failures above; several broken operations return no console exception and therefore need behavioral acceptance tests.

## Windows acceptance blocker

The Windows app is open and visually inspectable, but its WebView returns no accessibility elements. Coordinate testing additionally stopped multiple times with `user input was detected in this window`, meaning the window changed after capture and stale clicks were correctly refused. The remaining Windows pages are not marked working without evidence.

## Not yet tested

All Windows route-level controls and inputs remain unverified because the user prohibited PC takeover while playing and the app exposes no accessibility tree. Active Alarm additionally requires a real firing-alarm state so Snooze/Stop exist.
