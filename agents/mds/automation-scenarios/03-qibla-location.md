# 03 — Qibla and Location journey

## input

Country, city, Qibla reading/filter settings, heading modes, compass/map modes, and map zoom controls.

## Actions

Change and restore a city and Qibla preferences, navigate to Qibla, exercise every heading/reading/filter option, then zoom the map in and out.

## Tested

Confirmed location persistence, option catalog compatibility, Qibla navigation, accessible controls, and map interaction.

## Faild+why

Fails on missing options, stale confirmed projections, invalid value mapping, or absent Qibla controls.

## things to fix

Add GPS/permission-denied variants when deterministic platform permission fixtures are introduced.

## remarks

GPS acquisition and reverse geocoding are external operations and are not used as same-device timing samples.
