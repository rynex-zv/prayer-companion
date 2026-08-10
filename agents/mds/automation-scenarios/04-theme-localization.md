# 04 — Theme and localization journey

## input

Language, light/dark/system mode, accent choices, and text-size controls.

## Actions

Change and restore language, exercise all theme modes and accents, decrease/increase text size, and inspect document language/direction.

## Tested

Localized projection loading, persisted theme patches, `lang`, `dir`, RTL/LTR consistency, and control availability.

## Faild+why

Fails on mixed or missing language state, invalid direction, missing controls, or unconfirmed patches.

## things to fix

Add visual token assertions if a stable computed-style contract is adopted.

## remarks

Each state-changing action waits for its backend response before the next action.
