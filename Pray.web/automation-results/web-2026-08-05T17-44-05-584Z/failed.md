# Failed automation scenarios

- Run: `web-2026-08-05T17-44-05-584Z`
- Platform: `web`
- Failed: **2**
- Passed: **7**

## 01-page-contract — Every page, text, control name, input value, and navigation

- Documentation: `01-page-contract.md`
- Duration: 3497 ms
- Assertions completed: 201
- Failed assertion: **/settings/adhan control adhan:volume has no accessible name/text**

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

### Stack

```text
AutomationAssertionError: /settings/adhan control adhan:volume has no accessible name/text
    at N.assert (http://127.0.0.1:4179/assets/runner-C52TG1mH.js:1:330)
    at N.validateVisibleTextAndNames (http://127.0.0.1:4179/assets/runner-C52TG1mH.js:1:2690)
    at Object.run (http://127.0.0.1:4179/assets/runner-C52TG1mH.js:3:2360)
```

## 02-today-calendar — User checks prayer times and explores the calendar

- Documentation: `02-today-calendar.md`
- Duration: 2150 ms
- Assertions completed: 12
- Failed assertion: **No selector starts with calendar:day:**

### Completed steps

- Navigated to /
- Clicked tab:calendar
- Clicked calendar:previous
- Clicked calendar:next
- Clicked calendar:view:year
- Clicked calendar:view:month
- Clicked calendar:view:week
- Clicked calendar:view:day
- Clicked calendar:mode:hijri
- Clicked calendar:mode:gregorian
- Clicked calendar:today

### Stack

```text
AutomationAssertionError: No selector starts with calendar:day:
    at N.findSelector (http://127.0.0.1:4179/assets/runner-C52TG1mH.js:1:2015)
    at Object.run (http://127.0.0.1:4179/assets/runner-C52TG1mH.js:3:3110)
```
