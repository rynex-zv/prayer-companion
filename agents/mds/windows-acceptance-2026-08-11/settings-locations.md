##input
Windows Debug automation, route `/settings/locations`.

##Actions
Changed and restored country, city, latitude, longitude, reading mode, and filter; exercised GPS behavior.

##Tested
Each input before/after value, backend confirmation, manual-location persistence, live GPS projection delivery, reverse-geocode retry, and no GPS-to-IP fallback.

##Faild+why
Functional automation: none. UIA exposure is blocked.

##things to fix
Expose selects, numeric inputs, and buttons to UIA.

##remarks
GPS coordinates cannot report success with a blank country/city: the UI now offers retry GPS, explicit IP, or manual entry.
