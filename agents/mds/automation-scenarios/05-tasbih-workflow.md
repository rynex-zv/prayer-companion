# 05 — Tasbih workflow

## input

Preset name, repeat mode, item text/count, ordering, remove controls, counter increment, and reset.

## Actions

Create and rename a temporary preset, add/edit/reorder/remove an item, use the preset in Tasbih, reset the counter, and delete the temporary preset.

## Tested

Create/update/delete projections, ordering, counter state, persistence, and absence of unnecessary follow-up snapshots.

## Faild+why

Fails if any mutation does not return the affected confirmed collection or if temporary data remains after deletion.

## things to fix

Add maximum-count and empty-name validation variants later.

## remarks

The scenario cleans up all data it creates.
