##input
- Windows Debug route `/calendar`.
- Controls: previous month, next month, Year, Month, Week, Day, Today, Gregorian, Hijri, rendered calendar cells, and detail Close when shown.

##Actions
- Invoked previous/next month, all four views, both calendar systems, Today, and a rendered day cell.
- Repeated previous/next rapidly to verify command serialization after the fix.

##Tested
- Every top-level navigation/mode control reacted and the selected date opened its detail state.
- Rapid navigation no longer overlaps: observed calendar calls were 68 ms, 32 ms, 27 ms, and 32 ms, all below 300 ms.
- Navigation buttons are disabled while their native command is pending.

##Faild+why
- None for the exercised navigation, mode, and date-detail controls.

##things to fix
- Add a generated-cell parameterized test covering the full date grid and Close action on every supported locale.

##remarks
- Testing used React DOM controls; it did not stop at merely opening the page.
