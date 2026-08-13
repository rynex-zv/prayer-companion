##input
Windows Debug automation, route `/settings`.

##Actions
Opened every named Settings row and verified each destination route.

##Tested
Locations, theme, Adhan, notifications, permissions, alarms, Tasbih, and About navigation.

##Faild+why
Functional automation: none. UIA sees only three native controls, not the React settings rows.

##things to fix
Expose the React settings list to UIA.

##remarks
All destinations were asserted by route name.

