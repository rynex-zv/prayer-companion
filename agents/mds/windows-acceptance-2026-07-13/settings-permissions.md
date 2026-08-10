##input
- Windows Debug route `/settings/permissions`.
- Controls: Request/Grant button for every projected permission row.

##Actions
- Loaded and inspected every permission row and its React request control.

##Tested
- Permission state projection rendered truthfully and the request controls are addressable through the React DOM.

##Faild+why
- `NOT RUN` — OS grant/deny prompts were intentionally not invoked while the user was operating the PC.

##things to fix
- Exercise each prompt on an isolated Windows profile and record acknowledgement timing plus final granted/denied/unsupported state.

##remarks
- Prompt completion is excluded from 300 ms; only its initial native acknowledgement is subject to the ceiling.
