# Failed automation scenarios

- Run: `web-2026-08-05T17-23-43-949Z`
- Platform: `web`
- Failed: **9**
- Passed: **0**

## 01-page-contract — Every page, text, control name, input value, and navigation

- Documentation: `01-page-contract.md`
- Duration: 91 ms
- Assertions completed: 2
- Failed assertion: **/onboarding step 1 has no visible text**

### Completed steps

- Navigated to /onboarding

### Stack

```text
AutomationAssertionError: /onboarding step 1 has no visible text
    at q.assert (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:1:316)
    at q.validateVisibleTextAndNames (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:1:2106)
    at Object.run (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:3:1622)
    at async H (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:3:10526)
```

## 02-today-calendar — User checks prayer times and explores the calendar

- Documentation: `02-today-calendar.md`
- Duration: 85 ms
- Assertions completed: 2
- Failed assertion: **Today refresh button is missing**

### Completed steps

- Navigated to /

### Stack

```text
AutomationAssertionError: Today refresh button is missing
    at q.assert (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:1:316)
    at Object.run (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:3:2677)
    at async H (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:3:10526)
```

## 03-qibla-location — User changes location and verifies Qibla modes

- Documentation: `03-qibla-location.md`
- Duration: 83 ms
- Assertions completed: 1
- Failed assertion: **Missing selector: locations:city**

### Completed steps

- Navigated to /settings/locations

### Stack

```text
AutomationAssertionError: Missing selector: locations:city
    at q.element (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:1:795)
    at Object.run (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:3:3322)
    at async H (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:3:10526)
```

## 04-theme-localization — User personalizes language, theme, accent, and text size

- Documentation: `04-theme-localization.md`
- Duration: 95 ms
- Assertions completed: 1
- Failed assertion: **No alternate option for theme:language**

### Completed steps

- Navigated to /settings/theme

### Stack

```text
Error: No alternate option for theme:language
    at c (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:3:9971)
    at Object.run (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:3:4375)
    at async H (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:3:10526)
```

## 05-tasbih-workflow — User creates, edits, orders, uses, and removes a Tasbih preset

- Documentation: `05-tasbih-workflow.md`
- Duration: 85 ms
- Assertions completed: 2
- Failed assertion: **Could not set settings-tasbih:new-preset-name to Automation preset**

### Completed steps

- Navigated to /settings/tasbih

### Stack

```text
AutomationAssertionError: Could not set settings-tasbih:new-preset-name to Automation preset
    at q.assert (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:1:316)
    at q.setValue (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:1:1012)
    at async Object.run (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:3:4951)
    at async H (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:3:10526)
```

## 06-notification-reminder — User configures and removes an Adhan notification reminder

- Documentation: `06-notification-reminder.md`
- Duration: 83 ms
- Assertions completed: 2
- Failed assertion: **Could not click selector: notifications:vibration**

### Completed steps

- Navigated to /settings/notifications

### Stack

```text
AutomationAssertionError: Could not click selector: notifications:vibration
    at q.assert (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:1:316)
    at q.click (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:1:880)
    at async Object.run (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:3:6357)
    at async H (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:3:10526)
```

## 07-alarm-reminder — User creates, edits, toggles, and removes an alarm reminder

- Documentation: `07-alarm-reminder.md`
- Duration: 83 ms
- Assertions completed: 1
- Failed assertion: **No selector starts with alarms:built-in:**

### Completed steps

- Navigated to /settings/alarms

### Stack

```text
AutomationAssertionError: No selector starts with alarms:built-in:
    at q.findSelector (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:1:1744)
    at Object.run (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:3:7387)
    at async H (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:3:10526)
```

## 08-adhan-settings — User adjusts Adhan calculation and fasting reminder settings

- Documentation: `08-adhan-settings.md`
- Duration: 83 ms
- Assertions completed: 1
- Failed assertion: **Missing numeric input adhan:volume**

### Completed steps

- Navigated to /settings/adhan

### Stack

```text
Error: Missing numeric input adhan:volume
    at j (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:3:10128)
    at Object.run (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:3:8136)
    at async H (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:3:10526)
```

## 09-settings-about-navigation — User opens every Settings page and saves the About URL

- Documentation: `09-settings-about-navigation.md`
- Duration: 1757 ms
- Assertions completed: 18
- Failed assertion: **About remote URL is empty**

### Completed steps

- Navigated to /settings
- Clicked settings:row:locations
- Navigated to /settings
- Clicked settings:row:themeDiagnostics
- Navigated to /settings
- Clicked settings:row:adhan
- Navigated to /settings
- Clicked settings:row:notifications
- Navigated to /settings
- Clicked settings:row:permissions
- Navigated to /settings
- Clicked settings:row:alarmReminders
- Navigated to /settings
- Clicked settings:row:tasbihSettings
- Navigated to /settings
- Clicked settings:row:about
- Navigated to /settings/about

### Stack

```text
AutomationAssertionError: About remote URL is empty
    at q.assert (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:1:316)
    at Object.run (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:3:9506)
    at async H (http://127.0.0.1:4179/assets/runner-Cx87btYO.js:3:10526)
```
