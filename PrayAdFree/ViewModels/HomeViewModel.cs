using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Models;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.ViewModels;

public sealed class HomeViewModel : ViewModelBase {
    private static readonly PrayerId[] DisplayOrder = {
        PrayerId.Fajr,
        PrayerId.Sunrise,
        PrayerId.Dhuhr,
        PrayerId.Asr,
        PrayerId.Maghrib,
        PrayerId.Isha
    };

    private readonly PrayerDataService _dataService;
    private readonly IAppLogger _logger;
    private PrayerDay? _today;
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
    private bool _isBusy;
    private string _lastScheduleKey = "";
    private bool _refreshPending;

    public HomeViewModel(PrayerDataService dataService, IAppLogger logger) {
        _dataService = dataService;
        _logger = logger;
        RefreshCommand = new Command(async () => await RefreshAsync());
        TodayTimings = new ObservableCollection<PrayerTimeRow>();
        LocalizationManager.LanguageChanged += (_, _) => RefreshLocalization();
        _dataService.SettingsChanged += (_, _) => MainThread.BeginInvokeOnMainThread(async () => await RefreshAsync());
    }

    public ObservableCollection<PrayerTimeRow> TodayTimings { get; }
    public Command RefreshCommand { get; }

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

            UpdateNextPrayer(DateTime.Now);
            BuildRows();
            if (ShouldScheduleNotifications(_settings)) {
                try {
                    await _dataService.ScheduleNotificationsAsync(_settings, month, CancellationToken.None);
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
            UpdateNextPrayer(now);
            BuildRows();
        }

        var remaining = _nextPrayerTime - now;
        if (remaining < TimeSpan.Zero) {
            remaining = TimeSpan.Zero;
        }

        Countdown = $"{remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
    }

    private void UpdateNextPrayer(DateTime now) {
        if (_today == null) {
            return;
        }

        (_nextPrayerId, _nextPrayerTime) = NextPrayerCalculator.GetNext(_today, now);
        NextPrayerName = LocalizationManager.TranslatePrayer(_nextPrayerId);
        NextPrayerClock = TimeFormatHelper.FormatTime(_nextPrayerTime, _settings.ClockFormat);
        NextPrayerDayLabel = ResolveNextPrayerDayLabel(now, _nextPrayerTime);

        var offset = GetOffsetForPrayer(_nextPrayerId);
        ShowNextPrayerBaseClock = offset != 0;
        NextPrayerBaseClock = ShowNextPrayerBaseClock
            ? TimeFormatHelper.FormatTime(_nextPrayerTime.AddMinutes(-offset), _settings.ClockFormat)
            : string.Empty;
    }

    private void BuildRows() {
        if (_today == null) {
            return;
        }

        TodayTimings.Clear();
        var seen = new HashSet<PrayerId>();
        foreach (var prayer in DisplayOrder) {
            // Guard against any accidental duplicate insertion.
            if (!seen.Add(prayer)) {
                continue;
            }

            var adjustedTime = _today.Timings.Get(prayer);
            var offset = GetOffsetForPrayer(prayer);
            var baseTime = adjustedTime.AddMinutes(-offset);
            TodayTimings.Add(new PrayerTimeRow {
                Id = prayer,
                Name = LocalizationManager.TranslatePrayer(prayer),
                Time = TimeFormatHelper.FormatTime(adjustedTime, _settings.ClockFormat),
                BaseTime = TimeFormatHelper.FormatTime(baseTime, _settings.ClockFormat),
                ShowBaseTime = offset != 0,
                IsNext = prayer == _nextPrayerId
            });
        }

        var imsak = _today.Timings.Imsak.AddMinutes(-_settings.FastingOffsets.ImsakAdvanceMinutes);
        var iftar = _today.Timings.Maghrib.AddMinutes(_settings.FastingOffsets.IftarDelayMinutes);
        ImsakTime = TimeFormatHelper.FormatTime(imsak, _settings.ClockFormat);
        IftarTime = TimeFormatHelper.FormatTime(iftar, _settings.ClockFormat);

#if DEBUG
        _logger.LogEvent("HomeRows",
            $"count={TodayTimings.Count};next={_nextPrayerId};rows={string.Join(",", TodayTimings.Select(row => $"{row.Id}:{row.Time}"))}");
#endif
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

    private int GetOffsetForPrayer(PrayerId prayer) {
        return prayer switch {
            PrayerId.Fajr => _settings.Offsets.Fajr,
            PrayerId.Sunrise => _settings.Offsets.Sunrise,
            PrayerId.Dhuhr => _settings.Offsets.Dhuhr,
            PrayerId.Asr => _settings.Offsets.Asr,
            PrayerId.Maghrib => _settings.Offsets.Maghrib,
            PrayerId.Isha => _settings.Offsets.Isha,
            PrayerId.Imsak => _settings.Offsets.Imsak,
            _ => 0
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

        NextPrayerName = LocalizationManager.TranslatePrayer(_nextPrayerId);
        BuildRows();
    }

    private static string BuildLocation(LocationSettings location) {
        if (!string.IsNullOrWhiteSpace(location.City) && !string.IsNullOrWhiteSpace(location.Country)) {
            return $"{location.City}, {location.Country}";
        }

        return "Current location";
    }
}
