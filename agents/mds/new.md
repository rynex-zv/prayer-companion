# Current React View Inventory

This file records what the current React/MauiWebber surface exposes.

## App Shell

React shell has routes for Today, Calendar, Qibla, Tasbih, Settings index, eight settings subpages, and Onboarding. Bottom tabs are rendered as buttons and mirror in RTL.

## Today

Layout: stacked cards, basmala row, location/date card, next-prayer hero, timings card, fasting two-card grid.

Visible values:
- Basmala, location title, Gregorian/Hijri date.
- Next prayer name, time, optional base time, countdown, day label.
- Today timings: Fajr, Sunrise, Dhuhr, Asr, Maghrib, Isha.
- Imsak and Iftar times.
- Optional status message.

Inputs/actions:
- Refresh icon button.

## Calendar

Layout: title row, month nav card, Today button, vertical day card list.

Visible values:
- Selected month text.
- Day date, Hijri date, today badge.
- 3x2 prayer grid.

Inputs/actions:
- Previous month button.
- Next month button.
- Today button.

Missing compared with XAML:
- Month `DatePicker`.
- Load button.
- Status message display.

## Qibla

Layout: direction card, segmented control rows, compass or map card.

Visible values:
- Bearing, direction label, location title, status message.
- Permission missing card when needed.

Inputs/actions:
- Heading mode segmented control.
- Reading/display mode segmented control.
- Filter segmented control.
- Compass drag commits manual heading.
- Map mode.

## Tasbih

Layout: phrase card, large circular count button, reset button, presets card.

Visible values:
- Current phrase, progress, count, preset picker, preset item list.

Inputs/actions:
- Increment by tapping count circle.
- Reset.
- Preset picker.

## Settings Index

Layout: title row and a single list card with rows for Locations, Theme/Diagnostics, Adhan, Notifications, Permissions, Alarm reminders, Tasbih, About.

Inputs/actions:
- Row buttons navigate.

## Settings Locations

Layout: status line, optional VPN warning, location/GPS section, location section.

Inputs/actions:
- Use GPS toggle.
- Refresh GPS button.
- Country option buttons.
- City option buttons.
- Latitude editable value.
- Longitude editable value.

Missing compared with XAML:
- Compass reading mode chips.
- Compass filter chips.
- Disabled state when manual location/GPS is unavailable.

## Settings Theme

Layout: language/theme/accent/text card and diagnostics card.

Inputs/actions:
- Language picker.
- Theme segmented control.
- Accent color swatch buttons.
- Text size minus/current/plus.
- Diagnostics: bridge ready, last sync.

Difference from XAML:
- XAML shows language as selectable cards and accent as labeled chips; React uses picker/swatches.

## Settings Adhan

Layout: status line, sound section, calculation section, offsets section, fasting reminders section.

Inputs/actions:
- Sound option buttons.
- Volume editable value.
- Preview/test button.
- Method option buttons.
- Madhhab option buttons.
- High latitude rule option buttons.
- Fajr angle editable value.
- Isha angle editable value.
- Offset editable values for Fajr, Sunrise, Dhuhr, Asr, Maghrib, Isha, Imsak.
- Iftar delay editable value.
- Imsak advance editable value.
- Clock format option buttons.

Missing compared with XAML:
- Add custom adhan sound button.
- Sound list play/stop/remove custom controls.
- Volume slider presentation.
- Imsak reminder editor and list.
- Iftar reminder editor and list.
- Per-prayer sound/vibration override rows.

## Settings Notifications

Layout: status line, reminders/vibration section, system permissions section, test button grid.

Inputs/actions:
- Enable adhan toggle.
- Primary adhan type option buttons.
- Minutes before editable value.
- Vibration toggle.
- Vibration strength option buttons.
- Vibration pattern option buttons.
- Hide on close Windows toggle.
- Run background Windows toggle.
- Test notification button.
- Test alarm button.

Missing compared with XAML:
- Windows background hint text.
- Adhan reminder editor: scope, prayer, value, unit, direction, alert type, add.
- Existing reminders list with alert type picker and remove.

## Settings Permissions

Layout: status page with permission cards.

Visible values/actions:
- Permission title/description/status/fallback/action.
- Request/open action buttons.

Difference from XAML:
- React should confirm alarm mode summary is rendered; if not, add it.

## Settings Alarm Reminders

Layout: status line, built-in section, your reminders section.

Inputs/actions:
- Built-in reminder toggle buttons.
- Your reminders global toggle.
- User reminder toggle and editable text.
- Add reminder button.

Missing compared with XAML:
- Separate new-reminder text input before Add.
- Edit button concept.
- Remove button per user reminder.

## Settings Tasbih

Layout: status line, one section per preset.

Inputs/actions:
- Preset name edit for each preset.
- Repeat mode buttons for each preset.
- Item text/count edits.
- Item remove.
- Add item for each preset.
- Add preset button.

Missing compared with XAML:
- Preset picker controlling selected preset.
- New preset name entry before add.
- New item text/count entries before add.
- Item start index.
- Move item up/down buttons.

## Settings About

Layout: header, identity card, info card, contact card, action buttons.

Visible values/actions:
- Name, tagline, privacy, source, maintainer, contact.
- Email `support@rynex.nl`.
- Phone `+31 00 000 0000`.
- Website.
- Email/call/website/report buttons.

Mismatch with XAML:
- Email and phone values do not match old XAML.
- Button text is localized shorter text, not exact `Email Rynex` / `Call +31 6 10331734`.

## Onboarding

Layout: progress bar, card, bottom Back/Next controls.

Inputs/actions:
- Language picker.
- Permission summary and grant-all button.
- Location status/vpn warning text.

Missing compared with XAML:
- Language grid/card selection.
- Current permission card with fallback/status/action.
- Location permission card.
- Manual location setup on location step.
