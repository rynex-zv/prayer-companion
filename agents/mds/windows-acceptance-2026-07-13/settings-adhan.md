##input
- Windows Debug route `/settings/adhan`.
- Controls: custom sound add/select/play/remove, volume, calculation/madhhab/high-latitude/angles/clock fields, prayer offsets, Suhoor/Iftar reminder controls, and per-prayer sound/vibration options.

##Actions
- Selected/played built-in sounds, changed volume and restored it, traversed calculation/offset/prayer controls, created an Imsak reminder, and removed it.

##Tested
- Volume and reminder mutations returned complete confirmed projections without refresh; temporary reminder cleanup succeeded.
- Reminder catalog, scopes, and prayer-specific options rendered in the native app.

##Faild+why
- `NOT RUN` — Add custom sound opens the external file picker; no file was selected or removed in this session.
- `NOT RUN` — actual audio output for every sound/prayer combination was not exhaustively audited.

##things to fix
- Use a disposable sound file to test import/play/remove, then audit every prayer override combination.

##remarks
- File-picker completion is external, but its initial acknowledgement must remain below 300 ms.
