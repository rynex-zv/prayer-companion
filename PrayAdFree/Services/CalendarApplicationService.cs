using System.Collections.ObjectModel;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.Services;

public class CalendarApplicationService : ObservableApplicationService, ICalendarProjectionSource {
    private readonly PrayerDataService _dataService;
    private readonly CalendarMonthPresenter _presenter = new();
    private DateTime _selectedMonth;
    private bool _isBusy;
    private bool _reloadPending;
    private string _statusMessage = "";

    public CalendarApplicationService(PrayerDataService dataService) : this(dataService, true) { }

    protected CalendarApplicationService(PrayerDataService dataService, bool observeAppChanges) {
        _dataService = dataService;
        _selectedMonth = _presenter.NormalizeMonth(DateTime.Today);
        Days = new ObservableCollection<CalendarDayRow>();
        if (observeAppChanges) _dataService.SettingsChanged += OnSettingsChanged;
    }

    public ObservableCollection<CalendarDayRow> Days { get; }
    IReadOnlyList<CalendarDayRow> ICalendarProjectionSource.Days => Days;

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

    private async void OnSettingsChanged(object? sender, EventArgs args) {
        // Keep persistence on the mutation's critical path, but refresh this projection later.
        await Task.Yield();
        QueueReload();
    }

    private void QueueReload() {
        _reloadPending = true;
        _ = LoadAsync();
    }
}
