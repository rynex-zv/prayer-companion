##input
Windows Debug automation, route `/settings/notifications`.

##Actions
Changed type, minutes, vibration strength/pattern, reminder scope, and specific prayer; created and removed a reminder.

##Tested
Specific-prayer options, collection updates returned by mutations, asynchronous platform acknowledgement, and value restoration.

##Faild+why
Functional automation: none. UIA remains blocked.

##things to fix
Expose notification form controls through UIA.

##remarks
Interactive notification delivery is excluded from 300 ms completion, but acknowledgement was checked.

