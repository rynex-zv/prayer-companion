using System.Collections.ObjectModel;
using System.Linq;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Models;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.ViewModels;

public sealed class HomeViewModel : ViewModelBase {
    private readonly PrayerDataService _dataService;
    private PrayerDay? _today;
    private PrayerId _nextPrayerId;
    private DateTime _nextPrayerTime;
    private string _locationTitle = "";
    private string _hijriDate = "";
    private string _gregorianDate = "";
    private string _nextPrayerName = "";
    private string _nextPrayerClock = "";
    private string _countdown = "";
    private string _statusMessage = "";
    private string _imsakTime = "";
    private string _iftarTime = "";
    private bool _isBusy;

    public HomeViewModel(PrayerDataService dataService) {
        _dataService = dataService;
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
            return;
        }

        try {
            IsBusy = true;
            StatusMessage = "Updating times...";
            var settings = _dataService.LoadSettings();
            var month = await _dataService.GetMonthAsync(settings, DateTime.Today, CancellationToken.None);
            var today = month.Days.FirstOrDefault(day => day.Date == DateOnly.FromDateTime(DateTime.Today));
            _today = today;
            if (today == null) {
                StatusMessage = "Unable to load prayer times.";
                return;
            }

            LocationTitle = BuildLocation(settings.Location);
            HijriDate = today.Hijri.Date;
            GregorianDate = DateTime.Today.ToString("dddd, dd MMM yyyy");

            UpdateNextPrayer(DateTime.Now);
            BuildRows();
            await _dataService.ScheduleNotificationsAsync(settings, month, CancellationToken.None);
            StatusMessage = $"Last updated {DateTime.Now:t}";
        } finally {
            IsBusy = false;
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
        NextPrayerClock = _nextPrayerTime.ToString("t");
    }

    private void BuildRows() {
        if (_today == null) {
            return;
        }

        TodayTimings.Clear();
        foreach (var prayer in Enum.GetValues<PrayerId>()) {
            if (prayer == PrayerId.Imsak) {
                continue;
            }

            var time = _today.Timings.Get(prayer);
            TodayTimings.Add(new PrayerTimeRow {
                Id = prayer,
                Name = LocalizationManager.TranslatePrayer(prayer),
                Time = time.ToString("t"),
                IsNext = prayer == _nextPrayerId
            });
        }

        ImsakTime = _today.Timings.Imsak.ToString("t");
        IftarTime = _today.Timings.Maghrib.ToString("t");
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
