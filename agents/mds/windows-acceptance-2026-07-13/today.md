##input
- Windows Debug route `/`.
- Controls: Refresh and bottom tabs Today, Calendar, Qibla, Tasbih, and Settings.

##Actions
- Loaded the native Today projection, invoked Refresh, selected each destination tab, and returned to Today.

##Tested
- Prayer summary/list rendered, every bottom tab navigated to the correct React route, and Refresh returned a confirmed projection.
- Final native `today.refresh` round trip: 33 ms, with no follow-up snapshot.

##Faild+why
- None for the rendered Today controls.

##things to fix
- Keep the one-command/no-refresh assertion in automated acceptance.

##remarks
- Final cold `app.bootstrap` was 203 ms; the old 551 ms screenshot belongs to the superseded build.
