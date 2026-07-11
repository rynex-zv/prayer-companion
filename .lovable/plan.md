## Calendar redesign — plan

Full rebuild of the Calendar page to match a real calendar app UX (Year / Month / Week / Day), with a Gregorian↔Hijri toggle, prayer times contextual to view, Islamic occasions from local JSON, and remembered last view.

### 1) Views (with segmented control at top)

Top bar: `[Year] [Month] [Week] [Day]` segmented control, `< today >` navigator, and a `Gregorian / Hijri` toggle pill.

- **Year**: 12 mini-month grid. Each mini shows the month name (in active calendar), tiny weekday header, day numbers with today highlighted, and colored dots for Islamic occasions on their days. Tap a mini → jump to Month view of that month.
- **Month**: 6×7 grid. Each cell shows:
  - Primary date number (large, centered)
  - Secondary date in the top corner (small, muted) — Hijri when Gregorian is active, Gregorian when Hijri is active
  - Small dot for occasions; today ring; selected filled
  - Tapping a day opens a **bottom sheet** with that day's full prayer times, Hijri↔Gregorian pair, and any occasion name.
- **Week**: 7 rows (or 7 columns on desktop) with each day expanded — date pair on the left, 6 prayer time rows on the right. Today highlighted. Swipe/arrow for prev/next week.
- **Day**: Single day, big Hijri + Gregorian header, occasion banner (if any), full prayer times list, offsets shown when non-zero. Prev/next day arrows.

### 2) Calendar toggle behavior

- Store `activeCalendar: "gregorian" | "hijri"` in settings.
- Header, month names, day numbers are in the active calendar.
- The **other** calendar is always visible as secondary (cell corner in Month/Year, subtitle in Week/Day).
- Toggle persists across sessions.

### 3) Remembered last view

- Store `lastCalendarView: "year" | "month" | "week" | "day"` in settings; restore on entry.

### 4) Islamic occasions (local JSON in Core)

New files under `PrayAdFree.Core/Resources/CalendarEvents/`:

- `base.c.event.json` — shared occasions (Ramadan 1, Eid al-Fitr, Arafah, Eid al-Adha, Muharram 1, Ashura 10, Mawlid, Isra & Mi'raj, Laylat al-Qadr window, etc.) keyed by Hijri month/day.
- `<madhhab>.c.event.json` — per-madhhab additions/overrides (naming follows existing calc-method / madhhab keys already in Core, so the resolver reuses that source of truth).

Schema (single entry):
```json
{
  "id": "eid_fitr",
  "hijriMonth": 10,
  "hijriDay": 1,
  "labelKey": "occasion_eid_fitr",
  "color": "primary",
  "importance": "major"
}
```

- New `IslamicOccasionCatalog` service in Core: loads `base` + selected-madhhab JSON, merges (madhhab overrides base by `id`), returns occasions for a given Hijri month/year.
- Label strings go through the existing i18n label pipeline — every `labelKey` gets entries in all language files.
- Exposed via RPC methods `calendar.getSnapshot` (extended) and `calendar.setView`, `calendar.setActiveCalendar`, `calendar.selectDate`.

### 5) Prayer times display rules

- Year view: not shown (dots only for occasions).
- Month view: shown in bottom sheet after tapping a day.
- Week view: inline under each day.
- Day view: full list, always visible.

### 6) Translations

Every new UI string (`year`, `month`, `week`, `day`, `gregorian`, `hijri`, `todayLabel`, occasion names, empty-state text, sheet titles) added to Core's label catalog and all shipped language files. React consumes them via `useAppLabels`.

### 7) Technical layout

- Core:
  - New models: `CalendarView` enum, `CalendarSelection`, `IslamicOccasion`, `CalendarCellSnapshot` (date pair + occasion + prayer summary flag).
  - New service: `IslamicOccasionCatalog` (JSON embedded as resources), `CalendarSnapshotFactory` (builds year/month/week/day payloads).
  - `WebCoreRpcDispatcher` gains: `calendar.setView`, `calendar.setActiveCalendar`, `calendar.selectDate`, extended `calendar.getSnapshot` returning `{ view, activeCalendar, selection, header, cells, prayerTimes?, occasions[] }`.
  - `AppSettings` gains `LastCalendarView`, `ActiveCalendar`.
- React (`Pray.web/src/routes/calendar.tsx`):
  - Rewrite as a view-switching component with subcomponents `YearGrid`, `MonthGrid`, `WeekList`, `DayDetail`, `DayBottomSheet`.
  - All data via existing `useSnapshot`/`mauiCall` — no business logic in React.
  - Islamic aesthetic consistent with the rest of the app (geometric ring accents, primary color for occasion dots, subtle gradient on today).

### 8) Order

1. Core models + JSON files + `IslamicOccasionCatalog` + settings fields.
2. `CalendarSnapshotFactory` + RPC methods + regenerate contract.
3. React: segmented control + Gregorian/Hijri toggle + Month view first (most-used).
4. Week view + Day bottom sheet.
5. Day view.
6. Year view + occasion dots.
7. Fill translations, run `build:all`, verify.

### Not in scope

- Fetching occasions from a network API (all local per your instruction).
- Editing/adding user custom events (can be a later feature).
- Changing prayer calculation logic — Calendar consumes existing `PrayerTimesService`.
