##input
Windows Debug automation, route `/settings/adhan`.

##Actions
Changed/restored sound, volume, method, madhhab, high-latitude rule, angles, all offsets, clock format, fasting reminders, and per-prayer overrides.

##Tested
54 assertions in the Adhan scenario, complete confirmed projections, custom-sound operation, and calculation controls.

##Faild+why
Functional automation: none. UIA remains blocked.

##things to fix
Expose all semantic controls through UIA; continue validating prayer calculations against external reference fixtures.

##remarks
The application has one shared astronomical engine and multiple calculation-method choices; these are separate concepts.

