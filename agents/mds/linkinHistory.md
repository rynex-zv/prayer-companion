# Linkin History

Status meanings: `BROKEN`, `PARTIAL`, `WORKING`, `NOT VERIFIED`.
Do not delete this file; append/update rows as checks are completed.

| Area | Route | Screenshot | Language | Buttons | Inputs | Theme | Backend tracking | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|
| Today | `/` | WORKING | PARTIAL | WORKING | n/a | WORKING | WORKING | PARTIAL | Windows WebView no longer white; `windows-today-rpc-working.png` captured. MAUI RPC confirmed for `today.getSnapshot`, `app.getShellSnapshot`, and `renderComplete`. Language still needs full route audit. |
| Calendar | `/calendar` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | n/a | NOT VERIFIED | NOT VERIFIED | BROKEN | Initial log created; runtime screenshot pending. |
| Qibla | `/qibla` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | n/a | NOT VERIFIED | NOT VERIFIED | BROKEN | Initial log created; runtime screenshot pending. |
| Tasbih | `/tasbih` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | n/a | NOT VERIFIED | NOT VERIFIED | BROKEN | Initial log created; runtime screenshot pending. |
| Settings index | `/settings` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | n/a | NOT VERIFIED | NOT VERIFIED | BROKEN | Initial log created; runtime screenshot pending. |
| Settings locations | `/settings/locations` | NOT VERIFIED | PARTIAL | PARTIAL | PARTIAL | NOT VERIFIED | WORKING | PARTIAL | GPS/location route no longer returns a blank page while loading or on RPC error; frontend now normalizes missing snapshot fields and `settings.patch.locations` is wired in MAUI. Runtime screenshot still pending. |
| Settings theme | `/settings/theme` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | BROKEN | `settings.patch` and `app.setTheme` currently no-op in MAUI. |
| Settings adhan | `/settings/adhan` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | BROKEN | `settings.patch`/`settings.invoke` currently no-op in MAUI. |
| Settings notifications | `/settings/notifications` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | BROKEN | `settings.patch`/`settings.invoke` currently no-op in MAUI. |
| Settings permissions | `/settings/permissions` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | BROKEN | Permission actions currently no-op through web handler. |
| Settings alarm reminders | `/settings/alarms` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | BROKEN | `settings.patch` currently no-op in MAUI. |
| Settings tasbih | `/settings/tasbih` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | BROKEN | `settings.invoke` currently no-op in MAUI. |
| Settings about | `/settings/about` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | n/a | NOT VERIFIED | NOT VERIFIED | BROKEN | About actions currently no-op in MAUI. |
| Onboarding | `/onboarding` | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | BROKEN | Initial log created; runtime screenshot pending. |
