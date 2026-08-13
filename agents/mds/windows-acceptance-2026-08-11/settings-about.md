##input
Windows Debug automation, route `/settings/about`.

##Actions
Opened the page, changed/restored the HTTPS remote URL, saved it, and exercised platform operations safely.

##Tested
Navigation, URL validation, explicit unsupported browser update behavior, contact operations, and production hiding of `/test`.

##Faild+why
Functional automation: none. The previously published APK endpoint was HTML, so no successful download claim is made. UIA remains blocked.

##things to fix
Publish only verified artifacts and expose About controls through UIA.

##remarks
Windows local content uses the stable HTTPS virtual host, not `file:` navigation.

