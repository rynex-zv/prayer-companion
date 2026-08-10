# 08 — Adhan settings journey

## input

Shared calculation engine, volume, method, madhhab, high-latitude rule, clock format, fasting reminder rows, and related controls.

## Actions

Set the deterministic parity input (Amsterdam NL, `52.3676, 4.9041`, 24-hour clock), assert that the page reports `SharedCoreAdhan`, capture the calculated prayer clocks, change the method and assert that the projection changes, restore it, adjust volume, add/edit/remove an Imsak reminder, and verify confirmed section projections.

## Tested

The identical .NET/WebAssembly calculation engine contract, calculation-method effect, Adhan values, fasting reminder CRUD, field mappings, response completeness, and timing.

## Faild+why

Fails on invalid enum mapping, missing reminder catalogs, stale state, or incomplete mutation responses.

## things to fix

Add custom sound file-picker coverage in a separate interactive test because file selection is externally controlled.

## remarks

The scenario excludes sound playback and OS file pickers from deterministic same-device timing.
