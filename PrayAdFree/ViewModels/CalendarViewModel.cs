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
                    Dhuhr = TimeFormatHelper.FormatTime(day.Timings.Dhuhr, settings.ClockFormat),
                    Asr = TimeFormatHelper.FormatTime(day.Timings.Asr, settings.ClockFormat),
                    Maghrib = TimeFormatHelper.FormatTime(day.Timings.Maghrib, settings.ClockFormat),
                    Isha = TimeFormatHelper.FormatTime(day.Timings.Isha, settings.ClockFormat)
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
