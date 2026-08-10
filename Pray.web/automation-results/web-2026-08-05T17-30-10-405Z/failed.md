# Failed automation scenarios

- Run: `web-2026-08-05T17-30-10-405Z`
- Platform: `web`
- Failed: **9**
- Passed: **0**

## 01-page-contract — Every page, text, control name, input value, and navigation

- Documentation: `01-page-contract.md`
- Duration: 424 ms
- Assertions completed: 37
- Failed assertion: **Could not set onboarding:location:country to NL**

### Completed steps

- Navigated to /onboarding
- Validated 7 control names on /onboarding step 1
- Clicked onboarding:next
- Validated 6 control names on /onboarding step 2
- Clicked onboarding:next
- Validated 8 control names on /onboarding step 3
- Set onboarding:location:country=SA

### Stack

```text
AutomationAssertionError: Could not set onboarding:location:country to NL
    at C.assert (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:1:316)
    at C.setValue (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:1:1190)
    at async C.setAndRestore (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:1:1552)
    at async C.mutateEveryInput (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:1:3284)
    at async Object.run (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:3:1949)
    at async H (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:3:10628)
```

## 02-today-calendar — User checks prayer times and explores the calendar

- Documentation: `02-today-calendar.md`
- Duration: 12007 ms
- Assertions completed: 1
- Failed assertion: **Route did not become /**

### Completed steps

- None

### Stack

```text
AutomationAssertionError: Route did not become /
    at c (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:1:4488)
    at async C.navigate (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:1:454)
    at async Object.run (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:3:2554)
    at async H (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:3:10628)
```

## 03-qibla-location — User changes location and verifies Qibla modes

- Documentation: `03-qibla-location.md`
- Duration: 12009 ms
- Assertions completed: 1
- Failed assertion: **Route did not become /settings/locations**

### Completed steps

- None

### Stack

```text
AutomationAssertionError: Route did not become /settings/locations
    at c (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:1:4488)
    at async C.navigate (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:1:454)
    at async Object.run (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:3:3272)
    at async H (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:3:10628)
```

## 04-theme-localization — User personalizes language, theme, accent, and text size

- Documentation: `04-theme-localization.md`
- Duration: 12006 ms
- Assertions completed: 1
- Failed assertion: **Route did not become /settings/theme**

### Completed steps

- None

### Stack

```text
AutomationAssertionError: Route did not become /settings/theme
    at c (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:1:4488)
    at async C.navigate (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:1:454)
    at async Object.run (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:3:4300)
    at async H (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:3:10628)
```

## 05-tasbih-workflow — User creates, edits, orders, uses, and removes a Tasbih preset

- Documentation: `05-tasbih-workflow.md`
- Duration: 12007 ms
- Assertions completed: 1
- Failed assertion: **Route did not become /settings/tasbih**

### Completed steps

- None

### Stack

```text
AutomationAssertionError: Route did not become /settings/tasbih
    at c (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:1:4488)
    at async C.navigate (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:1:454)
    at async Object.run (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:3:4914)
    at async H (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:3:10628)
```

## 06-notification-reminder — User configures and removes an Adhan notification reminder

- Documentation: `06-notification-reminder.md`
- Duration: 12009 ms
- Assertions completed: 1
- Failed assertion: **Route did not become /settings/notifications**

### Completed steps

- None

### Stack

```text
AutomationAssertionError: Route did not become /settings/notifications
    at c (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:1:4488)
    at async C.navigate (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:1:454)
    at async Object.run (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:3:6313)
    at async H (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:3:10628)
```

## 07-alarm-reminder — User creates, edits, toggles, and removes an alarm reminder

- Documentation: `07-alarm-reminder.md`
- Duration: 12004 ms
- Assertions completed: 1
- Failed assertion: **Route did not become /settings/alarms**

### Completed steps

- None

### Stack

```text
AutomationAssertionError: Route did not become /settings/alarms
    at c (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:1:4488)
    at async C.navigate (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:1:454)
    at async Object.run (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:3:7340)
    at async H (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:3:10628)
```

## 08-adhan-settings — User adjusts Adhan calculation and fasting reminder settings

- Documentation: `08-adhan-settings.md`
- Duration: 12010 ms
- Assertions completed: 1
- Failed assertion: **Route did not become /settings/adhan**

### Completed steps

- None

### Stack

```text
AutomationAssertionError: Route did not become /settings/adhan
    at c (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:1:4488)
    at async C.navigate (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:1:454)
    at async Object.run (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:3:8063)
    at async H (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:3:10628)
```

## 09-settings-about-navigation — User opens every Settings page and saves the About URL

- Documentation: `09-settings-about-navigation.md`
- Duration: 12009 ms
- Assertions completed: 1
- Failed assertion: **Route did not become /settings**

### Completed steps

- None

### Stack

```text
AutomationAssertionError: Route did not become /settings
    at c (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:1:4488)
    at async C.navigate (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:1:454)
    at async Object.run (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:3:9281)
    at async H (http://127.0.0.1:4179/assets/runner-WNMGqPkW.js:3:10628)
```
