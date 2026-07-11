using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Models;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.ViewModels;

public sealed class HomeViewModel : ViewModelBase {
    private readonly PrayerDataService _dataService;
    private readonly IAppLogger _logger;
    private readonly WidgetSnapshotFactory _widgetSnapshotFactory = new();
    private PrayerDay? _today;
    private PrayerDay? _tomorrow;
    private PrayerId _nextPrayerId;
    private DateTime _nextPrayerTime;
    private AppSettings _settings = new();
    private string _locationTitle = "";
    private string _hijriDate = "";
    private string _gregorianDate = "";
    private string _nextPrayerName = "";
    private string _nextPrayerClock = "";
    private string _nextPrayerBaseClock = "";
    private string _nextPrayerDayLabel = "";
    private bool _showNextPrayerBaseClock;
    private string _countdown = "";
    private string _statusMessage = "";
    private string _imsakTime = "";
    private string _iftarTime = "";
    private bool _isImsakNext;
    private bool _isIftarNext;
    private string _nextFastingCountdown = "--:--:--";
    private bool _isBusy;
    private string _lastScheduleKey = "";
    private bool _refreshPending;
    private DateTime _imsakDateTime;
    private DateTime _iftarDateTime;
    private DateTime _tomorrowImsakDateTime;

    public HomeViewModel(PrayerDataService dataService, IAppLogger logger) {
        _dataService = dataService;
        _logger = logger;
        RefreshCommand = new Command(async () => await RefreshAsync());
        TodayTimings = new ObservableCollection<PrayerTimeRow>();
        LocalizationManager.LanguageChanged += (_, _) => RefreshLocalization();
        _dataService.SettingsChanged += (_, _) => MainThread.BeginInvokeOnMainThread(async () => await RefreshAsync());
    }

    public ObservableCollection<PrayerTimeRow> TodayTimings { get; }
    public PrayerId NextPrayerId => _nextPrayerId;
    public string NextPrayerDayId => _nextPrayerTime.Date > DateTime.Now.Date ? "tomorrow" : "today";
    public Command RefreshCommand { get; }
    public ClockFormat CurrentClockFormat => _settings.ClockFormat;

    public string LocationTitle {
        get => _locationTitle;
        set => SetProperty(ref _locationTitle, value);
    }

    public string HijriDate {
        get => _hijriDate;
        set => SetProperty(ref _hijriDate, value);
    }

    public string GregorianDate {
        get => _gregorianDate;
        set => SetProperty(ref _gregorianDate, value);
    }

    public string NextPrayerName {
        get => _nextPrayerName;
        set => SetProperty(ref _nextPrayerName, value);
    }

    public string NextPrayerClock {
        get => _nextPrayerClock;
        set => SetProperty(ref _nextPrayerClock, value);
    }

    public string NextPrayerDayLabel {
        get => _nextPrayerDayLabel;
        set => SetProperty(ref _nextPrayerDayLabel, value);
    }

    public string NextPrayerBaseClock {
        get => _nextPrayerBaseClock;
        set => SetProperty(ref _nextPrayerBaseClock, value);
    }

    public bool ShowNextPrayerBaseClock {
        get => _showNextPrayerBaseClock;
        set => SetProperty(ref _showNextPrayerBaseClock, value);
    }

    public string Countdown {
        get => _countdown;
        set => SetProperty(ref _countdown, value);
    }

    public string StatusMessage {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string ImsakTime {
        get => _imsakTime;
        set => SetProperty(ref _imsakTime, value);
    }

    public string IftarTime {
        get => _iftarTime;
        set => SetProperty(ref _iftarTime, value);
    }

    public bool IsImsakNext {
        get => _isImsakNext;
        set => SetProperty(ref _isImsakNext, value);
    }

    public bool IsIftarNext {
        get => _isIftarNext;
        set => SetProperty(ref _isIftarNext, value);
    }

    public string NextFastingCountdown {
        get => _nextFastingCountdown;
        set => SetProperty(ref _nextFastingCountdown, value);
    }

    public bool IsBusy {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public async Task RefreshAsync() {
        if (IsBusy) {
            _refreshPending = true;
            return;
        }

        try {
            IsBusy = true;
            _refreshPending = false;
            if (!string.Equals(StatusMessage, "Updating times...", StringComparison.Ordinal)) {
                StatusMessage = "Updating times...";
            }
            _settings = _dataService.LoadSettings();
            var month = await _dataService.GetMonthAsync(_settings, DateTime.Today, CancellationToken.None);
            var today = month.Days.FirstOrDefault(day => day.Date == DateOnly.FromDateTime(DateTime.Today));
            _today = today;
            if (today == null) {
                StatusMessage = "Unable to load prayer times.";
                return;
            }

            LocationTitle = BuildLocation(_settings.Location);
            HijriDate = today.Hijri.Date;
            GregorianDate = DateTime.Today.ToString("dddd, dd MMM yyyy");
            _tomorrow = month.Days.FirstOrDefault(day => day.Date == today.Date.AddDays(1));

            ApplySnapshots(DateTime.Now);
            if (ShouldScheduleNotifications(_settings)) {
                try {
                    await _dataService.ScheduleNotificationsAsync(_settings, month, CancellationToken.None, requestPermissions: false);
                } catch (Exception ex) {
                    _logger.LogException(ex, "HomeViewModel.ScheduleNotifications");
                    StatusMessage = "Notifications update failed.";
                }
            }
            StatusMessage = $"Last updated {DateTime.Now:t}";
        } catch (Exception ex) {
            _logger.LogException(ex, "HomeViewModel.RefreshAsync");
            StatusMessage = "Update failed.";
        } finally {
            IsBusy = false;
            if (string.Equals(StatusMessage, "Updating times...", StringComparison.Ordinal)) {
                StatusMessage = $"Last updated {DateTime.Now:t}";
            }

            if (_refreshPending) {
                _ = MainThread.InvokeOnMainThreadAsync(async () => await RefreshAsync());
            }
        }
    }

    public void UpdateCountdown(DateTime now) {
        if (_today == null) {
            Countdown = "--:--";
            return;
        }

        if (now >= _nextPrayerTime) {
            ApplySnapshots(now);
        }

        var remaining = _nextPrayerTime - now;
        if (remaining < TimeSpan.Zero) {
            remaining = TimeSpan.Zero;
        }

        Countdown = $"{remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        UpdateFastingCountdown(now);
    }

    private void ApplySnapshots(DateTime now) {
        if (_today == null) {
            return;
        }

        var snapshot = _widgetSnapshotFactory.Build(_today, _tomorrow, _settings, now);
        ApplyPrayerSnapshot(snapshot.DailyPrayer, now);
        BuildRows(snapshot.DailyPrayer);
        ApplyFastingSnapshot(snapshot.Fasting);
    }

    private void ApplyPrayerSnapshot(DailyPrayerSnapshot snapshot, DateTime now) {
        _nextPrayerId = snapshot.NextPrayerId;
        _nextPrayerTime = snapshot.NextPrayerTime;
        NextPrayerName = LocalizationManager.TranslatePrayer(_nextPrayerId);
        NextPrayerClock = TimeFormatHelper.FormatTime(_nextPrayerTime, _settings.ClockFormat);
        NextPrayerDayLabel = ResolveNextPrayerDayLabel(now, _nextPrayerTime);
        ShowNextPrayerBaseClock = snapshot.NextPrayerBaseTime.HasValue;
        NextPrayerBaseClock = snapshot.NextPrayerBaseTime.HasValue
            ? TimeFormatHelper.FormatTime(snapshot.NextPrayerBaseTime.Value, _settings.ClockFormat)
            : string.Empty;
    }

    private void BuildRows(DailyPrayerSnapshot snapshot) {
        TodayTimings.Clear();
        foreach (var entry in snapshot.Entries) {
            TodayTimings.Add(new PrayerTimeRow {
                Id = entry.Prayer,
                Name = LocalizationManager.TranslatePrayer(entry.Prayer),
                Time = TimeFormatHelper.FormatTime(entry.AdjustedTime, _settings.ClockFormat),
                BaseTime = TimeFormatHelper.FormatTime(entry.BaseTime, _settings.ClockFormat),
                ShowBaseTime = entry.ShowBaseTime,
                IsNext = entry.IsNext
            });
        }

#if DEBUG
        _logger.LogEvent("HomeRows",
            $"count={TodayTimings.Count};next={_nextPrayerId};rows={string.Join(",", TodayTimings.Select(row => $"{row.Id}:{row.Time}"))}");
#endif
    }

    private void ApplyFastingSnapshot(FastingSnapshot snapshot) {
        _imsakDateTime = snapshot.ImsakTime;
        _iftarDateTime = snapshot.IftarTime;
        _tomorrowImsakDateTime = _tomorrow != null
            ? _tomorrow.Timings.Imsak.AddMinutes(-_settings.FastingOffsets.ImsakAdvanceMinutes)
            : snapshot.ImsakTime.AddDays(1);
        ImsakTime = TimeFormatHelper.FormatTime(snapshot.ImsakTime, _settings.ClockFormat);
        IftarTime = TimeFormatHelper.FormatTime(snapshot.IftarTime, _settings.ClockFormat);
        IsImsakNext = snapshot.IsImsakNext;
        IsIftarNext = snapshot.IsIftarNext;
        var totalHours = (int)Math.Floor(snapshot.Remaining.TotalHours);
        NextFastingCountdown = $"{totalHours:00}:{snapshot.Remaining.Minutes:00}:{snapshot.Remaining.Seconds:00}";
    }

    private void UpdateFastingCountdown(DateTime now) {
        if (_imsakDateTime == default || _iftarDateTime == default) {
            IsImsakNext = false;
            IsIftarNext = false;
            NextFastingCountdown = "--:--:--";
            return;
        }

        DateTime nextTarget;
        if (now < _imsakDateTime) {
            IsImsakNext = true;
            IsIftarNext = false;
            nextTarget = _imsakDateTime;
        } else if (now < _iftarDateTime) {
            IsImsakNext = false;
            IsIftarNext = true;
            nextTarget = _iftarDateTime;
        } else {
            IsImsakNext = true;
            IsIftarNext = false;
            nextTarget = _tomorrowImsakDateTime > now ? _tomorrowImsakDateTime : _imsakDateTime.AddDays(1);
        }

        var remaining = nextTarget - now;
        if (remaining < TimeSpan.Zero) {
            remaining = TimeSpan.Zero;
        }

        var totalHours = (int)Math.Floor(remaining.TotalHours);
        NextFastingCountdown = $"{totalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
    }

    private static string ResolveNextPrayerDayLabel(DateTime now, DateTime nextPrayer) {
        if (nextPrayer.Date <= now.Date) {
            return LocalizationManager.Translate("Today");
        }

        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch {
            "ar" => "غدًا",
            "tr" => "Yarin",
            "fr" => "Demain",
            _ => "Tomorrow"
        };
    }

    private bool ShouldScheduleNotifications(AppSettings settings) {
        var overrides = settings.Notifications.PrayerOverrides ?? [];
        var reminderItems = settings.Notifications.ReminderItems ?? [];
        var reminderOffsets = settings.Notifications.ReminderOffsetsMinutes ?? [];
        var imsakReminders = settings.FastingReminders.ImsakRemindersMinutes ?? [];
        var iftarReminders = settings.FastingReminders.IftarRemindersMinutes ?? [];
        var deferredReminder = settings.Notifications.PendingDeferredReminder;

        var overrideKey = string.Join(',', overrides
            .OrderBy(item => item.Prayer)
            .Select(item => $"{(int)item.Prayer}:{item.SoundKey ?? ""}:{(item.EnableVibration.HasValue ? (item.EnableVibration.Value ? "1" : "0") : "")}"));

        var key = string.Join('|',
            DateOnly.FromDateTime(DateTime.Today).ToString("yyyyMMdd"),
            settings.Location.Latitude.ToString("F4"),
            settings.Location.Longitude.ToString("F4"),
            settings.Method,
            settings.Madhhab,
            settings.HighLatitudeRule,
            settings.Offsets.Fajr,
            settings.Offsets.Sunrise,
            settings.Offsets.Dhuhr,
            settings.Offsets.Asr,
            settings.Offsets.Maghrib,
            settings.Offsets.Isha,
            settings.Offsets.Imsak,
            settings.FastingOffsets.ImsakAdvanceMinutes,
            settings.FastingOffsets.IftarDelayMinutes,
            settings.Notifications.EnableAdhan,
            settings.Notifications.MinutesBefore,
            settings.Notifications.SoundKey,
            settings.Notifications.EnableVibration,
            settings.Notifications.VibrationStrength,
            settings.Notifications.VibrationPattern,
            settings.Notifications.ReminderScope,
            settings.Notifications.ReminderPrayer,
            deferredReminder?.NotifyTime.ToString("O") ?? string.Empty,
            deferredReminder?.Prayer.ToString() ?? string.Empty,
            deferredReminder?.SoundKey ?? string.Empty,
            overrideKey,
            string.Join(',', reminderOffsets.OrderBy(item => item)),
            string.Join(',', reminderItems.OrderBy(item => item.OffsetMinutes).ThenBy(item => item.AlertType).Select(item => $"{item.OffsetMinutes}:{(int)item.AlertType}")),
            string.Join(',', imsakReminders.OrderBy(item => item)),
            string.Join(',', iftarReminders.OrderBy(item => item))
        );

        if (string.Equals(_lastScheduleKey, key, StringComparison.Ordinal)) {
            return false;
        }

        _lastScheduleKey = key;
        return true;
    }

    private void RefreshLocalization() {
        if (_today == null) {
            return;
        }

        ApplySnapshots(DateTime.Now);
    }

    private static string BuildLocation(LocationSettings location) {
        if (!string.IsNullOrWhiteSpace(location.City) && !string.IsNullOrWhiteSpace(location.Country)) {
            return $"{location.City}, {location.Country}";
        }

        return "Current location";
    }
}
