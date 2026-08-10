# 02 — Today and Calendar journey

## input

Today refresh and Calendar previous/next, Year/Month/Week/Day, Gregorian/Hijri, Today, and date-cell controls.

## Actions

Refresh Today, navigate with the Calendar tab, exercise every calendar mode and view, return to month view, and select a rendered day.

## Tested

Today refresh acknowledgement, tab navigation, calendar projections, month movement, mode/view state, and date selection.

## Faild+why

Fails if a control is missing, navigation is rejected, or an RPC/confirmed state does not complete as required.

## things to fix

Extend this journey when event details or calendar search controls are added.

## remarks

Remote prayer-time refresh completes asynchronously; its local acknowledgement remains subject to 300 ms.
