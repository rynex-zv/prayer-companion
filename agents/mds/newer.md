# React Change Tasks From XAML Comparison

Status values: `TODO`, `IN PROGRESS`, `DONE`, `BLOCKED`.

## Boot Visibility

| ID | Task | Status | Evidence |
|---|---|---|---|
| BOOT-1 | Use XAML boot only while comparing old native pages. | DONE | `old.md` was created from XAML inventory. |
| BOOT-2 | Switch boot back to React/WebView after React tasks are applied so changes are visible when the app runs. | DONE | `AppShell.xaml` now loads `pages:TodayWebPage`. |

## High Priority Tasks

| ID | Page | Task | Status | Evidence |
|---|---|---|---|---|
| CAL-1 | Calendar | Add status message display. | DONE | `calendar.tsx` renders `data.statusMessage`. |
| CAL-2 | Calendar | Add Load button. | DONE | `calendar:load` calls `calendar.setMonth` with `selectedMonthValue`. |
| CAL-3 | Calendar | Add month input equivalent to XAML `DatePicker`. | DONE | `calendar.tsx` uses `type="month"` and backend returns `selectedMonthValue`. |
| LOC-1 | Settings Locations | Add Qibla preferences section. | DONE | `settings.locations.tsx` renders `qiblaPreferences` section. |
| LOC-2 | Settings Locations | Add compass reading mode controls bound to backend snapshot. | DONE | `BuildLocationsSettings` returns `qiblaReadingMode/qiblaReadingModes`; `PatchQibla` saves. |
| LOC-3 | Settings Locations | Add compass filter mode controls bound to backend snapshot. | DONE | `BuildLocationsSettings` returns `qiblaFilterMode/qiblaFilterModes`; `PatchQibla` saves. |
| ADH-1 | Settings Adhan | Add custom adhan sound button. | DONE | `adhan:add-custom-sound` button calls `settings.invoke`. |
| ADH-2 | Settings Adhan | Add sound select/preview/remove controls per sound. | DONE | Sound rows expose select/play/remove controls; selection persists through `sounds`. |
| ADH-3 | Settings Adhan | Add Imsak reminder editor/list. | DONE | `ReminderEditor` writes `imsakReminders`; backend `ReadReminderMinutes` persists value/unit/direction. |
| ADH-4 | Settings Adhan | Add Iftar reminder editor/list. | DONE | `ReminderEditor` writes `iftarReminders`; backend `ReadReminderMinutes` persists value/unit/direction. |
| ADH-5 | Settings Adhan | Add per-prayer sound/vibration override controls. | DONE | `perPrayerOverrides` UI writes sound/vibration; backend parses default/none/enabled correctly. |
| NOT-1 | Settings Notifications | Add Windows background hint text. | DONE | `settings.notifications.tsx` renders `windowsBackgroundServiceHint`. |
| NOT-2 | Settings Notifications | Add adhan reminder editor/list with scope, prayer, value, unit, direction, alert type, add, edit, remove. | DONE | UI edits `reminders`; backend maps reminders through `ReadAdhanReminderItems`. |
| ALM-1 | Settings Alarm Reminders | Add separate new reminder text input. | DONE | `alarms:new-reminder-text` controls the add flow. |
| ALM-2 | Settings Alarm Reminders | Add remove button for user reminders. | DONE | `alarms:remove:*` removes user reminders. |
| TAS-1 | Settings Tasbih | Add selected preset picker. | DONE | `settings.tasbih.tsx` uses `Picker` and `tasbih.selectPreset`. |
| TAS-2 | Settings Tasbih | Add new preset name input. | DONE | `settings-tasbih:new-preset-name` feeds `addTasbihPreset`. |
| TAS-3 | Settings Tasbih | Add new item text/count inputs. | DONE | `settings-tasbih:new-item-text:*` and `new-item-count:*` feed `addTasbihItem`. |
| TAS-4 | Settings Tasbih | Add item start index value. | DONE | Item cards show `startIndex` with LTR numeric value. |
| TAS-5 | Settings Tasbih | Add move up/down controls. | DONE | Move controls call backend `moveTasbihItem`. |
| ABT-1 | Settings About | Change contact email to `rynex@rynex.nl`. | DONE | `settings.about.tsx` uses `rynex@rynex.nl`. |
| ABT-2 | Settings About | Change phone to `+31610331734`. | DONE | `settings.about.tsx` uses `+31610331734`. |
| ABT-3 | Settings About | Use XAML-equivalent button labels. | DONE | Buttons read `Email Rynex`, `Call +31 6 10331734`, `Open website`, and localized report label. |
| ONB-1 | Onboarding | Replace language picker with grid/card language selection. | DONE | `onboarding.tsx` renders language buttons. |
| ONB-2 | Onboarding | Add current permission card details when backend provides permission data. | DONE | `onboarding.tsx` renders permission detail cards when `permissions` has items. |
| ONB-3 | Onboarding | Add manual location setup equivalent on location step when backend exposes data. | DONE | `onboarding.tsx` renders GPS, country/city, latitude, and longitude controls when `location` is present. |

## Medium Priority Tasks

| ID | Page | Task | Status | Evidence |
|---|---|---|---|---|
| LAY-1 | Settings Index | Group rows like XAML: Location/Theme, Adhan/Notifications/Permissions/Alarms, Tasbih/About. | DONE | `settings.tsx` renders three grouped cards. |
| RTL-1 | All React | Preserve numeric/time values as LTR. | DONE | Added/kept `dir="ltr"` on month/numeric/tasbih/location numeric controls touched in this pass. |
| RTL-2 | All React | Keep RTL mirroring from frontend store. | DONE | Settings index and onboarding consume `direction` from `useAppStore`; row icons mirror in RTL. |
