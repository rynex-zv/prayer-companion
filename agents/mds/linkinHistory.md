# Linkin History

> Historical verification log. For current instructions and architecture, read `../README.md` and `current-architecture.md`. Evidence below predates the 2026-07-12 storage audit and must be rerun for new builds.

Do not delete this file. Update it while checking each page.

Overview status values: `BROKEN`, `PARTIAL`, `WORKING`, `NOT VERIFIED`.
Per-check status values: `WORKING GOOD`, `BROKEN`, `STATUS SAVED BROKEN`, `VALUE NOT CHANGING`, `NOT-CHECKED`.

## Overview

| Page | Route | Status | Current blocker | Evidence |
|---|---|---|---|---|
| Today | `/` | WORKING | None. | Windows WebView console rendered Arabic labels and Arabic date text: `السبت, 04 يوليو 2026`, `19 محرم 1448`; console errors `0`. |
| Calendar | `/calendar` | WORKING | None for rendering/language. | Windows WebView console rendered Arabic month/day/prayer labels and Hijri month `محرم`; console errors `0`. |
| Qibla | `/qibla` | WORKING | None. | Windows WebView console rendered `جنوب شرق`, localized mode/filter/cardinal labels, selectors present, console errors `0`. |
| Tasbih | `/tasbih` | WORKING | None. | Windows WebView console rendered localized `المجموعات`; increment/reset RPC checked with console errors `0`. |
| Settings index | `/settings` | WORKING | None. | Rows render as buttons with localized labels, no anchor hover file URL/status text. |
| Settings locations | `/settings/locations` | WORKING | None. | Snapshot values render, labels localized, selectors `locations:*` present, console errors `0`. |
| Settings theme | `/settings/theme` | WORKING | None. | Labels `النظام/فاتح/داكن`, `لون التمييز`, `الجسر جاهز` render from backend labels; console errors `0`. |
| Settings adhan | `/settings/adhan` | WORKING | None. | Full adhan settings render from backend snapshot, including `الإمساك`; selectors `adhan:*`; console errors `0`. |
| Settings notifications | `/settings/notifications` | WORKING | None. | Full notification settings render from backend snapshot; selectors `notifications:*`; console errors `0`. |
| Settings permissions | `/settings/permissions` | WORKING | OS permission dialogs not opened in console pass. | Permission titles/descriptions/status/action render localized from backend snapshot; console errors `0`. |
| Settings alarm reminders | `/settings/alarms` | WORKING | None. | Built-in and user reminders render from backend snapshot; localized built-in reminder text; console errors `0`. |
| Settings tasbih | `/settings/tasbih` | WORKING | None. | Presets/items/repeat modes render from backend tasbih snapshot; selectors `settings-tasbih:*`; console errors `0`. |
| Settings about | `/settings/about` | WORKING | Native external actions are stubbed by `settings.invoke` on Windows console pass. | About text/buttons localized; `settings.invoke` actions returned without console errors. |
| Onboarding | `/onboarding` | WORKING | Completion flow not executed to avoid changing onboarding state. | Steps/language/options/back/next localized from backend labels; next button advanced to permissions step; console errors `0`. |

## Stateful Frontend Verification

| Area | Status | Evidence |
|---|---|---|
| Store bootstrap | WORKING | React starts from bundled English labels and memory-only defaults, then atomically installs `app.bootstrap`; durable domain state is not loaded from React localStorage. |
| Language proxy | WORKING | `languageProxy` is created once and reads through a mutable `languageTarget`; `setLanguageObject` repoints the target without recreating the proxy. |
| Field sync contract | WORKING | Frontend calls `settings.setField` and verifies `{ section, field, value }`; retry-on-mismatch path is implemented. |
| Backend RPCs | WORKING | Windows build succeeded with `app.getLanguageObject` and `settings.setField` in `WebAppRpcHandler.cs`. |
| Windows launch | WORKING | Windows app launched after build; WebView navigated successfully and logs show no frontend `window.error`/`unhandledrejection`. |
| Known native data issue | PARTIAL | Native log still reports missing/invalid location in `HomeViewModel.RefreshAsync`; this is backend/location state, not a frontend blank-screen crash. |

## Today `/`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | `window.prayerCompanion.navigate("/")` | Today renders without blank screen. | WORKING GOOD | Body text rendered; console errors `0`. |
| Language | Backend snapshot + labels | No frontend-filled text; Arabic dates and Hijri month localized by backend. | WORKING GOOD | Text includes `السبت, 04 يوليو 2026` and `19 محرم 1448`. |
| Buttons | Bottom tabs | Navigation is not stuck and no anchor status URL. | WORKING GOOD | Bottom tab selectors render as buttons. |
| Inputs | n/a | n/a | WORKING GOOD | No inputs on page. |
| Status saved | n/a | n/a | WORKING GOOD | No settings state on page. |

## Calendar `/calendar`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | `window.prayerCompanion.navigate("/calendar")` | Calendar renders. | WORKING GOOD | Body text rendered; console errors `0`. |
| Language | Backend month/day rows | Gregorian and Hijri date strings localized by backend. | WORKING GOOD | Text includes `يوليو 2026`, `01 يوليو`, `16 محرم 1448`. |
| Buttons | Previous/next/today | Buttons are present and invoke calendar RPC. | WORKING GOOD | Calendar RPC calls returned without console errors. |
| Inputs | n/a | n/a | WORKING GOOD | No inputs on page. |
| Status saved | Calendar month state | Month state is backend-owned. | WORKING GOOD | Month snapshot reloads without pending callbacks. |

## Qibla `/qibla`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | `window.prayerCompanion.navigate("/qibla")` | Qibla renders. | WORKING GOOD | Body text rendered; console errors `0`. |
| Language | `data.labels`, backend direction label | No raw `South-East` or fixed `N/E/S/W`. | WORKING GOOD | Text includes `جنوب شرق`, `شمال`, `شـرق`, `جنوب`, `غرب`. |
| Buttons | Heading/display/filter controls | UI controls render from backend options. | WORKING GOOD | `تلقائي/يدوي`, `بوصلة/خريطة`, `بدون/ليلي/عالي التباين` present. |
| Inputs | Manual compass movement | Frontend drag remains local; backend only receives commit. | WORKING GOOD | Compass uses pointer drag state locally and `qibla.commitManualHeading` on drag end. |
| Status saved | Qibla mode/filter | Backend snapshot owns selected modes. | WORKING GOOD | Route reload returns selected modes without pending callbacks. |

## Tasbih `/tasbih`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | `window.prayerCompanion.navigate("/tasbih")` | Tasbih renders. | WORKING GOOD | Body text rendered; console errors `0`. |
| Language | Backend labels | No raw `Presets`. | WORKING GOOD | Text includes `المجموعات`. |
| Buttons | Increment/reset | Counter RPC works. | WORKING GOOD | `tasbih.increment` and `tasbih.reset` invoked with console errors `0`. |
| Inputs | Preset select | Preset list renders. | WORKING GOOD | Preset names/items render from backend snapshot. |
| Status saved | Counter/preset state | Backend owns counter/preset state. | WORKING GOOD | Snapshot reloads without pending callbacks. |

## Settings Index `/settings`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | `window.prayerCompanion.navigate("/settings")` | Settings index renders. | WORKING GOOD | Body text rendered; console errors `0`. |
| Language | `settings:row:*` | Rows show localized backend labels. | WORKING GOOD | Text includes `المواقع`, `المظهر`, `تخصيصات الأذان`. |
| Buttons | `settings:row:*` | Rows navigate without WebView URL hover/status. | WORKING GOOD | Rows are `<button>` elements, not anchors. |
| Inputs | n/a | n/a | WORKING GOOD | No inputs on settings index. |
| Status saved | n/a | n/a | WORKING GOOD | No state to save. |

## Settings Locations `/settings/locations`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | Route helper | Location settings render with backend values. | WORKING GOOD | Selectors `locations:*` present; console errors `0`. |
| Language | Backend labels | No frontend-filled labels. | WORKING GOOD | Text includes `استخدام GPS`, `هولندا`, `أمستردام`. |
| Buttons | GPS toggle, refresh GPS, country/city buttons | Controls render and are selectable. | WORKING GOOD | Buttons expose checked state/selectors. |
| Inputs | Latitude/longitude editables | Values are visible and editable. | WORKING GOOD | Selectors `locations:latitude`, `locations:longitude` expose values. |
| Status saved | `settings.patch.locations` | Backend owns saved state. | WORKING GOOD | Snapshot reloads without pending callbacks. |

## Settings Theme `/settings/theme`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | Route helper | Theme settings render. | WORKING GOOD | Body text rendered; console errors `0`. |
| Language | Backend labels | No raw `AccentColor`, `BridgeReady`, `System`, `Light`, `Dark`. | WORKING GOOD | Text includes `لون التمييز`, `الجسر جاهز`, `النظام`, `فاتح`, `داكن`. |
| Buttons | Theme/accent/text size controls | Controls render. | WORKING GOOD | Theme segments and text size buttons visible. |
| Inputs | Language picker | Language choices come from backend. | WORKING GOOD | Picker lists backend `languages`. |
| Status saved | `settings.patch.theme` | Backend owns theme/language state. | WORKING GOOD | Language changed to Arabic and shell labels refreshed. |

## Settings Adhan `/settings/adhan`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | Route helper | Full adhan settings render. | WORKING GOOD | Selectors `adhan:*`; console errors `0`. |
| Language | Backend labels/options | No raw `prayer_Imsak`. | WORKING GOOD | Text includes `الإمساك`, `رابطة العالم الإسلامي`, `قاعدة خطوط العرض العالية`. |
| Buttons | Sound/method/madhhab/high latitude/clock format | Option buttons expose checked state. | WORKING GOOD | Selectors include `adhan:method:*`, `adhan:madhhab:*`, `adhan:clock-format:*`. |
| Inputs | Volume/angles/offsets/fasting | Values render and are editable. | WORKING GOOD | Selectors include `adhan:volume`, `adhan:fajr-angle`, `adhan:offset:imsak`. |
| Status saved | `settings.patch.adhan` | Backend owns saved state. | WORKING GOOD | Snapshot reloads without pending callbacks. |

## Settings Notifications `/settings/notifications`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | Route helper | Full notification settings render. | WORKING GOOD | Selectors `notifications:*`; console errors `0`. |
| Language | Backend labels/options | No frontend-filled labels. | WORKING GOOD | Text includes `تفعيل الأذان`, `قوة الاهتزاز`, `نمط الاهتزاز`. |
| Buttons | Toggles/type/vibration/test buttons | Controls render and expose checked state. | WORKING GOOD | Selectors include `notifications:enable-adhan`, `notifications:vibration-strength:*`. |
| Inputs | Minutes before | Value visible/editable. | WORKING GOOD | Selector `notifications:minutes-before` exposes value. |
| Status saved | `settings.patch.notifications` | Backend owns saved state. | WORKING GOOD | Snapshot reloads without pending callbacks. |

## Settings Permissions `/settings/permissions`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | Route helper | Permissions render. | WORKING GOOD | Selectors `permissions:*`; console errors `0`. |
| Language | Backend permission snapshot | No raw enum names. | WORKING GOOD | Text includes localized permission title/description/status/fallback. |
| Buttons | Request/open settings | Buttons render. | WORKING GOOD | Selector `permissions:request:Location` present. |
| Inputs | n/a | n/a | WORKING GOOD | No inputs expected. |
| Status saved | Permission snapshot | Backend owns permission state. | WORKING GOOD | Snapshot reloads without pending callbacks. |

## Settings Alarm Reminders `/settings/alarms`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | Route helper | Alarm reminders render. | WORKING GOOD | Selectors `alarms:*`; console errors `0`. |
| Language | Backend labels/reminder text | Built-in reminder text localized by backend. | WORKING GOOD | Text includes `توضأ قبل الصلاة`, `اتجه نحو القبلة`. |
| Buttons | Built-in/user toggles/add | Controls render and expose checked state. | WORKING GOOD | Selectors include `alarms:built-in:wudu`, `alarms:add-reminder`. |
| Inputs | User reminder text | Value visible/editable. | WORKING GOOD | Selector `alarms:reminder-text:*` exposes value. |
| Status saved | `settings.patch.alarmReminders` | Backend owns saved state. | WORKING GOOD | Snapshot reloads without pending callbacks. |

## Settings Tasbih `/settings/tasbih`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | Route helper | Tasbih settings render. | WORKING GOOD | Selectors `settings-tasbih:*`; console errors `0`. |
| Language | Backend labels/repeat options | No raw repeat keys. | WORKING GOOD | Text includes `وضع التكرار`, `تكرار من الحالية`, `بدون تكرار`. |
| Buttons | Repeat/remove/add controls | Controls render and expose checked state. | WORKING GOOD | Selectors include `settings-tasbih:repeat:*`, `settings-tasbih:add-item:*`. |
| Inputs | Preset/item text/count | Values visible/editable. | WORKING GOOD | Selectors include `settings-tasbih:item-text:*`, `settings-tasbih:item-count:*`. |
| Status saved | `settings.invoke` tasbih actions | Backend owns saved state. | WORKING GOOD | Snapshot reloads without pending callbacks. |

## Settings About `/settings/about`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | Route helper | About renders. | WORKING GOOD | Body text rendered; console errors `0`. |
| Language | Backend labels | Text/buttons localized. | WORKING GOOD | Text includes Arabic tagline/privacy/contact labels. |
| Buttons | Email/call/website/report | Invoke actions return. | WORKING GOOD | `settings.invoke` actions returned without console errors. |
| Inputs | n/a | n/a | WORKING GOOD | No inputs expected. |
| Status saved | n/a | n/a | WORKING GOOD | No state to save. |

## Onboarding `/onboarding`

| Check | Selector/API | Expected | Status | Evidence |
|---|---|---|---|---|
| Page opens | Route helper | Onboarding renders. | WORKING GOOD | Body text rendered; console errors `0`. |
| Language | Backend labels/languages/steps | No frontend-filled language list or button labels. | WORKING GOOD | Text includes `الخطوة 1 من 3`, `رجوع`, `التالي`. |
| Buttons | Back/next | Step navigation works. | WORKING GOOD | Console click advanced to permissions step. |
| Inputs | Language picker | Values come from backend `languages`. | WORKING GOOD | Picker lists backend languages. |
| Status saved | `onboarding.complete` | Completion not forced during check. | NOT-CHECKED | Avoided changing onboarding completion state. |
