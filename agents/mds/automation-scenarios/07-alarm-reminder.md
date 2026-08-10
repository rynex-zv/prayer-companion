# 07 — Alarm reminder journey

## input

Built-in alarm toggles plus user reminder create, edit, enable/disable, and remove controls.

## Actions

Toggle a built-in reminder twice, create a temporary user reminder, toggle and edit it, then remove it.

## Tested

Alarm collection mutations, confirmed projections, stable generated selectors, persistence, and cleanup.

## Faild+why

Fails if the created row cannot be located, edited, toggled, or deleted.

## things to fix

Add alarm ringing/snooze integration under a deterministic clock fixture.

## remarks

No real alarm is scheduled in automation mode.
