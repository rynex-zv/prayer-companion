# Failed automation scenarios

- Run: `web-2026-08-11T23-16-52-945Z`
- Platform: `web`
- Failed: **1**
- Passed: **9**

## 10-platform-operations — System operations acknowledge promptly and report truthful completion

- Documentation: `10-platform-operations.md`
- Duration: 4238 ms
- Assertions completed: 41
- Failed assertion: **Inactive alarm route did not return to Today**

### Completed steps

- external.openEmail acknowledged in 1 ms and completed asynchronously
- external.call acknowledged in 0 ms and completed asynchronously
- external.openUrl acknowledged in 0 ms and completed asynchronously
- external.reportIssue acknowledged in 0 ms and completed asynchronously
- adhan.sound.addCustom acknowledged in 0 ms and completed asynchronously
- alarm.test acknowledged in 0 ms and completed asynchronously
- notification.test acknowledged in 0 ms and completed asynchronously
- permissions.request acknowledged in 0 ms and completed asynchronously

### Stack

```text
AutomationAssertionError: Inactive alarm route did not return to Today
    at c (http://127.0.0.1:4179/assets/runner-DRNf8wD6.js:1:5038)
    at async Object.run (http://127.0.0.1:4179/assets/runner-DRNf8wD6.js:3:14534)
```
