# 06 — Notification reminder journey

## input

Vibration toggle, minutes-before, reminder value/unit/direction/alert type, add, and remove.

## Actions

Toggle vibration twice, change/restore lead time, create a reminder, edit every reminder field, then remove it.

## Tested

Notification setting mappings, reminder create/update/delete projections, persistence, and cleanup.

## Faild+why

Fails on enum/catalog mismatch, stale projection, missing reminder row, or failed deletion.

## things to fix

Add real-delivery tests only in a dedicated notification integration environment.

## remarks

Test mode uses a no-op native notification scheduler, so acceptance never sends a real user notification.
