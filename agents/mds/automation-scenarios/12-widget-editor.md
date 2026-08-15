# 12 — Widget editor

##input

Widget development build with the shared Core widget RPCs enabled.

##Actions

Open the Widgets editor, create a profile, change its name, density, platform, dimension, language and contrast, save it, restore defaults, then delete it.

##Tested

CRUD responses, value changes, Core-backed live preview, semantic host dimensions, Arabic preview, contrast correction, restore, and delete without a follow-up snapshot request.

Latest successful run: `web-2026-08-13T22-20-23-688Z` — 19 assertions, 0 warnings, 0 failures, 2517 ms scenario duration. Same-device RPC timings observed: create 88 ms, previews 21/10 ms, updates 18/13 ms, and delete 19 ms.

##Faild+why

No assertion failed in the latest run. Production enablement and real platform acceptance are still incomplete, so status remains **لم تتم الإضافة**.

##things to fix

Fix every failed assertion, overflow, inaccessible control, mixed-language result, duplicate request, or RPC over 300 ms before enabling the editor in Production.

##remarks

This scenario is compiled only when the widget development feature is enabled; unfinished widget UI is intentionally absent from Production.
