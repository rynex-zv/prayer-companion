##input
- Windows Debug route `/settings/about`.
- Controls: Clear site data, Email, Call, Website, Report issue, Pull latest web version, remote bundle URL, Save URL, and Reset URL.

##Actions
- Saved the existing remote URL through the React form and verified the page remained healthy.
- Verified the loaded origin and browser capability reporting.

##Tested
- Save URL completed through the native typed transport.
- Windows content loaded from `https://app.prayadfree.local/index.html`; no unsafe `file:` navigation appeared.
- Browser remote update is represented as unsupported instead of falsely reporting success.
- External operations now return a fast operation ID and report completion/failure asynchronously; automation scenario 10 passed 40 assertions in 418 ms with zero warnings.

##Faild+why
- `NOT RUN` — Clear site data is destructive; external Email/Call/Website/Report intents and remote download were not launched while the user was active.

##things to fix
- Exercise/cancel external intents and remote download in isolation, then test Clear site data last on a disposable profile.

##remarks
- The old `file:` origin failure is fixed in this build.
