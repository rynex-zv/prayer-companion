using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.ViewModels;

public sealed class CalendarViewModel : ViewModelBase, ICalendarProjectionSource {
    private readonly PrayerDataService _dataService;
    private readonly CalendarMonthPresenter _presenter = new();
    private DateTime _selectedMonth;
    private bool _isBusy;
    private bool _reloadPending;
    private string _statusMessage = "";

    public CalendarViewModel(PrayerDataService dataService) {
        _dataService = dataService;
        _selectedMonth = _presenter.NormalizeMonth(DateTime.Today);
        LoadCommand = new Command(QueueReload);
        PreviousMonthCommand = new Command(() => SelectedMonth = _presenter.MoveMonth(SelectedMonth, -1));
        NextMonthCommand = new Command(() => SelectedMonth = _presenter.MoveMonth(SelectedMonth, 1));
        TodayCommand = new Command(() => {
            var todayMonth = _presenter.NormalizeMonth(DateTime.Today);
            if (todayMonth == SelectedMonth) {
                QueueReload();
                return;
            }

            SelectedMonth = todayMonth;
        });
        Days = new ObservableCollection<CalendarDayRow>();
        _dataService.SettingsChanged += (_, _) => {
            MainThread.BeginInvokeOnMainThread(QueueReload);
        };
    }

    public ObservableCollection<CalendarDayRow> Days { get; }
    IReadOnlyList<CalendarDayRow> ICalendarProjectionSource.Days => Days;
    public Command LoadCommand { get; }
    public Command PreviousMonthCommand { get; }
    public Command NextMonthCommand { get; }
    public Command TodayCommand { get; }

    public DateTime SelectedMonth {
        get => _selectedMonth;
        set {
            var normalized = _presenter.NormalizeMonth(value);
            if (SetProperty(ref _selectedMonth, normalized)) {
                QueueReload();
            }
        }
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
            _reloadPending = true;
            return;
        }

        do {
            _reloadPending = false;

            try {
                IsBusy = true;
                StatusMessage = "Loading month...";
                var settings = _dataService.LoadSettings();
                var month = await _dataService.GetMonthAsync(settings, SelectedMonth, CancellationToken.None);
                var rows = _presenter.BuildRows(month, settings);

                Days.Clear();
                foreach (var row in rows) {
                    Days.Add(row);
                }

                StatusMessage = $"Loaded {rows.Count} days";
            } catch (Exception ex) {
                Days.Clear();
                StatusMessage = $"Failed to load: {ex.Message}";
            } finally {
                IsBusy = false;
            }
        } while (_reloadPending);
    }

    private void QueueReload() {
        _reloadPending = true;
        _ = LoadAsync();
    }
}
