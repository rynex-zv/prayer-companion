using System.Collections.Generic;
using System.Linq;
using Plugin.LocalNotification;
#if ANDROID
using Plugin.LocalNotification.AndroidOption;
#endif
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace Pray_Ad_Free.Services;

public sealed class LocalNotificationScheduler : ILocalNotificationScheduler {
    private readonly PrayerSchedulePlanner _planner;
    private readonly IAppLogger _logger;
    private readonly IWindowsNotificationQueueService _windowsNotificationQueueService;
    private readonly SemaphoreSlim _scheduleGate = new(1, 1);
    private string _lastScheduleSignature = string.Empty;

    public LocalNotificationScheduler(
        PrayerSchedulePlanner planner,
        IAppLogger logger,
        IWindowsNotificationQueueService windowsNotificationQueueService) {
        _planner = planner;
        _logger = logger;
        _windowsNotificationQueueService = windowsNotificationQueueService;
    }

    public async Task ScheduleAsync(IEnumerable<PrayerDay> days, AppSettings settings, CancellationToken cancellationToken, bool requestPermissions = true) {
        var dayList = days?
            .OrderBy(item => item.Date)
            .ToList() ?? new List<PrayerDay>();
        var scheduleSignature = BuildScheduleSignature(dayList, settings);

        await _scheduleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            if (string.Equals(_lastScheduleSignature, scheduleSignature, StringComparison.Ordinal)) {
                _logger.LogEvent("NotificationScheduleSkipped", "signature_match");
                return;
            }

            _lastScheduleSignature = scheduleSignature;

            if (requestPermissions && !OperatingSystem.IsWindows()) {
                var permission = new NotificationPermission {
                    AskPermission = true
                };
#if ANDROID
                permission.Android = new AndroidNotificationPermission {
                    RequestPermissionToScheduleExactAlarm = true
                };
#endif
                await LocalNotificationCenter.Current.RequestNotificationPermission(permission).ConfigureAwait(false);
            }

            await CancelAsyncCore().ConfigureAwait(false);

            var requests = new List<NotificationRequest>();
            var signatures = new HashSet<string>(StringComparer.Ordinal);

            foreach (var day in dayList) {
                cancellationToken.ThrowIfCancellationRequested();

                var schedule = _planner.BuildSchedule(day, settings.Notifications);
                foreach (var item in schedule) {
                    if (item.Time <= DateTime.Now) {
                        continue;
                    }

                    var prayerName = LocalizationManager.TranslatePrayer(item.Prayer);
                    var overrideSettings = FindOverride(settings.Notifications.PrayerOverrides, item.Prayer);
                    var soundKey = overrideSettings?.SoundKey ?? settings.Notifications.SoundKey;
                    var effectiveSoundKey = AdhanSoundLibrary.ResolveEffectiveSoundKey(soundKey);
                    var isSilent = AdhanSoundLibrary.IsSilent(effectiveSoundKey);
                    var notificationSound = ResolveSystemNotificationSound(settings.Notifications, effectiveSoundKey, isSilent);
                    var isCustomSound = AdhanSoundLibrary.IsCustomSound(settings.Notifications, effectiveSoundKey);
                    var useRuntimeAdhanPlayback = ShouldUseRuntimeAdhanPlayback();
                    var playRuntimeAdhan = useRuntimeAdhanPlayback && !isSilent && (isCustomSound || string.IsNullOrWhiteSpace(notificationSound));
                    var vibrationOverride = overrideSettings?.EnableVibration;

                    AddIfUnique(requests, signatures, new NotificationRequest {
                        NotificationId = BuildId(day.Date, item.Prayer),
                        Title = string.Format(LocalizationManager.Translate("Notification_PrayerTitle"), prayerName),
                        Description = string.Format(LocalizationManager.Translate("Notification_PrayerBody"), prayerName),
                        Silent = ResolveNotificationSilent(playRuntimeAdhan, isSilent),
                        Sound = playRuntimeAdhan ? string.Empty : notificationSound ?? string.Empty,
                        ReturningData = isSilent || !playRuntimeAdhan
                            ? string.Empty
                            : AdhanNotificationPayload.BuildPlay(item.Prayer, effectiveSoundKey),
                        Schedule = new NotificationRequestSchedule {
                            NotifyTime = ToLocalKind(item.Time),
                            NotifyRepeatInterval = null
#if ANDROID
                            ,
                            Android = new AndroidScheduleOptions {
                                AlarmType = AndroidAlarmType.RtcWakeup
                            }
#endif
                        },
#if ANDROID
                        Android = new AndroidOptions {
                            Priority = AndroidPriority.Default,
                            ChannelId = BuildAndroidChannelId(effectiveSoundKey, isSilent, playRuntimeAdhan, AdhanReminderAlertType.Adhan),
                            VibrationPattern = isSilent ? Array.Empty<long>() : BuildVibration(settings.Notifications, vibrationOverride)
                        }
#endif
                    });
                }

                ScheduleFastingReminders(day, settings, requests, signatures);
                ScheduleAdhanReminders(day, settings, requests, signatures);
            }

            var (scheduledCount, next) = await EmitRequestsAsync(requests, settings).ConfigureAwait(false);
            try {
                var nextTime = next?.Schedule?.NotifyTime?.ToString("O") ?? "none";
                _logger.LogEvent("NotificationScheduledCount", $"{scheduledCount}|next={nextTime}");
            } catch {
            }
        } finally {
            _scheduleGate.Release();
        }
    }

    public Task CancelAsync() {
        _lastScheduleSignature = string.Empty;
        return CancelAsyncCore();
    }

    private Task CancelAsyncCore() {
        LocalNotificationCenter.Current.CancelAll();
        _windowsNotificationQueueService.Clear();
        return Task.CompletedTask;
    }

    private static void AddIfUnique(ICollection<NotificationRequest> requests, ISet<string> signatures, NotificationRequest request) {
        if (request.Schedule?.NotifyTime == null) {
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description)) {
            return;
        }

        var when = request.Schedule.NotifyTime.Value.ToString("O");
        var signature = $"{request.NotificationId}|{when}|{request.ReturningData}|{request.Title}|{request.Description}";
        if (signatures.Contains(signature)) {
            return;
        }

        signatures.Add(signature);
        requests.Add(request);
    }

    private static int BuildId(DateOnly date, PrayerId prayer) {
        return int.Parse($"{date:yyMMdd}{(int)prayer + 1}");
    }

    private static int BuildReminderId(DateOnly date, int group, int index) {
        var year = date.Year % 100;
        var dayOfYear = date.DayOfYear;
        var safeGroup = Math.Clamp(group, 0, 99);
        var safeIndex = Math.Clamp(index, 0, 99);
        return (year * 10_000_000) + (dayOfYear * 10_000) + (safeGroup * 100) + safeIndex;
    }

    private static bool ShouldSchedule(DateTime time) => time > DateTime.Now;

    private static DateTime ToLocalKind(DateTime time) {
        return time.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(time, DateTimeKind.Local)
            : time;
    }

    private static long[] BuildVibration(NotificationSettings settings, bool? overrideEnabled = null) {
        var enabled = overrideEnabled ?? settings.EnableVibration;
        if (!enabled) {
            return Array.Empty<long>();
        }

        var strength = settings.VibrationStrength switch {
            VibrationStrength.Low => 200,
            VibrationStrength.Medium => 400,
            _ => 700
        };

        return settings.VibrationPattern switch {
            VibrationPattern.Long => new long[] { 0, strength * 2 },
            VibrationPattern.Pulse => new long[] { 0, strength, 200, strength, 200, strength },
            _ => new long[] { 0, strength, 200, strength }
        };
    }

    private static void ScheduleFastingReminders(
        PrayerDay day,
        AppSettings settings,
        ICollection<NotificationRequest> requests,
        ISet<string> signatures) {
        var baseImsak = day.Timings.Imsak.AddMinutes(-settings.FastingOffsets.ImsakAdvanceMinutes);
        var baseIftar = day.Timings.Maghrib.AddMinutes(settings.FastingOffsets.IftarDelayMinutes);

        var imsakReminders = settings.FastingReminders.ImsakRemindersMinutes.Distinct().OrderBy(item => item).ToList();
        for (var i = 0; i < imsakReminders.Count; i++) {
            var time = baseImsak.AddMinutes(imsakReminders[i]);
            if (!ShouldSchedule(time)) {
                continue;
            }

            AddIfUnique(requests, signatures, new NotificationRequest {
                NotificationId = BuildReminderId(day.Date, 8, i),
                Title = LocalizationManager.Translate("ImsakReminder"),
                Description = LocalizationManager.Translate("Imsak"),
                Schedule = new NotificationRequestSchedule {
                    NotifyTime = ToLocalKind(time),
                    NotifyRepeatInterval = null
#if ANDROID
                    ,
                    Android = new AndroidScheduleOptions {
                        AlarmType = AndroidAlarmType.RtcWakeup
                    }
#endif
                }
            });
        }

        var iftarReminders = settings.FastingReminders.IftarRemindersMinutes.Distinct().OrderBy(item => item).ToList();
        for (var i = 0; i < iftarReminders.Count; i++) {
            var time = baseIftar.AddMinutes(iftarReminders[i]);
            if (!ShouldSchedule(time)) {
                continue;
            }

            AddIfUnique(requests, signatures, new NotificationRequest {
                NotificationId = BuildReminderId(day.Date, 9, i),
                Title = LocalizationManager.Translate("IftarReminder"),
                Description = LocalizationManager.Translate("Iftar"),
                Schedule = new NotificationRequestSchedule {
                    NotifyTime = ToLocalKind(time),
                    NotifyRepeatInterval = null
#if ANDROID
                    ,
                    Android = new AndroidScheduleOptions {
                        AlarmType = AndroidAlarmType.RtcWakeup
                    }
#endif
                }
            });
        }
    }

    private static void ScheduleAdhanReminders(
        PrayerDay day,
        AppSettings settings,
        ICollection<NotificationRequest> requests,
        ISet<string> signatures) {
        var notificationSettings = settings.Notifications;
        var reminderItems = AdhanReminderResolver.Resolve(notificationSettings);
        if (reminderItems.Count == 0) {
            return;
        }

        var prayers = notificationSettings.ReminderScope == AdhanReminderScope.SpecificPrayer
            ? new[] { notificationSettings.ReminderPrayer }
            : new[] { PrayerId.Fajr, PrayerId.Dhuhr, PrayerId.Asr, PrayerId.Maghrib, PrayerId.Isha };

        for (var p = 0; p < prayers.Length; p++) {
            var prayer = prayers[p];
            var overrideSettings = FindOverride(notificationSettings.PrayerOverrides, prayer);
            var soundKey = overrideSettings?.SoundKey ?? notificationSettings.SoundKey;
            var effectiveSoundKey = AdhanSoundLibrary.ResolveEffectiveSoundKey(soundKey);
            var isCustomSound = AdhanSoundLibrary.IsCustomSound(notificationSettings, effectiveSoundKey);
            var baseTime = day.Timings.Get(prayer);

            for (var i = 0; i < reminderItems.Count; i++) {
                var reminder = reminderItems[i];
                var notifyTime = baseTime.AddMinutes(reminder.OffsetMinutes);
                if (!ShouldSchedule(notifyTime) || !ReminderDispatchPolicy.ShouldEmitToast(reminder.AlertType)) {
                    continue;
                }

                var shouldPlayAdhan = ReminderDispatchPolicy.ShouldPlayAdhan(reminder.AlertType, notificationSettings.EnableAdhan);
                var isSilent = AdhanSoundLibrary.IsSilent(effectiveSoundKey);
                var notificationSound = reminder.AlertType == AdhanReminderAlertType.Notification
                    ? string.Empty
                    : ResolveSystemNotificationSound(notificationSettings, effectiveSoundKey, isSilent) ?? string.Empty;
                var useRuntimeAdhanPlayback = ShouldUseRuntimeAdhanPlayback();
                var playRuntimeAdhan = shouldPlayAdhan && useRuntimeAdhanPlayback && !isSilent &&
                                       (isCustomSound || string.IsNullOrWhiteSpace(notificationSound));

                AddIfUnique(requests, signatures, new NotificationRequest {
                    NotificationId = BuildReminderId(day.Date, 20 + p, i),
                    Title = LocalizationManager.Translate("AdhanReminder"),
                    Description = BuildAdhanReminderDescription(prayer, reminder.OffsetMinutes, reminder.AlertType),
                    Silent = ResolveNotificationSilent(playRuntimeAdhan, false),
                    Sound = playRuntimeAdhan ? string.Empty : notificationSound,
                    ReturningData = playRuntimeAdhan
                        ? AdhanNotificationPayload.BuildPlay(prayer, effectiveSoundKey)
                        : string.Empty,
                    Schedule = new NotificationRequestSchedule {
                        NotifyTime = ToLocalKind(notifyTime),
                        NotifyRepeatInterval = null
#if ANDROID
                        ,
                        Android = new AndroidScheduleOptions {
                            AlarmType = AndroidAlarmType.RtcWakeup
                        }
#endif
                    },
#if ANDROID
                    Android = new AndroidOptions {
                        Priority = AndroidPriority.Default,
                        ChannelId = BuildAndroidChannelId(effectiveSoundKey, isSilent, playRuntimeAdhan, reminder.AlertType),
                        VibrationPattern = BuildVibration(notificationSettings, overrideSettings?.EnableVibration)
                    }
#endif
                });
            }
        }
    }

    private async Task<(int scheduledCount, NotificationRequest? next)> EmitRequestsAsync(
        IReadOnlyList<NotificationRequest> requests,
        AppSettings settings) {
        if (requests.Count == 0) {
            return (0, null);
        }

        var ordered = requests
            .Where(item => item.Schedule?.NotifyTime != null)
            .OrderBy(item => item.Schedule!.NotifyTime)
            .ToList();

        if (ordered.Count == 0) {
            return (0, null);
        }

        if (OperatingSystem.IsWindows()) {
            var planned = ordered.Select(item => new PlannedNotification(
                item.NotificationId,
                item.Schedule!.NotifyTime!.Value,
                item.Title ?? string.Empty,
                item.Description ?? string.Empty,
                item.ReturningData ?? string.Empty,
                AdhanNotificationPayload.TryParse(item.ReturningData, out _))).ToList();

            var normalized = NotificationScheduleSelector.Normalize(planned, DateTime.Now);
            _windowsNotificationQueueService.ReplaceSchedule(normalized);
            var nextPlanned = normalized.FirstOrDefault();
            var nextRequest = nextPlanned == null
                ? null
                : ordered.FirstOrDefault(item => item.NotificationId == nextPlanned.NotificationId && item.Schedule?.NotifyTime == nextPlanned.NotifyTime);
            return (normalized.Count, nextRequest);
        }

#if ANDROID
        EnsureAndroidChannels(ordered);
#endif

        foreach (var request in ordered) {
#if ANDROID
            if (request.Android == null) {
                request.Android = new AndroidOptions {
                    Priority = AndroidPriority.Default,
                    ChannelId = "prayer_default",
                    VibrationPattern = BuildVibration(settings.Notifications)
                };
            }
#endif
            await LocalNotificationCenter.Current.Show(request);
        }

        return (ordered.Count, ordered.FirstOrDefault());
    }

    private static string BuildAdhanReminderDescription(PrayerId prayer, int offsetMinutes, AdhanReminderAlertType alertType) {
        var prayerName = LocalizationManager.TranslatePrayer(prayer);
        var directionKey = offsetMinutes < 0 ? "Before" : "After";
        var offset = Math.Abs(offsetMinutes);
        var unit = offset >= 60 && offset % 60 == 0
            ? $"{offset / 60} {LocalizationManager.Translate("Hours")}"
            : $"{offset} {LocalizationManager.Translate("Minutes")}";

        var typeLabel = alertType switch {
            AdhanReminderAlertType.Adhan => LocalizationManager.Translate("ReminderType_Adhan"),
            AdhanReminderAlertType.Notification => LocalizationManager.Translate("ReminderType_Notification"),
            AdhanReminderAlertType.Silent => LocalizationManager.Translate("ReminderType_Silent"),
            _ => LocalizationManager.Translate("ReminderType_Adhan")
        };

        return $"{prayerName} - {LocalizationManager.Translate(directionKey)} {unit} - {typeLabel}";
    }

    private static string? ResolveSystemNotificationSound(NotificationSettings settings, string soundKey, bool isSilent) {
        if (isSilent) {
            return null;
        }

        var sound = AdhanSoundLibrary.ResolveNotificationSound(settings, soundKey);
        if (string.IsNullOrWhiteSpace(sound)) {
            return null;
        }

        if (sound.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) >= 0) {
            return AdhanSoundLibrary.ResolveNotificationSound(settings, AdhanSoundLibrary.DefaultBuiltinKey);
        }

        return sound;
    }

    private static string BuildAndroidChannelId(string soundKey, bool isSilent, bool useRuntimeAdhanPlayback, AdhanReminderAlertType alertType) {
        if (alertType == AdhanReminderAlertType.Notification) {
            return "prayer_notification_v2";
        }

        if (useRuntimeAdhanPlayback) {
            return "prayer_runtime_media_v2";
        }

        if (isSilent) {
            return "prayer_silent_v2";
        }

        return AdhanSoundLibrary.BuildChannelId(soundKey);
    }

    private static bool ShouldUseRuntimeAdhanPlayback() {
        return OperatingSystem.IsAndroid() || OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst() || OperatingSystem.IsWindows();
    }

    private static bool ResolveNotificationSilent(bool useRuntimeAdhanPlayback, bool isSilent) {
#if ANDROID
        _ = useRuntimeAdhanPlayback;
        return isSilent;
#else
        return useRuntimeAdhanPlayback || isSilent;
#endif
    }

#if ANDROID
    private static void EnsureAndroidChannels(IReadOnlyList<NotificationRequest> requests) {
        var channels = requests
            .Select(item => new {
                ChannelId = item.Android?.ChannelId,
                Sound = item.Sound,
                Vibration = item.Android?.VibrationPattern
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.ChannelId))
            .GroupBy(item => item.ChannelId!, StringComparer.OrdinalIgnoreCase)
            .Select(group => {
                var sample = group.First();
                var vibrationPattern = group
                    .Select(item => item.Vibration ?? Array.Empty<long>())
                    .OrderByDescending(pattern => pattern.Length)
                    .FirstOrDefault() ?? Array.Empty<long>();
                var soundFile = ResolveAndroidChannelSound(sample.Sound);
                var isSilentChannel = IsAndroidSilentChannel(group.Key);
                return new NotificationChannelRequest {
                    Id = group.Key,
                    Name = "Prayer Alerts",
                    Description = "Prayer and adhan notifications",
                    Importance = AndroidImportance.High,
                    EnableVibration = vibrationPattern.Length > 0,
                    VibrationPattern = vibrationPattern,
                    EnableSound = !isSilentChannel && !string.IsNullOrWhiteSpace(soundFile),
                    Sound = isSilentChannel ? string.Empty : soundFile ?? string.Empty
                };
            })
            .ToList();

        if (channels.Count == 0) {
            return;
        }

        LocalNotificationCenter.CreateNotificationChannels(channels);
    }

    private static string? ResolveAndroidChannelSound(string? sound) {
        if (string.IsNullOrWhiteSpace(sound)) {
            return null;
        }

        return Path.GetFileName(sound);
    }

    private static bool IsAndroidSilentChannel(string channelId) {
        return channelId.StartsWith("prayer_silent", StringComparison.OrdinalIgnoreCase)
            || channelId.StartsWith("prayer_runtime_media", StringComparison.OrdinalIgnoreCase)
            || channelId.StartsWith("prayer_notification", StringComparison.OrdinalIgnoreCase);
    }
#endif

    private static string BuildScheduleSignature(IReadOnlyList<PrayerDay> days, AppSettings settings) {
        var overrides = settings.Notifications.PrayerOverrides ?? [];
        var reminderItems = settings.Notifications.ReminderItems ?? [];
        var reminderOffsets = settings.Notifications.ReminderOffsetsMinutes ?? [];
        var imsakReminders = settings.FastingReminders.ImsakRemindersMinutes ?? [];
        var iftarReminders = settings.FastingReminders.IftarRemindersMinutes ?? [];

        var daysKey = string.Join(',', days.Select(item =>
            $"{item.Date:yyyyMMdd}:{item.Timings.Fajr:HHmm}:{item.Timings.Dhuhr:HHmm}:{item.Timings.Asr:HHmm}:{item.Timings.Maghrib:HHmm}:{item.Timings.Isha:HHmm}:{item.Timings.Imsak:HHmm}"));

        var overrideKey = string.Join(',', overrides
            .OrderBy(item => item.Prayer)
            .Select(item => $"{(int)item.Prayer}:{item.SoundKey ?? string.Empty}:{(item.EnableVibration.HasValue ? (item.EnableVibration.Value ? "1" : "0") : string.Empty)}"));

        var reminderKey = string.Join(',', reminderItems
            .OrderBy(item => item.OffsetMinutes)
            .ThenBy(item => item.AlertType)
            .Select(item => $"{item.OffsetMinutes}:{(int)item.AlertType}"));

        return string.Join('|',
            settings.Location.Latitude.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
            settings.Location.Longitude.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
            settings.Method,
            settings.Madhhab,
            settings.HighLatitudeRule,
            settings.Notifications.EnableAdhan,
            settings.Notifications.MinutesBefore,
            settings.Notifications.SoundKey,
            settings.Notifications.EnableVibration,
            settings.Notifications.VibrationStrength,
            settings.Notifications.VibrationPattern,
            settings.Notifications.ReminderScope,
            settings.Notifications.ReminderPrayer,
            string.Join(',', reminderOffsets.OrderBy(item => item)),
            reminderKey,
            string.Join(',', imsakReminders.OrderBy(item => item)),
            string.Join(',', iftarReminders.OrderBy(item => item)),
            settings.FastingOffsets.ImsakAdvanceMinutes,
            settings.FastingOffsets.IftarDelayMinutes,
            settings.Offsets.Fajr,
            settings.Offsets.Sunrise,
            settings.Offsets.Dhuhr,
            settings.Offsets.Asr,
            settings.Offsets.Maghrib,
            settings.Offsets.Isha,
            settings.Offsets.Imsak,
            overrideKey,
            daysKey);
    }

    private static AdhanPrayerOverride? FindOverride(IReadOnlyList<AdhanPrayerOverride> overrides, PrayerId prayer) {
        if (overrides == null || overrides.Count == 0) {
            return null;
        }

        for (var i = 0; i < overrides.Count; i++) {
            if (overrides[i].Prayer == prayer) {
                return overrides[i];
            }
        }

        return null;
    }
}
