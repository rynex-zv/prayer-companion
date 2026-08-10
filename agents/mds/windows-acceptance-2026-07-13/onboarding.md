##input
- Windows Debug route `/onboarding`.
- Controls: five languages, individual permission buttons, Request all, GPS/manual location choices, Refresh GPS, country/city, Back, and Next/Finish.

##Actions
- Entered the React onboarding route, advanced through its steps, exercised navigation fields, and invoked Finish.

##Tested
- `onboarding.complete` returned successfully and redirected out of onboarding.
- Step navigation and manual location controls rendered and remained localized in the selected language.

##Faild+why
- `NOT RUN` — OS permission prompts, Request all outcomes, and real GPS acquisition were not completed in this user-active session.

##things to fix
- Repeat on a disposable profile for each language and every grant/deny/GPS outcome, verifying persistence after restart.

##remarks
- This was a real route/action test, not an inventory-only report.
