# Lovable Prompt: Pray Ad Free React Frontend

## Product Summary
Build a React/Vite frontend for **Pray Ad Free**, a prayer-time app that runs on web, Android, iOS, desktop, and inside a .NET MAUI WebView.

The app shows:
- Today prayer times and countdowns.
- Monthly prayer calendar.
- Qibla direction with compass/map modes.
- Tasbih counter with presets.
- Settings for location, calculation method, adhan, notifications, permissions, alarm reminders, theme, language, and about/contact.
- First-run onboarding for language, permissions, and location.

Important: this is a frontend-only UI. Do not implement prayer calculations, GPS logic, notification scheduling, adhan playback, permission handling, file picking, or settings persistence in JavaScript. Those stay in the existing C# MAUI app. React only displays snapshots and sends user actions through the native bridge.

## Build Targets / Modes
The frontend must support multiple build/use modes:

1. **Browser Preview / Lovable Preview**
   - Runs in a normal browser.
   - Uses mock data only.
   - No native bridge required.

2. **Phone Build**
   - Built with a `--phone` flag or equivalent mode.
   - Optimized for Android/iPhone WebView.
   - Uses compact spacing, safe-area padding, bottom navigation, and fast startup.
   - This is the version copied into MAUI embedded assets and also published to the remote update website.

3. **Remote Web Build**
   - Served as static files from `https://pray.rynex.nl/`.
   - Same app/design as phone where possible.
   - Must be usable in a browser as a web app, but inside MAUI it still talks to C# through `window.mauiWebber.call`.

4. **Embedded Fallback Build**
   - Same static build copied into MAUI resources.
   - Must work offline.
   - Must open immediately while MAUI checks for remote updates in the background.

Do not create separate unrelated apps for these modes. Use one React codebase with build flags/config.

## Mock Data Rule
Put all mock/demo data in one folder:

```txt
src/mock/
  index.ts
  countries.ts
  today.ts
  calendar.ts
  qibla.ts
  tasbih.ts
  settings.ts
  onboarding.ts
```

At the top of the app, expose a single test configuration object that can be edited manually:

```ts
export const TEST = {
  enabled: !window.mauiWebber,
  country: "NL",
  city: "Amsterdam",
  language: "ar",
  theme: "light",
  clockFormat: "12h",
  qiblaState: "aligned", // aligned | searching | noPermission | manual | map
  permissionsScenario: "partial", // allGranted | partial | missingCritical
}
```

All browser mock data must come from this folder and this `TEST` object. Do not scatter mock objects inside page components.

When running inside MAUI and `window.mauiWebber` exists, ignore mock data and use native snapshots.

## Native Bridge Contract
Use one wrapper around the MAUI bridge:

```ts
const response = await mauiWebber.call(method, payload)
// success: { ok: true, data: ... }
// failure: { ok: false, error: "..." }
```

The wrapper should:
- Use `window.mauiWebber.call` when available.
- Use mock handlers from `src/mock/` when not available.
- Never throw directly into UI components; return a clean error state.

Suggested file:

```txt
src/native/mauiWebberClient.ts
```

## Translation / Text Rule
The source of truth for text is C# localization.

Inside MAUI:
- Every snapshot should include `labels` or localized display values.
- React must display labels from the snapshot.
- React must not hardcode final production text except fallback/demo text.

In browser/mock mode:
- Use mock translation dictionaries in `src/mock/translations/`.
- The selected language comes from `TEST.language`.

Use this pattern:

```ts
const label = snapshot.labels?.nextPrayer ?? t("nextPrayer")
```

Text direction:
- Use `snapshot.isRtl` or mock language.
- Set root `dir="rtl"` for Arabic.
- Handle mixed Arabic + AM/PM times correctly. Use bidi isolation for time fragments when needed:

```html
<span dir="ltr">6:47 PM</span>
```

Do not concatenate Arabic labels and English AM/PM text without direction handling.

## Design Direction
Design should feel like a real utility app, not a landing page.

Use:
- Mobile-first responsive layout.
- Soft sky/sand background.
- Green/teal primary actions.
- White/glass cards.
- Clear card hierarchy.
- Bottom navigation with five tabs.
- Arabic-friendly typography.
- Compact controls for settings.
- Icons for navigation and action buttons.

Avoid:
- Marketing hero page.
- Huge decorative sections.
- Random gradients/orbs.
- Putting UI cards inside other cards.
- Reimplementing native/backend logic in JavaScript.

## Navigation
Main tabs:

1. Today
2. Calendar
3. Qibla
4. Tasbih
5. Settings

Settings sub-pages:
- Locations
- Theme / Diagnostics
- Adhan
- Notifications
- Permissions
- Alarm Reminders
- Tasbih Settings
- About

The app should support both:
- React-owned tabs and routes.
- MAUI Shell-owned tabs during migration.

## Global RPC Methods
Suggested methods:

```ts
app.getShellSnapshot
app.navigate
app.getLocalization
app.setLanguage
app.setTheme
```

`app.getShellSnapshot` returns:
- current route.
- language.
- `isRtl`.
- theme mode.
- accent color.
- tab labels/icons.
- global labels.
- onboarding status.

## Today Page
RPC:

```ts
today.getSnapshot
today.refresh
```

Display:
- Basmala.
- Location card: city/country, Gregorian date, Hijri date.
- Next prayer hero card:
  - prayer name.
  - prayer time.
  - optional base time.
  - countdown.
  - day label.
- Today timings list:
  - Fajr, Sunrise, Dhuhr, Asr, Maghrib, Isha.
  - highlight next prayer with dot/active state.
- Imsak and Iftar cards.
- Small refresh button or pull-to-refresh.
- Status message.

Data shape:

```ts
type TodaySnapshot = {
  locationTitle: string
  hijriDate: string
  gregorianDate: string
  nextPrayerName: string
  nextPrayerClock: string
  nextPrayerBaseClock: string
  showNextPrayerBaseClock: boolean
  nextPrayerDayLabel: string
  countdown: string
  statusMessage: string
  imsakTime: string
  iftarTime: string
  isImsakNext: boolean
  isIftarNext: boolean
  nextFastingCountdown: string
  isRtl: boolean
  labels: Record<string, string>
  todayTimings: {
    id: string
    name: string
    time: string
    baseTime?: string
    showBaseTime?: boolean
    isNext: boolean
  }[]
}
```

Startup behavior:
- Render cached snapshot immediately.
- Refresh in the background.
- Show performance trace hooks for bridge ready, first snapshot, and render complete.

## Calendar Page
RPC:

```ts
calendar.getSnapshot
calendar.setMonth
calendar.today
calendar.nextMonth
calendar.previousMonth
```

Display:
- Page title.
- Month control card:
  - previous month.
  - month/year picker.
  - next month.
  - Today.
  - Load/refresh if needed.
  - status message.
- List of day cards.
- Each day card:
  - Gregorian date.
  - Hijri date.
  - Prayer grid 3x2: Fajr, Sunrise, Dhuhr, Asr, Maghrib, Isha.

Data shape:

```ts
type CalendarSnapshot = {
  selectedMonth: string
  statusMessage: string
  days: {
    date: string
    hijri: string
    fajr: string
    sunrise: string
    dhuhr: string
    asr: string
    maghrib: string
    isha: string
    isToday?: boolean
  }[]
}
```

## Qibla Page
RPC:

```ts
qibla.getSnapshot
qibla.setHeadingMode
qibla.adjustManualHeading
qibla.commitManualHeading
qibla.setDisplayMode
qibla.setVisualFilter
```

Display:
- Top status card:
  - Qibla Direction label.
  - bearing degree, large.
  - direction label.
  - status message.
- Segmented controls:
  - Heading mode: Auto/Sensor, Manual.
  - Display mode: Compass, Map.
  - Visual filter: None, Night, Contrast.
- Compass card.
- Optional map card.

Compass shape:
- Circular compass face.
- Outer ring with degree ticks.
- Cardinal directions N/E/S/W or localized equivalents.
- Inner Kaaba/Qibla marker or arrow.
- Needle/arrow points to Qibla.
- Phone heading rotates the compass face; Qibla arrow remains visually understandable.
- Use `needleRotation` for the Qibla needle and `compassRotation` for the dial.

Compass states:

1. **Searching**
   - No stable heading/location yet.
   - Show muted compass and status text.

2. **Permission Missing**
   - Location/compass permission missing.
   - Show action button text from C# snapshot.
   - Do not fake permission.

3. **Sensor / Auto**
   - Compass follows device heading.
   - Manual drag disabled.

4. **Manual**
   - User can drag horizontally to adjust heading.
   - On drag: call `qibla.adjustManualHeading({ delta })`.
   - On drag end: call `qibla.commitManualHeading()`.

5. **Aligned**
   - If heading is close to Qibla, visually highlight the arrow/ring.
   - Use C# snapshot if it provides `isAligned`; otherwise frontend may derive only for display, not for logic.

6. **Map**
   - Show map panel or static placeholder.
   - Draw user location and Qibla line if data is available.

7. **Visual filters**
   - None: normal colors.
   - Night: darker muted compass.
   - Contrast: high contrast, clear ring/needle.

Data shape:

```ts
type QiblaSnapshot = {
  bearing: number
  heading: number
  needleRotation: number
  compassRotation: number
  directionLabel: string
  locationTitle: string
  statusMessage: string
  selectedHeadingMode: string
  selectedReadingMode: string
  selectedFilterMode: string
  displayMode: "Compass" | "Map"
  visualFilter: "None" | "Night" | "Contrast"
  state: "searching" | "permissionMissing" | "sensor" | "manual" | "aligned" | "map"
  isAligned?: boolean
  headingModes: Option[]
  readingModes: Option[]
  filterModes: Option[]
}
```

## Tasbih Page
RPC:

```ts
tasbih.getSnapshot
tasbih.increment
tasbih.reset
tasbih.selectPreset
```

Display:
- Current phrase.
- Large circular count.
- Progress text.
- Large increment button.
- Smaller reset button.
- Preset picker.
- Preset item list with target counts.
- Disable preset selection while count > 0.

Data shape:

```ts
type TasbihSnapshot = {
  count: number
  currentPhrase: string
  progressText: string
  isPresetSelectionEnabled: boolean
  selectedPresetId: string
  presets: {
    id: string
    name: string
    repeatMode: string
    items: { text: string; targetCount: number }[]
  }[]
}
```

## Settings Index Page
Display grouped navigation rows:

- Locations: location and GPS.
- Theme: theme mode and accent color.
- Adhan Customizations: adhan sound and calculation settings.
- Notifications: adhan reminders and vibration.
- Permissions: system permissions.
- Alarm Reminders: alarm-screen reminders.
- Tasbih: tasbih presets.
- About: app/contact info.

Each row has icon, title, subtitle, chevron, and opens a sub-page.

## Settings / Locations
RPC:

```ts
settings.getSnapshot
settings.patch
settings.invoke
```

Display:
- Use GPS switch.
- Refresh GPS button.
- Country picker.
- City picker.
- Country text field.
- City text field.
- Latitude field.
- Longitude field.
- VPN warning if C# sends it.

Rules:
- GPS has highest priority if allowed.
- Manual user entry has second priority.
- Internet/IP autofill has third priority.
- If user edits manual fields, do not override them with IP/GPS autofill unless user explicitly enables GPS.
- React does not detect VPN or GPS itself.

## Settings / Theme and Diagnostics
Display:
- Language picker/list.
- Theme mode: System, Light, Dark.
- Accent color swatches.
- Text size control with - / + and percentage.
- Diagnostics/status/logs if C# provides them.
- Save/load/report actions if C# provides them.

## Settings / Adhan
Display:
- Adhan sound list:
  - label.
  - selected state.
  - preview play/stop.
  - remove button for custom sounds.
- Add custom adhan sound button.
- Test notification button.
- Volume slider and label.
- Calculation:
  - method picker.
  - madhhab picker.
  - high latitude rule picker.
  - Fajr angle and Isha angle.
  - custom angles are editable only when method is Custom.
- Offsets:
  - Fajr, Sunrise, Dhuhr, Asr, Maghrib, Isha, Imsak.
- Clock format picker.
- Fasting:
  - Iftar delay.
  - Imsak advance.
- Imsak reminders:
  - value, unit, direction, add.
  - list with remove.
- Iftar reminders:
  - value, unit, direction, add.
  - list with remove.
- Per-prayer adhan overrides:
  - prayer name.
  - sound picker.
  - vibration picker.

Native actions:
- Add custom sound.
- Remove custom sound.
- Preview sound.
- Stop preview.
- Test notification.

## Settings / Notifications
Display:
- Enable adhan switch.
- Mobile primary adhan type picker.
- Hide on close on Windows switch.
- Run background service on Windows switch.
- Test notification button.
- Test alarm button.
- Vibration switch.
- Vibration strength picker.
- Vibration pattern picker.
- Minutes before input.
- Adhan reminders:
  - scope picker.
  - prayer picker if scope is specific.
  - value, unit, direction, alert type.
  - add/remove list.

## Settings / Permissions
Display:
- Alarm mode card:
  - title.
  - status.
  - description.
- Permission cards:
  - title.
  - critical/optional role.
  - description.
  - fallback text.
  - status text.
  - action button.

Permission action must call C#. C# opens system settings or permission request flow.

## Settings / Alarm Reminders
Display:
- Built-in reminder list:
  - text.
  - enabled/disabled toggle.
- User reminder section:
  - add input.
  - user reminders list.
  - enabled toggle.
  - edit.
  - remove.

## Settings / Tasbih
Display:
- New preset name input + add button.
- Preset picker.
- Editable preset name.
- Repeat mode picker.
- Add item form:
  - text.
  - target count.
  - add.
- Items list:
  - editable text.
  - editable target count.
  - start index.
  - move up.
  - move down.
  - remove.

## About Page
Display:
- App name.
- Tagline.
- Privacy text.
- Source text.
- Maintainer.
- Contact.
- Email.
- Phone.
- Website.
- Website note.
- Buttons:
  - Email.
  - Call.
  - Open website.
  - Report issue.

These buttons call C# actions.

## Onboarding / Welcome Flow
Show only if C# says onboarding is not completed.

Steps:
- Language.
- Permissions.
- Location.

Location rules:
- No internet and no GPS: show manual location setup.
- Internet available but no GPS/manual: C# fills IP-based location on first page.
- If user changed manual fields, do not override them.
- If GPS is allowed, location comes from GPS first.
- Priority: GPS, user manual, internet/IP.
- If C# detects VPN and user has no GPS/manual location, show VPN warning.

React only displays state and sends actions.

## Suggested React Structure

```txt
src/
  app/
    App.tsx
    routes.ts
    TEST.ts
  native/
    mauiWebberClient.ts
  mock/
    index.ts
    countries.ts
    today.ts
    calendar.ts
    qibla.ts
    tasbih.ts
    settings.ts
    onboarding.ts
    translations/
      en.ts
      ar.ts
      fr.ts
      es.ts
      tr.ts
  pages/
    TodayPage.tsx
    CalendarPage.tsx
    QiblaPage.tsx
    TasbihPage.tsx
    SettingsPage.tsx
    settings/
      LocationsPage.tsx
      ThemeDiagnosticsPage.tsx
      AdhanPage.tsx
      NotificationsPage.tsx
      PermissionsPage.tsx
      AlarmRemindersPage.tsx
      TasbihSettingsPage.tsx
      AboutPage.tsx
    OnboardingPage.tsx
  components/
    AppShell.tsx
    BottomTabs.tsx
    Card.tsx
    Field.tsx
    Picker.tsx
    SegmentedControl.tsx
    Toggle.tsx
    QiblaCompass.tsx
  styles/
    tokens.css
    app.css
```

## Acceptance Criteria
- Browser preview works with mock data from `src/mock/` only.
- `TEST.country` and `TEST.language` can change all mock page data.
- Phone build works with `--phone`.
- Inside MAUI, data comes from `window.mauiWebber.call`.
- React does not calculate prayer times, GPS, permissions, or notifications.
- All production text comes from C# snapshots/localization.
- RTL works, including Arabic mixed with AM/PM.
- Qibla compass has clear states and correct visual behavior.
- Today renders cached data immediately, then refreshes.
- UI is responsive for phone, desktop, and web.
