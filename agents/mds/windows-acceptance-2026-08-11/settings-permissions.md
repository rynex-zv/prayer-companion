##input
Windows Debug automation, route `/settings/permissions`.

##Actions
Inspected named permission controls and exercised permission operations in automation-safe mode.

##Tested
Truthful acknowledgement/completion behavior, explicit failure for unsupported/denied operations, `PermissionStatus.change`, focus, and visibility resynchronization.

##Faild+why
Functional automation: none. Real user prompts are excluded; UIA exposure remains blocked.

##things to fix
Expose permission controls through UIA and retain truthful platform states.

##remarks
No fake-success permission result is accepted; granted/denied text and button state now follow the browser permission in real time.
