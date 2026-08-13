# Failed automation scenarios

- Run: `web-2026-08-13T00-16-40-183Z`
- Platform: `web`
- Failed: **1**
- Passed: **9**

## 01-page-contract — Every page, text, control name, input value, and navigation

- Documentation: `01-page-contract.md`
- Duration: 4753 ms
- Assertions completed: 36
- Failed assertion: **onboarding:location:city was overwritten after backend confirmation**

### Completed steps

- Navigated to /onboarding
- Validated 4 control names on /onboarding step 1
- Clicked onboarding:next
- Validated 6 control names on /onboarding step 2
- Clicked onboarding:next
- Validated 8 control names on /onboarding step 3
- Set onboarding:location:country=SA
- Set onboarding:location:country=NL

### Stack

```text
AutomationAssertionError: onboarding:location:city was overwritten after backend confirmation
    at c (http://127.0.0.1:4179/assets/runner-DJXDxyMc.js:1:5038)
    at async Z.setValue (http://127.0.0.1:4179/assets/runner-DJXDxyMc.js:1:1486)
    at async Z.setAndRestore (http://127.0.0.1:4179/assets/runner-DJXDxyMc.js:1:1694)
    at async Z.mutateEveryInput (http://127.0.0.1:4179/assets/runner-DJXDxyMc.js:1:3508)
    at async Object.run (http://127.0.0.1:4179/assets/runner-DJXDxyMc.js:3:1944)
```
