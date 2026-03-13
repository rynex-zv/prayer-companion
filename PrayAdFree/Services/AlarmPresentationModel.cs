namespace Pray_Ad_Free.Services;

public sealed record AlarmPresentationModel(
    string PrayerClock,
    string DelayFromBase,
    string PrayerName,
    string ReminderText,
    int MinDelayMinutes,
    int MaxDelayMinutes,
    int InitialDelayMinutes);
