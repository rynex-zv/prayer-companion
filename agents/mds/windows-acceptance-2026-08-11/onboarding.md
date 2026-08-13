##input
Windows Debug automation, route `/onboarding`, all three steps.

##Actions
Opened the route; exercised Next/Back; validated control names; changed and restored country, city, latitude, and longitude; completed onboarding and verified redirect.

##Tested
Language, permissions, location controls, value persistence before/after backend confirmation, navigation, and completion.

##Faild+why
Functional automation: none. Windows UI Automation remains blocked because the React document provider is not exposed.

##things to fix
Expose the WebView2 React document and named controls through UIA.

##remarks
No coordinate guessing was used; actions were dispatched to named React controls.

