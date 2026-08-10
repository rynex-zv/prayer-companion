# 01 — Page and control contract

## input

All enabled inputs, selects, text areas, buttons, links, route roots, visible text, and accessible names on onboarding and every registered application route.

## Actions

Walk the three onboarding steps, complete onboarding, navigate every route by the in-app router, change every enabled field to a valid alternate value, wait for its confirmed RPC projection, then restore its original value.

## Tested

Visible content; accessible control names; stable `data-selector-name` values; before/change/restore assertions; route navigation; RPC timing and the 300 ms ceiling.

## Faild+why

The scenario fails on the exact assertion, route, or RPC that violated the contract. Failure does not stop scenarios 02–09.

## things to fix

Add selectors and accessible names to every new control, and add valid alternate-value logic when introducing a new input type.

## remarks

This is code-driven DOM interaction inside the real React UI. It does not use coordinates or UI Automation fallback.
