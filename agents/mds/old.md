# Old XAML View Inventory

This file records what the native XAML app exposes: visible values, inputs, and layout structure.

## App Boot And Tabs

The XAML shell boots into a five-tab `TabBar`: Today, Calendar, Qibla, Tasbih, Settings. Each tab uses a native XAML page.

## Today

Layout: full-screen vertical gradient, refreshable scroll, centered basmala, location/date glass card, next-prayer hero card, prayer-times card, fasting card.

Visible values:
- Basmala text.
- Location title.
- Gregorian date and Hijri date.
- Next prayer label, prayer name, current adjusted time, optional base time, countdown, day label.
- Today prayer rows: Fajr, Sunrise, Dhuhr, Asr, Maghrib, Isha, each with time and optional base time.
- Fasting card: Iftar time, Imsak time, next fasting countdown.
- Optional status message.

Inputs/actions:
- Pull-to-refresh.

## Calendar

Layout: gradient page, centered title, controls card, vertical day card list.

Visible values:
- Selected month in `DatePicker`.
- Status message.
- Day card date and Hijri date.
- 3x2 prayer time grid: Fajr, Sunrise, Dhuhr, Asr, Maghrib, Isha.

Inputs/actions:
- Previous month button `<`.
- Month `DatePicker`.
- Next month button `>`.
- Today button.
- Load button.

## Qibla

Layout: gradient page, centered content width on desktop, direction hero, options card, compass card, optional map card.

Visible values:
- Qibla direction label.
- Bearing in degrees.
- Direction label.
- Status message.
- Location label in map mode.

Inputs/actions:
- Heading mode chips: Auto, Manual.
- Display mode chips: Compass, Map.
- Visual filter chips: None, Night, Contrast.
- Compass pan gesture for manual heading.
- Native/WebView map display when map mode is selected.

## Tasbih

Layout: gradient page, counter card, presets card.

Visible values:
- Current phrase.
- Large circular count.
- Progress text.
- Preset picker selected item.
- Preset item rows: target count and text.

Inputs/actions:
- Increment button.
- Reset button.
- Preset picker, disabled when preset cannot change.

## Settings Index

Layout: gradient page, centered title, three grouped glass cards with rows.

Rows:
- Locations: title `Locations`, subtitle `QiblaPreferences`.
- Theme/Diagnostics: title `ThemeMode`, subtitle `ThemeColor`.
- Adhan: title `AdhanCustomizations`, subtitle `AdhanSound`.
- Notifications: title `Notifications`, subtitle `AdhanReminders`.
- Permissions: title `PermissionsTitle`, subtitle `PermissionsSubtitle`.
- Alarm reminders: title `AlarmRemindersTitle`, subtitle `AlarmRemindersBuiltIn`.
- Tasbih: title `TasbihSettings`, subtitle `TasbihPresets`.
- About: title `About`, subtitle `SupportAndFeedback`.

Inputs/actions:
- Each row is tap navigation with icon and chevron.

## Settings Locations

Layout: one card containing shared location setup and Qibla preferences.

Inputs/actions:
- Use GPS switch, disabled when GPS unavailable.
- Update GPS button, disabled when GPS unavailable.
- Country picker.
- City picker.
- Latitude entry.
- Longitude entry.
- Compass reading mode chips.
- Compass filter chips.

## Settings Theme/Diagnostics

Layout: card with choice chips/cards.

Inputs/actions:
- Theme mode chips from `ThemeModes`.
- Accent color chips from `AccentOptions`, each with swatch and label.
- Text size minus button, current text scale label, plus button.
- Language cards from `Languages`, radio-dot selection.

## Settings Adhan

Layout: long scroll card with sound, calculation, offsets, fasting reminders, and per-prayer overrides.

Inputs/actions:
- Add custom adhan sound button.
- Test notification button.
- Sound list with select, play/stop preview, remove custom.
- Volume slider and percent label.
- Method picker.
- Madhhab picker.
- High latitude picker.
- Fajr angle entry.
- Isha angle entry.
- Offset entries: Fajr, Sunrise, Dhuhr, Asr, Maghrib, Isha, Imsak.
- Clock format picker.
- Fasting entries: Iftar delay, Imsak advance.
- Imsak reminder editor: value entry, unit picker, direction picker, add button, reminder list with remove.
- Iftar reminder editor: value entry, unit picker, direction picker, add button, reminder list with remove.
- Per-prayer override rows: prayer name, sound picker, vibration picker.

## Settings Notifications

Layout: one card with toggles, pickers, test actions, and reminder editor.

Inputs/actions:
- Enable adhan switch.
- Primary adhan type picker.
- Windows background options card when supported:
  - Hide on close switch.
  - Start minimized with Windows/background service switch.
  - Background service hint text.
- Test notification button.
- Test alarm button.
- Vibration switch.
- Vibration strength picker.
- Vibration pattern picker.
- Minutes before entry.
- Adhan reminder editor:
  - Scope picker.
  - Prayer picker, disabled when scope does not need prayer.
  - Reminder value entry.
  - Reminder unit picker.
  - Reminder direction picker.
  - Reminder alert type picker.
  - Add button.
  - Existing reminders list with alert type picker and remove button.

## Settings Permissions

Layout: card with alarm mode summary and permission item list.

Visible values/actions:
- Alarm mode title, status, description.
- Permission cards: title, role badge, description, fallback text, status text, action button.

## Settings Alarm Reminders

Layout: one card with built-in reminders and user reminders.

Inputs/actions:
- Built-in reminder list with enable/disable button per item.
- New user reminder entry and add button.
- User reminder rows with edit, enable/disable, remove.

## Settings Tasbih

Layout: one card with preset and item editor.

Inputs/actions:
- New preset name entry and add preset button.
- Preset picker.
- Selected preset name entry.
- Repeat mode picker.
- New item text entry.
- New item count entry.
- Add item button.
- Existing item rows with text entry, count entry, start index label, move up, move down, remove.

## Settings About

Layout: gradient page with app identity card, info card, contact card.

Visible values/actions:
- App icon, app name, tagline.
- Privacy text, source text.
- Maintainer, contact text.
- Email: `rynex@rynex.nl`.
- Phone: `+31610331734`.
- Website.
- Buttons: Email Rynex, Call +31 6 10331734, Open website, Report issue.

## Onboarding

Layout: gradient page with header, step card, bottom nav.

Inputs/actions:
- Step counter text.
- Language step: 2-column language collection selection.
- Permission step: current permission description, fallback, status, action.
- Location step: location permission card, optional manual location hint, VPN warning, shared location setup view.
- Back button shown when available.
- Primary next/finish button.
