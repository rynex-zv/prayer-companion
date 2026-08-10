##input
- Windows Debug route `/settings/theme`.
- Controls: language picker (English, Arabic, French, Spanish, Turkish), Dark/Light/System, five accents, text-size decrease, and text-size increase.

##Actions
- Applied Dark, Light, and System; applied rose, amber, blue, green, and teal; decreased/increased text size; selected Arabic.

##Tested
- Every theme mode, accent button, and text-size control visibly applied and persisted.
- Arabic selection localized the tested page; controls expose semantic React names.

##Faild+why
- `NOT RUN` — full page-by-page mixed-language inspection for English, French, Spanish, and Turkish was not repeated in this Windows session.

##things to fix
- Automate the complete route matrix once per locale and assert no fallback-language strings.

##remarks
- Final tested state was Arabic/System/teal with text size restored after the decrement/increment pair.
