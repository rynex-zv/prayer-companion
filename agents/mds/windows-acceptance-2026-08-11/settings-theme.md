##input
Windows Debug automation, route `/settings/theme`.

##Actions
Changed/restored language, theme, accent, and text size.

##Tested
English/Arabic consistency, RTL/LTR state, theme controls, persistence, and value confirmation.

##Faild+why
Functional automation: none. Incomplete French/Spanish/Turkish catalogs are intentionally not advertised; UIA remains blocked.

##things to fix
Complete those translation catalogs before re-advertising them; expose controls through UIA.

##remarks
This prevents mixed-language pages from being presented as supported localization.

