using System.Collections.ObjectModel;
using PrayAdFree.Core.Models;
using Pray_Ad_Free.Models;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.ViewModels;

public sealed class CalendarViewModel : ViewModelBase {
    private readonly PrayerDataService _dataService;
    private DateTime _selectedMonth;
    private bool _isBusy;
    private string _statusMessage = "";

    public CalendarViewModel(PrayerDataService dataService) {
        _dataService = dataService;
        _selectedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        LoadCommand = new Command(async () => await LoadAsync());
        Days = new ObservableCollection<PrayerDayRow>();
    }

    public ObservableCollection<PrayerDayRow> Days { get; }
    public Command LoadCommand { get; }

    public DateTime SelectedMonth {
        get => _selectedMonth;
        set => SetProperty(ref _selectedMonth, value);
    }

    public string StatusMessage {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public async Task LoadAsync() {
        if (IsBusy) {
            return;
        }

        try {
            IsBusy = true;
            StatusMessage = "Loading month...";
            var settings = _dataService.LoadSettings();
            var month = await _dataService.GetMonthAsync(settings, SelectedMonth, CancellationToken.None);
            Days.Clear();
            foreach (var day in month.Days) {
                Days.Add(new PrayerDayRow {
                    Date = day.Date.ToString("dd MMM"),
                    Hijri = day.Hijri.Date,

                    Fajr = TimeFormatHelper.FormatTime(day.Timings.Fajr, settings.ClockFormat),
                    FajrBase = TimeFormatHelper.FormatTime(day.Timings.Fajr.AddMinutes(-settings.Offsets.Fajr), settings.ClockFormat),
                    ShowFajrBase = settings.Offsets.Fajr != 0,
                    Sunrise = TimeFormatHelper.FormatTime(day.Timings.Sunrise, settings.ClockFormat),

                    Dhuhr = TimeFormatHelper.FormatTime(day.Timings.Dhuhr, settings.ClockFormat),
                    DhuhrBase = TimeFormatHelper.FormatTime(day.Timings.Dhuhr.AddMinutes(-settings.Offsets.Dhuhr), settings.ClockFormat),
                    ShowDhuhrBase = settings.Offsets.Dhuhr != 0,

                    Asr = TimeFormatHelper.FormatTime(day.Timings.Asr, settings.ClockFormat),
                    AsrBase = TimeFormatHelper.FormatTime(day.Timings.Asr.AddMinutes(-settings.Offsets.Asr), settings.ClockFormat),
                    ShowAsrBase = settings.Offsets.Asr != 0,

                    Maghrib = TimeFormatHelper.FormatTime(day.Timings.Maghrib, settings.ClockFormat),
                    MaghribBase = TimeFormatHelper.FormatTime(day.Timings.Maghrib.AddMinutes(-settings.Offsets.Maghrib), settings.ClockFormat),
                    ShowMaghribBase = settings.Offsets.Maghrib != 0,

                    Isha = TimeFormatHelper.FormatTime(day.Timings.Isha, settings.ClockFormat),
                    IshaBase = TimeFormatHelper.FormatTime(day.Timings.Isha.AddMinutes(-settings.Offsets.Isha), settings.ClockFormat),
                    ShowIshaBase = settings.Offsets.Isha != 0
                });
            }

            StatusMessage = $"Loaded {month.Days.Count} days";
        } catch (Exception ex) {
            Days.Clear();
            StatusMessage = $"Failed to load: {ex.Message}";
        } finally {
            IsBusy = false;
        }
    }
}
