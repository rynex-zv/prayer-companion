using Microsoft.Maui.Devices;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Models;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.ViewModels;

/// <summary>XAML command adapter over the shared Today application service.</summary>
public sealed class HomeViewModel : TodayApplicationService {
    public HomeViewModel(PrayerDataService dataService, IAppLogger logger) : base(dataService, logger, false) {
        RefreshCommand = new Command(async () => await RefreshAsync());
    }

    public Command RefreshCommand { get; }
}

/// <summary>XAML command adapter over the shared Calendar application service.</summary>
public sealed class CalendarViewModel : CalendarApplicationService {
    private readonly CalendarMonthPresenter _presenter = new();

    public CalendarViewModel(PrayerDataService dataService) : base(dataService, false) {
        LoadCommand = new Command(async () => await LoadAsync());
        PreviousMonthCommand = new Command(() => SelectedMonth = _presenter.MoveMonth(SelectedMonth, -1));
        NextMonthCommand = new Command(() => SelectedMonth = _presenter.MoveMonth(SelectedMonth, 1));
        TodayCommand = new Command(() => SelectedMonth = _presenter.NormalizeMonth(DateTime.Today));
    }

    public Command LoadCommand { get; }
    public Command PreviousMonthCommand { get; }
    public Command NextMonthCommand { get; }
    public Command TodayCommand { get; }
}

/// <summary>XAML selection-command adapter over the shared Qibla application service.</summary>
public sealed class QiblaViewModel : QiblaApplicationService {
    public QiblaViewModel(PrayerDataService dataService) : base(dataService, false) {
        SelectHeadingModeCommand = new Command<OptionItem<QiblaHeadingMode>>(item => {
            if (item is not null) SelectedHeadingMode = item;
        });
        SelectReadingModeCommand = new Command<OptionItem<QiblaReadingMode>>(item => {
            if (item is not null) SelectedReadingMode = item;
        });
        SelectFilterModeCommand = new Command<OptionItem<QiblaFilterMode>>(item => {
            if (item is not null) SelectedFilterMode = item;
        });
    }

    public Command<OptionItem<QiblaHeadingMode>> SelectHeadingModeCommand { get; }
    public Command<OptionItem<QiblaReadingMode>> SelectReadingModeCommand { get; }
    public Command<OptionItem<QiblaFilterMode>> SelectFilterModeCommand { get; }
}

/// <summary>XAML command and vibration adapter over the shared Tasbih application service.</summary>
public sealed class TasbihViewModel : TasbihApplicationService {
    public TasbihViewModel(PrayerDataService dataService, IAppLogger logger) : base(dataService, logger, false) {
        IncrementCommand = new Command(Increment);
        ResetCommand = new Command(() => {
            Reset();
            TryVibrateReset();
        });
    }

    public Command IncrementCommand { get; }
    public Command ResetCommand { get; }

    private static void TryVibrateReset() {
        try {
            Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(80));
        } catch (FeatureNotSupportedException) {
        } catch {
        }
    }
}
