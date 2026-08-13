# Failed automation scenarios

- Run: `web-2026-08-11T07-44-26-985Z`
- Platform: `web`
- Failed: **1**
- Passed: **9**

## 02-today-calendar — User checks prayer times and explores the calendar

- Documentation: `02-today-calendar.md`
- Duration: 12130 ms
- Assertions completed: 1
- Failed assertion: **Timed out waiting for today:refresh**

### Completed steps

- Navigated to /

### Stack

```text
AutomationAssertionError: Timed out waiting for today:refresh
    at m (http://127.0.0.1:4179/assets/runner-CcvJfk86.js:1:5026)
    at async Z.waitForSelector (http://127.0.0.1:4179/assets/runner-CcvJfk86.js:1:1880)
    at async Object.run (http://127.0.0.1:4179/assets/runner-CcvJfk86.js:3:2742)
```
