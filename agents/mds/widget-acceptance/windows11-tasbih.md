# Windows 11 — Tasbih

##input

Widget profile template: Tasbih. Target: Windows 11.

##Actions

Build, install without clearing existing data, add the real widget, bind a profile, resize through every supported family, change language/theme/location, restart, and exercise every available action.

##Tested

Shared Core projection/layout contracts, atomic projection storage and Adaptive Card conversion are covered by automated tests. The isolated provider builds in Debug and Release with 0 warnings/0 errors; instance assignment and small/medium/large projection publication are wired to the shared local files. Platform acceptance is recorded only after a signed, installed real host run.

##Faild+why

BLOCKED: the signed MSIX widget-provider installation and Widgets Board UI Automation are not complete.

##things to fix

Complete the remaining native implementation and real-host acceptance; capture timing, accessibility, persistence, resize, update, and screenshot evidence.

##remarks

No pass is claimed from source presence, unit tests, preview rendering, coordinate guessing, or a build alone. Status remains لم تتم الإضافة.
