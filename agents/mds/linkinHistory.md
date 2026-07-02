# Linkin History

Do not delete this file. Update it while checking each page.

Overview status values: `BROKEN`, `PARTIAL`, `WORKING`, `NOT VERIFIED`.
Per-check status values: `WORKING GOOD`, `BROKEN`, `STATUS SAVED BROKEN`, `VALUE NOT CHANGING`, `NOT-CHECKED`.

## Overview

| Page | Route | Status | Current blocker | Evidence |
|---|---|---|---|---|
| Today | `/` | PARTIAL | Needs current screenshot/language/button pass after latest frontend routing changes. | Previous Windows runtime rendered Today and MAUI RPC handled `today.getSnapshot`, `app.getShellSnapshot`, `renderComplete`. |
| Calendar | `/calendar` | NOT VERIFIED | Not checked in current pass. | Not checked. |
| Qibla | `/qibla` | NOT VERIFIED | Not checked in current pass. | Not checked. |
| Tasbih | `/tasbih` | NOT VERIFIED | Not checked in current pass. | Not checked. |
| Settings index | `/settings` | PARTIAL | Code fix applied for raw labels and hover URLs; runtime screenshot/console check still required before marking working. | `npm run typecheck` passed after replacing internal anchors with buttons and adding console route API. |
| Settings locations | `/settings/locations` | PARTIAL | Needs runtime input and saved-state verification. | GPS blank-page guard added earlier; current pass still needs verification. |
| Settings theme | `/settings/theme` | NOT VERIFIED | Not checked in current pass. | Not checked. |
| Settings adhan | `/settings/adhan` | NOT VERIFIED | Not checked in current pass. | Not checked. |
| Settings notifications | `/settings/notifications` | NOT VERIFIED | Not checked in current pass. | Not checked. |
| Settings permissions | `/settings/permissions` | NOT VERIFIED | Not checked in current pass. | Not checked. |
| Settings alarm reminders | `/settings/alarms` | NOT VERIFIED | Not checked in current pass. | Not checked. |
| Settings tasbih | `/settings/tasbih` | NOT VERIFIED | Not checked in current pass. | Not checked. |
| Settings about | `/settings/about` | NOT VERIFIED | Not checked in current pass. | Not checked. |
| Onboarding | `/onboarding` | NOT VERIFIED | Not checked in current pass. | Not checked. |

## Console Helpers

Use these in the WebView console after the latest frontend bundle is running:

| Helper | Purpose | Status |
|---|---|---|
| `window.prayerCompanion.getRoutes()` | Returns all pages and subpages to check. | WORKING GOOD in typecheck, runtime check pending. |
| `window.prayerCompanion.navigate("/settings")` | Navigates without anchor hover/status URL. | WORKING GOOD in typecheck, runtime check pending. |
| `window.prayerCompanion.currentRoute()` | Returns current route. | WORKING GOOD in typecheck, runtime check pending. |
| `window.prayerCompanion.inspect()` | Lists route, language, direction, and `[data-selector-name]` elements. | WORKING GOOD in typecheck, runtime check pending. |

## Today `/`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | `window.prayerCompanion.navigate("/")` | Today renders without blank screen. | NOT-CHECKED | Needs current runtime screenshot. |
| Language | Visible labels | No mixed raw English/Arabic keys. | NOT-CHECKED | Needs current runtime screenshot. |
| Buttons | Refresh button and tabs | Clicks work and do not show file URL hover. | NOT-CHECKED | Needs current runtime console/screenshot. |
| Inputs | n/a | n/a | WORKING GOOD | No inputs on page. |
| Status saved | n/a | n/a | WORKING GOOD | No page settings to save. |

## Calendar `/calendar`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | `window.prayerCompanion.navigate("/calendar")` | Calendar renders. | NOT-CHECKED | Not checked. |
| Language | Visible labels | Calendar controls/prayer names localized. | NOT-CHECKED | Not checked. |
| Buttons | Month previous/next/today | Values change in frontend and backend tracked state. | NOT-CHECKED | Not checked. |
| Inputs | n/a | n/a | WORKING GOOD | No text inputs expected. |
| Status saved | Calendar month state | Month state remains consistent after navigation. | NOT-CHECKED | Not checked. |

## Qibla `/qibla`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | `window.prayerCompanion.navigate("/qibla")` | Qibla renders. | NOT-CHECKED | Not checked. |
| Language | Visible labels | No mixed raw English/Arabic keys. | NOT-CHECKED | Not checked. |
| Buttons | Mode/filter/manual controls | Buttons update frontend immediately and backend state is tracked. | NOT-CHECKED | Not checked. |
| Inputs | Manual heading controls | Manual movement is frontend-smooth, no backend lag loop. | NOT-CHECKED | Not checked. |
| Status saved | Qibla display/heading state | State remains after route navigation. | NOT-CHECKED | Not checked. |

## Tasbih `/tasbih`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | `window.prayerCompanion.navigate("/tasbih")` | Tasbih renders. | NOT-CHECKED | Not checked. |
| Language | Visible labels | No mixed raw English/Arabic keys. | NOT-CHECKED | Not checked. |
| Buttons | Increment/reset/preset buttons | Counter and preset state update. | NOT-CHECKED | Not checked. |
| Inputs | Preset select if visible | Value changes. | NOT-CHECKED | Not checked. |
| Status saved | Counter/preset state | Backend tracks selected preset. | NOT-CHECKED | Not checked. |

## Settings Index `/settings`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | `window.prayerCompanion.navigate("/settings")` | Settings index renders. | NOT-CHECKED | Needs current runtime screenshot. |
| Language | `settings:row:*` | Rows show localized labels, not raw `SettingsLocations` style keys. | NOT-CHECKED | Backend label map fixed; runtime check pending. |
| Buttons | `settings:row:*` | Each row navigates without WebView file URL hover/status. | NOT-CHECKED | Internal anchors replaced with buttons; runtime check pending. |
| Inputs | n/a | n/a | WORKING GOOD | No inputs on settings index. |
| Status saved | n/a | n/a | WORKING GOOD | Settings index has no state to save. |

## Settings Locations `/settings/locations`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | `window.prayerCompanion.navigate("/settings/locations")` | Location settings render, never blank. | NOT-CHECKED | Needs current runtime screenshot after latest bundle. |
| Language | Visible labels | No mixed raw English/Arabic keys. | NOT-CHECKED | Not checked. |
| Buttons | GPS toggle, refresh GPS, back | Buttons work and no hover file URL. | NOT-CHECKED | Not checked. |
| Inputs | Country, city, latitude, longitude | Values change in UI. | NOT-CHECKED | Not checked. |
| Status saved | `settings.patch.locations` | Values persist after leaving and returning. | NOT-CHECKED | Not checked. |

## Settings Theme `/settings/theme`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | `window.prayerCompanion.navigate("/settings/theme")` | Theme settings render. | NOT-CHECKED | Not checked. |
| Language | Language picker and labels | Changing language updates all visible labels. | NOT-CHECKED | Not checked. |
| Buttons | Theme segment, accent buttons, text size | UI changes immediately. | NOT-CHECKED | Not checked. |
| Inputs | Language/theme/accent/text size | Values change. | NOT-CHECKED | Not checked. |
| Status saved | `settings.patch.theme` | Theme/language persists after route navigation. | NOT-CHECKED | Not checked. |

## Settings Adhan `/settings/adhan`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | `window.prayerCompanion.navigate("/settings/adhan")` | Adhan settings render. | NOT-CHECKED | Not checked. |
| Language | All headings/fields/buttons | No mixed raw English/Arabic keys. | NOT-CHECKED | Earlier Android evidence showed mixed labels; must recheck after fixes. |
| Buttons | Sound select, preview, add sound, test notification, reminder add/remove | Buttons update UI and call backend only for tracked state/actions. | NOT-CHECKED | Not checked. |
| Inputs | Volume, method, madhhab, high latitude, angles, offsets, fasting, reminders, per-prayer overrides | Values change. | NOT-CHECKED | Not checked. |
| Status saved | `settings.patch.adhan` | Values persist after leaving and returning. | NOT-CHECKED | Not checked. |

## Settings Notifications `/settings/notifications`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | `window.prayerCompanion.navigate("/settings/notifications")` | Notifications settings render. | NOT-CHECKED | Not checked. |
| Language | Visible labels | No mixed raw English/Arabic keys. | NOT-CHECKED | Not checked. |
| Buttons | Toggles, test notification, test alarm | Buttons update UI and invoke actions. | NOT-CHECKED | Not checked. |
| Inputs | Adhan type, vibration strength/pattern, minutes before | Values change. | NOT-CHECKED | Not checked. |
| Status saved | `settings.patch.notifications` | Values persist after leaving and returning. | NOT-CHECKED | Not checked. |

## Settings Permissions `/settings/permissions`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | `window.prayerCompanion.navigate("/settings/permissions")` | Permissions settings render. | NOT-CHECKED | Not checked. |
| Language | Permission titles/descriptions/status/fallback | No raw enum names or mixed language. | NOT-CHECKED | Not checked. |
| Buttons | Request/open permission buttons | Buttons invoke expected permission action. | NOT-CHECKED | Not checked. |
| Inputs | n/a | n/a | WORKING GOOD | No inputs expected. |
| Status saved | Permission state | Refreshed status reflects backend snapshot. | NOT-CHECKED | Not checked. |

## Settings Alarm Reminders `/settings/alarms`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | `window.prayerCompanion.navigate("/settings/alarms")` | Alarm reminders render. | NOT-CHECKED | Not checked. |
| Language | Built-in/user labels and item text | No mixed raw English/Arabic keys. | NOT-CHECKED | Not checked. |
| Buttons | Built-in toggles, user toggle, add/remove | Buttons update UI. | NOT-CHECKED | Not checked. |
| Inputs | New reminder and edit reminder text | Values change. | NOT-CHECKED | Not checked. |
| Status saved | `settings.patch.alarmReminders` | Values persist after leaving and returning. | NOT-CHECKED | Not checked. |

## Settings Tasbih `/settings/tasbih`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | `window.prayerCompanion.navigate("/settings/tasbih")` | Tasbih settings render. | NOT-CHECKED | Not checked. |
| Language | Labels and repeat options | No mixed raw English/Arabic keys. | NOT-CHECKED | Not checked. |
| Buttons | Add preset/item, move, remove | Buttons update UI. | NOT-CHECKED | Not checked. |
| Inputs | Preset name, repeat mode, item text/count | Values change. | NOT-CHECKED | Not checked. |
| Status saved | `settings.invoke` tasbih actions | Values persist after leaving and returning. | NOT-CHECKED | Not checked. |

## Settings About `/settings/about`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | `window.prayerCompanion.navigate("/settings/about")` | About renders. | NOT-CHECKED | Not checked. |
| Language | Text and buttons | No mixed raw English/Arabic keys. | NOT-CHECKED | Not checked. |
| Buttons | Email, call, website, report | Buttons invoke expected action without anchor hover URL. | NOT-CHECKED | Not checked. |
| Inputs | n/a | n/a | WORKING GOOD | No inputs expected. |
| Status saved | n/a | n/a | WORKING GOOD | No state to save. |

## Onboarding `/onboarding`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | `window.prayerCompanion.navigate("/onboarding")` | Onboarding renders. | NOT-CHECKED | Not checked. |
| Language | Steps/buttons/location text | No mixed raw English/Arabic keys. | NOT-CHECKED | Not checked. |
| Buttons | Back/next/finish/language selection | Buttons update UI and complete state. | NOT-CHECKED | Not checked. |
| Inputs | Language/location controls | Values change. | NOT-CHECKED | Not checked. |
| Status saved | `onboarding.complete`, language/location settings | Values persist after leaving and returning. | NOT-CHECKED | Not checked. |
