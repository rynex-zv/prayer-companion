# Failed automation scenarios

- Run: `web-2026-08-05T17-45-00-945Z`
- Platform: `web`
- Failed: **1**
- Passed: **8**

## 01-page-contract — Every page, text, control name, input value, and navigation

- Documentation: `01-page-contract.md`
- Duration: 7959 ms
- Assertions completed: 251
- Failed assertion: **adhan:fajr-angle did not become 18 automation**

### Completed steps

- Navigated to /onboarding
- Validated 7 control names on /onboarding step 1
- Clicked onboarding:next
- Validated 6 control names on /onboarding step 2
- Clicked onboarding:next
- Validated 8 control names on /onboarding step 3
- Set onboarding:location:country=SA
- Set onboarding:location:country=NL
- Set onboarding:location:city=Rotterdam
- Set onboarding:location:city=Amsterdam
- Set onboarding:location:latitude=52.3676 automation
- Set onboarding:location:latitude=52.3676
- Set onboarding:location:longitude=4.9041 automation
- Set onboarding:location:longitude=4.9041
- Changed and restored 4 inputs on /onboarding step 3
- Clicked onboarding:back
- Clicked onboarding:next
- Clicked onboarding:next
- Completed onboarding navigation and redirect
- Navigated to /
- Validated 1 control names on /
- Changed and restored 0 inputs on /
- Navigated to /calendar
- Validated 40 control names on /calendar
- Changed and restored 0 inputs on /calendar
- Navigated to /qibla
- Validated 7 control names on /qibla
- Changed and restored 0 inputs on /qibla
- Navigated to /tasbih
- Validated 2 control names on /tasbih
- Set tasbih:preset-picker=hundred
- Set tasbih:preset-picker=after-prayer
- Changed and restored 1 inputs on /tasbih
- Navigated to /settings
- Validated 8 control names on /settings
- Changed and restored 0 inputs on /settings
- Navigated to /settings/locations
- Validated 9 control names on /settings/locations
- Set locations:country=SA
- Set locations:country=NL
- Set locations:city=Rotterdam
- Set locations:city=Amsterdam
- Set locations:latitude=52.3676 automation
- Set locations:latitude=52.3676
- Set locations:longitude=4.9041 automation
- Set locations:longitude=4.9041
- Set locations:qibla-reading-mode=map
- Set locations:qibla-reading-mode=compass
- Set locations:qibla-filter-mode=night
- Set locations:qibla-filter-mode=none
- Changed and restored 6 inputs on /settings/locations
- Navigated to /settings/theme
- Validated 12 control names on /settings/theme
- Set theme:language=ar
- Set theme:language=en
- Changed and restored 1 inputs on /settings/theme
- Navigated to /settings/adhan
- Validated 22 control names on /settings/adhan
- Set adhan:volume=81
- Set adhan:volume=80
- Set adhan:method=Jafari
- Set adhan:method=Auto
- Set adhan:madhhab=Maliki
- Set adhan:madhhab=Shafi
- Set adhan:high-latitude=SeventhOfTheNight
- Set adhan:high-latitude=MiddleOfTheNight

### Stack

```text
AutomationAssertionError: adhan:fajr-angle did not become 18 automation
    at c (http://127.0.0.1:4179/assets/runner-isGUHr9c.js:1:4580)
    at async N.setValue (http://127.0.0.1:4179/assets/runner-isGUHr9c.js:1:1349)
    at async N.setAndRestore (http://127.0.0.1:4179/assets/runner-isGUHr9c.js:1:1552)
    at async N.mutateEveryInput (http://127.0.0.1:4179/assets/runner-isGUHr9c.js:1:3376)
    at async Object.run (http://127.0.0.1:4179/assets/runner-isGUHr9c.js:3:2391)
```
