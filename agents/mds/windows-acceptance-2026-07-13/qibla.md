##input
- Windows Debug route `/qibla`.
- Controls: Auto/Manual direction, Compass/Map display, None/Night/Contrast filters, permission request, map zoom in, and map zoom out.

##Actions
- Invoked Auto, Manual, Compass, Map, and all three filters; inspected the React accessible names and map controls.

##Tested
- All seven in-process segmented choices updated the visible Qibla presentation without a crash.
- Qibla controls now carry React accessible names; Map rendered its zoom controls.

##Faild+why
- `NOT RUN` — the OS location-permission prompt/GPS acquisition is interactive and excluded from the synchronous ceiling.
- `NOT RUN` — final zoom in/out confirmation was interrupted by physical user input, so no zoom pass is claimed.

##things to fix
- Re-run the two zoom controls and the OS permission outcome in an isolated input session.

##remarks
- Accessibility ownership is in the semantic React DOM, not in MAUI wrapper controls.
