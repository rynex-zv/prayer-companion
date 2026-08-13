##input
Windows Debug automation, route `/calendar`.

##Actions
Clicked previous/next, Today, year/month/week/day views, Hijri/Gregorian modes, and a named day cell.

##Tested
All visible Calendar control names, navigation state, mode changes, and returned mutation projections.

##Faild+why
Functional automation: none. UIA document exposure remains absent.

##things to fix
Make the same semantic React buttons visible to Windows UIA.

##remarks
Testing used selectors and assertions, not screen coordinates.

