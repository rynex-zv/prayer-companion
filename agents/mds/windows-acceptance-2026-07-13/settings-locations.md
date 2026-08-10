##input
- Windows Debug route `/settings/locations`.
- Controls: GPS toggle, Refresh GPS, latitude, longitude, country, city, Qibla reading mode, and Qibla filter.

##Actions
- Exercised GPS/refresh acknowledgement, coordinate edits, and location pickers.
- In the final rebuilt session changed city Amsterdam → Rotterdam and restored Rotterdam → Amsterdam.

##Tested
- Both final city mutations persisted, returned the full confirmed projection, displayed `تم الحفظ`, and required no follow-up read.
- Final native calls: snapshot 4 ms; mutations 41 ms and 10 ms.
- Original city was restored successfully.

##Faild+why
- `NOT RUN` — real GPS acquisition and its OS permission prompt are external/interactive operations.

##things to fix
- Verify real GPS success, denial, and timeout asynchronously on a device with controllable location services.

##remarks
- The earlier 770/479/420 ms location failures were fixed by returning the existing mutation projection and moving notification reconciliation off the response path.
