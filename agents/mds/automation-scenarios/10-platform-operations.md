# 10 — Platform operations

## Input

- Windows, Android, or Web automation build with the matching platform flag enabled.
- Email, phone, HTTPS URL, issue report, custom Adhan import, permission validation, alarm test, and notification test operations.

## Actions

- Opened `/alarm` without an active alarm and verified automatic return to Today.

- Sends each interactive operation with a stable operation ID.
- Measures the initial typed-backend acknowledgement.
- Waits for the matching asynchronous completion event without stopping later checks after a failure.
- Sends an invalid permission ID to prove the backend rejects unsupported input.

## Tested

- Interactive operations acknowledge with `accepted=true`, `status=pending`, and the same operation ID in under 300 ms.
- Completion is reported as `platform.operation.completed` rather than a fabricated immediate success.
- Automation mode simulates external applications and file selection without changing the user's system.
- Alarm and notification test commands return explicit action results.

## Faild+why

- Filled by the generated failed report when an acknowledgement, completion, timing, or validation assertion fails.

## things to fix

- Any operation missing a completion event, exceeding 300 ms before acknowledgement, or silently accepting invalid input blocks release.

## remarks

- File pickers, permission prompts, GPS acquisition, external apps, and notification delivery are outside the 300 ms completion ceiling; only their initial backend acknowledgement is timed.
