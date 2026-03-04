using PrayAdFree.Core.Models;
using Pray_Ad_Free.Models;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.ViewModels;

public sealed class AdhanPrayerOverrideViewModel : ViewModelBase {
    private OptionItem<string>? _selectedSound;
    private OptionItem<int>? _selectedVibration;

    public AdhanPrayerOverrideViewModel(PrayerId prayer) {
        Prayer = prayer;
    }

    public PrayerId Prayer { get; }

    public string Name => LocalizationManager.TranslatePrayer(Prayer);

    public OptionItem<string>? SelectedSound {
        get => _selectedSound;
        set => SetProperty(ref _selectedSound, value);
    }

    public OptionItem<int>? SelectedVibration {
        get => _selectedVibration;
        set => SetProperty(ref _selectedVibration, value);
    }

    public void RefreshLocalization() {
        OnPropertyChanged(nameof(Name));
    }
}
