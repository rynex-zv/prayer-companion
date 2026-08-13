##input
Windows Debug automation and production run, route `/`.

##Actions
Opened Today, refreshed once, inspected prayer rows, remaining-time labels, progress, location, calculation method, and countdown.

##Tested
Six prayer timestamps, live countdown/progress, refresh interaction, Rotterdam production data, navigation to Calendar, background resume synchronization, and the explicit GPS/IP/manual recovery controls.

##Faild+why
Functional automation: none. UIA cannot enumerate the React controls.

##things to fix
Restore UIA exposure; retain the 300 ms data-call ceiling.

##remarks
The location operation now forwards its persisted settings events to React before completion; Today no longer shows a calculation-method error while GPS is still resolving.
