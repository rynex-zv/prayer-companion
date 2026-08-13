# 09 — Settings and About navigation journey

## input

Every Settings row plus the remote web URL field and save control.

## Actions

Open each settings destination through its visible row, return to Settings between destinations, then save the current About URL.

## Tested

- About always exposes a device-specific download or an explicit unavailable status; the control may never disappear silently.

Settings navigation, route names, About query/save responses, browser unsupported-update behavior, and stable page controls.

## Faild+why

Fails if a row does not navigate, a destination does not render, or URL save is not acknowledged.

## things to fix

Add external email/call/site launch tests only where the host can safely intercept those intents.

## remarks

This scenario never pulls or deploys a remote bundle.
