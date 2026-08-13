# Failed automation scenarios

- Run: `web-2026-08-11T07-32-37-598Z`
- Platform: `web`
- Failed: **5**
- Passed: **5**

## 01-page-contract — Every page, text, control name, input value, and navigation

- Documentation: `01-page-contract.md`
- Duration: 13668 ms
- Assertions completed: 54
- Failed assertion: **Route /calendar did not finish rendering visible content**
- ⚠️ location.refresh took 289 ms (browser)

### Completed steps

- Navigated to /onboarding
- Validated 7 control names on /onboarding step 1
- Clicked onboarding:next
- Validated 6 control names on /onboarding step 2
- Clicked onboarding:next
- Validated 8 control names on /onboarding step 3
- Set onboarding:location:country=NL
- Set onboarding:location:country=
- Set onboarding:location:latitude=53.3676
- Set onboarding:location:latitude=52.3676
- Set onboarding:location:longitude=5.9041
- Set onboarding:location:longitude=4.9041
- Changed and restored 4 inputs on /onboarding step 3
- Clicked onboarding:back
- Clicked onboarding:next
- Clicked onboarding:next
- Completed onboarding navigation and redirect
- Navigated to /
- Validated 1 control names on /
- Changed and restored 0 inputs on /

### Stack

```text
AutomationAssertionError: Route /calendar did not finish rendering visible content
    at l (http://127.0.0.1:4179/assets/runner-CrjgLbmB.js:1:5018)
    at async G.navigate (http://127.0.0.1:4179/assets/runner-CrjgLbmB.js:1:773)
    at async Object.run (http://127.0.0.1:4179/assets/runner-CrjgLbmB.js:3:2343)
```

## 02-today-calendar — User checks prayer times and explores the calendar

- Documentation: `02-today-calendar.md`
- Duration: 136 ms
- Assertions completed: 2
- Failed assertion: **Today refresh button is missing**

### Completed steps

- Navigated to /

### Stack

```text
AutomationAssertionError: Today refresh button is missing
    at G.assert (http://127.0.0.1:4179/assets/runner-CrjgLbmB.js:1:346)
    at Object.run (http://127.0.0.1:4179/assets/runner-CrjgLbmB.js:3:2682)
```

## 03-qibla-location — User changes location and verifies Qibla modes

- Documentation: `03-qibla-location.md`
- Duration: 161 ms
- Assertions completed: 1
- Failed assertion: **Missing selector: locations:city**

### Completed steps

- Navigated to /settings/locations

### Stack

```text
AutomationAssertionError: Missing selector: locations:city
    at G.element (http://127.0.0.1:4179/assets/runner-CrjgLbmB.js:1:1081)
    at Object.run (http://127.0.0.1:4179/assets/runner-CrjgLbmB.js:3:3368)
```

## 08-adhan-settings — User adjusts Adhan calculation and fasting reminder settings

- Documentation: `08-adhan-settings.md`
- Duration: 476 ms
- Assertions completed: 11
- Failed assertion: **Changing calculation method Auto -> Jafari did not change any prayer time**

### Completed steps

- Navigated to /settings/locations
- Set locations:country=NL
- Set locations:city=Amsterdam
- Navigated to /settings/adhan
- Set adhan:clock-format=24h
- Shared prayer inputs: Amsterdam NL, 52.3676, 4.9041, Auto, Shafi, MiddleOfTheNight, 24h
- Shared prayer snapshot: 
- Set adhan:method=Jafari

### Stack

```text
AutomationAssertionError: Changing calculation method Auto -> Jafari did not change any prayer time
    at G.assert (http://127.0.0.1:4179/assets/runner-CrjgLbmB.js:1:346)
    at Object.run (http://127.0.0.1:4179/assets/runner-CrjgLbmB.js:3:9182)
```

## 10-platform-operations — System operations acknowledge promptly and report truthful completion

- Documentation: `10-platform-operations.md`
- Duration: 7 ms
- Assertions completed: 2
- Failed assertion: **external.openEmail returned a fake success instead of a pending acknowledgement**

### Completed steps

- None

### Stack

```text
AutomationAssertionError: external.openEmail returned a fake success instead of a pending acknowledgement
    at G.assert (http://127.0.0.1:4179/assets/runner-CrjgLbmB.js:1:346)
    at Object.run (http://127.0.0.1:4179/assets/runner-CrjgLbmB.js:3:12920)
```
