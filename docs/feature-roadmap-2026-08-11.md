# Prayer Companion — 7 feature roadmap

## 1. Live prayer progress on Today

Shows how far the user is through the current prayer interval and how much time remains before the next prayer. It fits the app because the main screen is already centered on the next prayer; a progress indicator makes the countdown easier to understand at a glance.

Implementation:

- Add absolute prayer timestamps to the Today projection.
- Compute current interval progress in the React Today route from the previous and next prayer timestamps.
- Render a compact progress bar under the next-prayer countdown.
- Refresh Today when the interval expires so the next prayer advances.

Tests:

- Core test verifies the Today projection includes ordered absolute timestamps.
- Web typecheck verifies the route handles missing timestamps safely.
- Runtime check verifies the progress bar moves without a backend refresh every second.

## 2. Share today's prayer times

Lets the user share the current location, date, and prayer times through the native share sheet when available, or clipboard when browser share is unavailable. It fits the app because prayer times are commonly sent to family, groups, and mosque/community chats.

Implementation:

- Add a frontend share action on the Today page.
- Generate the share text from the confirmed Today projection.
- Use `navigator.share` when available and `navigator.clipboard.writeText` otherwise.
- Show a small status message after copy/share.

Tests:

- Unit-level route helper test for share text formatting.
- Web typecheck.
- Browser runtime check by stubbing `navigator.share`/clipboard.

## 3. Per-prayer remaining-time list

Shows each prayer row with either “in HH:MM” for upcoming prayers or “passed” for completed prayers. It fits the app because it makes the daily list actionable, not just static clock values.

Implementation:

- Reuse Today timing timestamps from feature 1.
- Add a derived remaining label in the Today route.
- Keep it UI-derived so no extra durable state is introduced.

Tests:

- Helper test covers upcoming, passed, and missing timestamp cases.
- Web typecheck.

## 4. Browser custom adhan sound cache

Allows web users to upload an audio file from the device, store it in IndexedDB, select it immediately, preview it, and remove it. It fits the app because adhan/audio customization is core to notification behavior and must work on phones from the website.

Implementation:

- Add a browser audio store in IndexedDB for custom sound Blobs.
- Handle `adhan.sound.addCustom`, `adhan.sound.preview`, and `adhan.sound.removeCustom` in the browser platform adapter.
- Return authoritative settings projections from add/remove so the UI updates immediately.
- Keep default sound available and previewable when no custom sounds exist.

Tests:

- Web typecheck.
- Browser runtime check for IndexedDB add/preview/remove.
- Existing .NET tests confirm settings projection remains valid.

## 5. Permission refresh after onboarding grants

Updates onboarding permission cards immediately after the user grants location or notifications. It fits the app because onboarding should not claim permissions are disabled after the browser/OS already granted them.

Implementation:

- Return a permission snapshot after browser `permissions.requestAll`.
- Refresh onboarding projection after individual permission grants.
- Keep the UI-local granted set only as optimistic presentation, not durable state.

Tests:

- Web typecheck.
- Runtime check with browser permission mocks where available.

## 6. Device-aware app download button

On the website, shows a download button for the latest built app package matching the current device only when a real artifact exists. It fits the app because users can install from their phone without connecting it to the development machine.

Implementation:

- Look for `/downloads/manifest.json`.
- Accept the manifest only if it is actual JSON, not the SPA fallback HTML.
- Match Android APK, iOS package link, Windows EXE/ZIP, or desktop ZIP by user agent.
- Verify candidate URLs do not resolve to HTML before showing the button.

Tests:

- Web typecheck.
- Runtime check: absent manifest hides the button; valid manifest shows the matching package.

## 7. Safer location/title projection

When coordinates are known but reverse geocoding cannot provide a city/country, show coordinates instead of a stale city like Amsterdam. It fits the app because wrong location names destroy trust in prayer times.

Implementation:

- Clear stale city/country when coordinates change and no authoritative reverse-geocode result exists.
- Preserve API-provided reverse-geocode values.
- Use coordinates as the Today title when no place name is available.
- Keep GPS/manual updates invalidating Today so prayer times reflect the changed settings.

Tests:

- .NET WebState RPC tests for stale location cleanup.
- Prayer calculation matrix tests for updated coordinates.
- Live browser check after GPS/manual location update.
