##input
Windows Debug automation, routes `/qibla` and `/settings/locations`.

##Actions
Changed and restored location, Qibla reading mode, and filter; navigated to Qibla; exercised its named controls.

##Tested
Location-confirmed projection, Qibla modes, accessible DOM names, and navigation.

##Faild+why
Functional automation: none. Windows UIA cannot see the React document.

##things to fix
Expose Qibla buttons and readings through UIA.

##remarks
GPS/IP fallback is not used when GPS is disabled or fails.

