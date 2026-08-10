##input
- Windows Debug automation and Windows Release route `/`, using the existing persisted Jafari method.
- Controls: Refresh and bottom tabs Today, Calendar, Qibla, Tasbih, and Settings.

##Actions
- Invalidated the obsolete error cache, loaded the native Today projection, invoked Refresh, selected each destination tab, and returned to Today.

##Tested
- Six prayer times rendered with the Jafari calculation method; the Release render trace reported `timingCount: 6`.
- Every bottom tab navigated to the correct React route and Refresh returned a confirmed projection.
- Latest production `app.bootstrap`: 40 ms bridge round trip / 6 ms backend.

##Faild+why
- None for the rendered Today controls.

##things to fix
- Keep Jafari/Tehran six-timing assertions and cache-schema migration coverage in automated acceptance.

##remarks
- The prior blank/error page was caused by unsupported Jafari calculation plus a cached error projection. Both causes are fixed; no silent method substitution was added.
