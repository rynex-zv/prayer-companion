##input
- Windows Debug route `/settings/adhan`.
- Controls: custom sound add/select/play/remove, volume, calculation/madhhab/high-latitude/angles/clock fields, prayer offsets, Suhoor/Iftar reminder controls, and per-prayer sound/vibration options.

##Actions
- Exercised calculation/offset/prayer controls, explicitly selected Jafari and Tehran, verified their projections, restored the original method, created an Imsak reminder, and removed it. Sound operations ran in safe automation mode with no physical audio output.

##Tested
- Volume and reminder mutations returned complete confirmed projections without refresh; temporary reminder cleanup succeeded.
- Reminder catalog, scopes, and prayer-specific options rendered in the native app.
- Jafari returned six formatted times using Fajr 16°, Maghrib 4°, Isha 14°; Tehran used Fajr 17.7°, Maghrib 4.5°, Isha 14°.
- Scenario 08 passed 54 assertions with zero warnings.

##Faild+why
- `NOT RUN` — Add custom sound opens the external file picker; no file was selected or removed in this session.
- `NOT RUN` — actual audio output for every sound/prayer combination was not exhaustively audited; the PC remained at volume 0 as requested.

##things to fix
- Use a disposable sound file to test import/play/remove, then audit every prayer override combination.

##remarks
- File-picker completion is external, but its initial acknowledgement must remain below 300 ms.
