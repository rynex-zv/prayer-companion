# Failed automation scenarios

- Run: `web-2026-08-11T07-41-28-856Z`
- Platform: `web`
- Failed: **3**
- Passed: **7**

## 02-today-calendar — User checks prayer times and explores the calendar

- Documentation: `02-today-calendar.md`
- Duration: 137 ms
- Assertions completed: 2
- Failed assertion: **Today refresh button is missing**

### Completed steps

- Navigated to /

### Stack

```text
AutomationAssertionError: Today refresh button is missing
    at Y.assert (http://127.0.0.1:4179/assets/runner-DdR1NKpB.js:1:346)
    at Object.run (http://127.0.0.1:4179/assets/runner-DdR1NKpB.js:3:2682)
```

## 03-qibla-location — User changes location and verifies Qibla modes

- Documentation: `03-qibla-location.md`
- Duration: 126 ms
- Assertions completed: 1
- Failed assertion: **Missing selector: locations:city**

### Completed steps

- Navigated to /settings/locations

### Stack

```text
AutomationAssertionError: Missing selector: locations:city
    at Y.element (http://127.0.0.1:4179/assets/runner-DdR1NKpB.js:1:1081)
    at Object.run (http://127.0.0.1:4179/assets/runner-DdR1NKpB.js:3:3368)
```

## 10-platform-operations — System operations acknowledge promptly and report truthful completion

- Documentation: `10-platform-operations.md`
- Duration: 4153 ms
- Assertions completed: 24
- Failed assertion: **adhan.sound.addCustom did not publish completion**

### Completed steps

- external.openEmail acknowledged in 1 ms and completed asynchronously
- external.call acknowledged in 0 ms and completed asynchronously
- external.openUrl acknowledged in 0 ms and completed asynchronously
- external.reportIssue acknowledged in 0 ms and completed asynchronously

### Stack

```text
AutomationAssertionError: adhan.sound.addCustom did not publish completion
    at m (http://127.0.0.1:4179/assets/runner-DdR1NKpB.js:1:5018)
    at async Object.run (http://127.0.0.1:4179/assets/runner-DdR1NKpB.js:3:13262)
```
