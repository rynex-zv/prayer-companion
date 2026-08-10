# Failed automation scenarios

- Run: `web-2026-08-05T22-26-35-529Z`
- Platform: `web`
- Failed: **1**
- Passed: **0**

## 08-adhan-settings — User adjusts Adhan calculation and fasting reminder settings

- Documentation: `08-adhan-settings.md`
- Duration: 1316 ms
- Assertions completed: 27
- Failed assertion: **300 ms data-call ceiling exceeded: onboarding.getSnapshot=476ms, onboarding.complete=453ms**
- ⚠️ onboarding.getSnapshot took 476 ms (browser)
- ⚠️ onboarding.complete took 453 ms (browser)
- ⚠️ 300 ms ceiling exceeded: onboarding.getSnapshot=476ms
- ⚠️ 300 ms ceiling exceeded: onboarding.complete=453ms

### Completed steps

- Navigated to /settings/adhan
- Set adhan:method=Jafari
- Set adhan:method=Auto
- Set adhan:volume=81
- Set adhan:volume=80
- Set adhan:madhhab=Maliki
- Set adhan:madhhab=Shafi
- Set adhan:high-latitude=SeventhOfTheNight
- Set adhan:high-latitude=MiddleOfTheNight
- Set adhan:clock-format=auto
- Set adhan:clock-format=24h
- Clicked adhan:imsak-reminder:add
- Set adhan:imsak-reminder:value:0=11
- Set adhan:imsak-reminder:direction:0=after
- Clicked adhan:imsak-reminder:remove:0

### Stack

```text
Error: 300 ms data-call ceiling exceeded: onboarding.getSnapshot=476ms, onboarding.complete=453ms
    at KL (http://127.0.0.1:4179/assets/index-C3Ned9W6.js:16:12198)
    at async IL (http://127.0.0.1:4179/assets/index-C3Ned9W6.js:16:14688)
```
