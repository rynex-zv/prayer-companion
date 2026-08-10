##input
- Windows Debug route `/tasbih`.
- Controls: accessible counter ring, Reset, preset picker, and preset options.

##Actions
- Incremented twice, reset to zero, opened the picker, and selected `100x Subhan Allah`.

##Tested
- Counter mutations and reset visibly updated without a manual refresh.
- Preset selection returned the complete affected projection; the ring and picker have React accessible labels.

##Faild+why
- None for increment, reset, and preset selection.

##things to fix
- Add a parameterized selection assertion for every user-created preset.

##remarks
- Preset create/delete testing is recorded separately in `settings-tasbih.md`.
