using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Models;
using Pray_Ad_Free.Services;
using System.Collections.ObjectModel;

namespace Pray_Ad_Free.ViewModels;

public sealed class QiblaViewModel : ViewModelBase {
    private readonly PrayerDataService _dataService;
    private double _bearing;
    private double _heading;
    private double _needleRotation;
    private double _compassRotation;
    private string _locationTitle = "";
    private string _statusMessage = "";
    private LocationSettings? _location;
    private OptionItem<QiblaReadingMode>? _selectedReadingMode;
    private OptionItem<QiblaFilterMode>? _selectedFilterMode;
    private bool _suspendPreferenceSave;
    private Command<OptionItem<QiblaReadingMode>>? _selectReadingModeCommand;
    private Command<OptionItem<QiblaFilterMode>>? _selectFilterModeCommand;

    public QiblaViewModel(PrayerDataService dataService) {
        _dataService = dataService;
        ReadingModes = new ObservableCollection<OptionItem<QiblaReadingMode>>();
        FilterModes = new ObservableCollection<OptionItem<QiblaFilterMode>>();
        BuildOptions();
        SelectReadingModeCommand = new Command<OptionItem<QiblaReadingMode>>(item => {
            if (item != null) {
                SelectedReadingMode = item;
            }
        });
        SelectFilterModeCommand = new Command<OptionItem<QiblaFilterMode>>(item => {
            if (item != null) {
                SelectedFilterMode = item;
            }
        });
        LocalizationManager.LanguageChanged += (_, _) => {
            BuildOptions();
            LoadPreferences();
        };
    }

    public ObservableCollection<OptionItem<QiblaReadingMode>> ReadingModes { get; }
    public ObservableCollection<OptionItem<QiblaFilterMode>> FilterModes { get; }
    public Command<OptionItem<QiblaReadingMode>> SelectReadingModeCommand {
        get => _selectReadingModeCommand!;
        private set => _selectReadingModeCommand = value;
    }
    public Command<OptionItem<QiblaFilterMode>> SelectFilterModeCommand {
        get => _selectFilterModeCommand!;
        private set => _selectFilterModeCommand = value;
    }

    public OptionItem<QiblaReadingMode>? SelectedReadingMode {
        get => _selectedReadingMode;
        set {
            if (SetProperty(ref _selectedReadingMode, value)) {
                SavePreferences();
            }
        }
    }

    public OptionItem<QiblaFilterMode>? SelectedFilterMode {
        get => _selectedFilterMode;
        set {
            if (SetProperty(ref _selectedFilterMode, value)) {
                SavePreferences();
            }
        }
    }

    public double Bearing {
        get => _bearing;
        set => SetProperty(ref _bearing, value);
    }

    public double Heading {
        get => _heading;
        set => SetProperty(ref _heading, value);
    }

    public double NeedleRotation {
        get => _needleRotation;
        set => SetProperty(ref _needleRotation, value);
    }

    public double CompassRotation {
        get => _compassRotation;
        set => SetProperty(ref _compassRotation, value);
    }

    public string LocationTitle {
        get => _locationTitle;
        set => SetProperty(ref _locationTitle, value);
    }

    public string StatusMessage {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public LocationSettings? Location => _location;

    public async Task LoadAsync() {
        StatusMessage = LocalizationManager.Translate("FindingLocation");
        LoadPreferences();
        var settings = _dataService.LoadSettings();
        var updated = await _dataService.UpdateLocationAsync(settings, CancellationToken.None);
        _location = updated.Location;
        if (_location != null) {
            Bearing = QiblaCalculator.CalculateBearing(_location.Latitude, _location.Longitude);
            LocationTitle = $"{_location.City}, {_location.Country}".Trim(' ', ',');
            UpdateNeedle();
            StatusMessage = LocalizationManager.Translate("CompassCalibrationHint");
        }
    }

    public void UpdateHeading(double heading) {
        Heading = heading;
        UpdateNeedle();
    }

    private void UpdateNeedle() {
        NeedleRotation = (Bearing + 360) % 360;
        CompassRotation = (-Heading + 360) % 360;
    }

    private void BuildOptions() {
        ReadingModes.Clear();
        ReadingModes.Add(new OptionItem<QiblaReadingMode>(QiblaReadingMode.Smooth, LocalizationManager.Translate("CompassReading_Smooth")));
        ReadingModes.Add(new OptionItem<QiblaReadingMode>(QiblaReadingMode.Balanced, LocalizationManager.Translate("CompassReading_Balanced")));
        ReadingModes.Add(new OptionItem<QiblaReadingMode>(QiblaReadingMode.Fast, LocalizationManager.Translate("CompassReading_Fast")));
        ReadingModes.Add(new OptionItem<QiblaReadingMode>(QiblaReadingMode.Raw, LocalizationManager.Translate("CompassReading_Raw")));

        FilterModes.Clear();
        FilterModes.Add(new OptionItem<QiblaFilterMode>(QiblaFilterMode.Off, LocalizationManager.Translate("CompassFilter_Off")));
        FilterModes.Add(new OptionItem<QiblaFilterMode>(QiblaFilterMode.Normal, LocalizationManager.Translate("CompassFilter_Normal")));
        FilterModes.Add(new OptionItem<QiblaFilterMode>(QiblaFilterMode.Strict, LocalizationManager.Translate("CompassFilter_Strict")));
    }

    private void LoadPreferences() {
        var settings = _dataService.LoadSettings();
        _suspendPreferenceSave = true;
        try {
            SelectedReadingMode = ReadingModes.FirstOrDefault(item => item.Value == settings.Qibla.ReadingMode)
                ?? ReadingModes.FirstOrDefault();
            SelectedFilterMode = FilterModes.FirstOrDefault(item => item.Value == settings.Qibla.FilterMode)
                ?? FilterModes.FirstOrDefault();
        } finally {
            _suspendPreferenceSave = false;
        }
    }

    private void SavePreferences() {
        if (_suspendPreferenceSave) {
            return;
        }

        var settings = _dataService.LoadSettings();
        settings = new AppSettings {
            Location = settings.Location,
            Method = settings.Method,
            Madhhab = settings.Madhhab,
            HighLatitudeRule = settings.HighLatitudeRule,
            Offsets = settings.Offsets,
            FastingOffsets = settings.FastingOffsets,
            FastingReminders = settings.FastingReminders,
            Notifications = settings.Notifications,
            Qibla = new QiblaPreferences {
                ReadingMode = SelectedReadingMode?.Value ?? QiblaReadingMode.Balanced,
                FilterMode = SelectedFilterMode?.Value ?? QiblaFilterMode.Normal
            },
            ClockFormat = settings.ClockFormat,
            TextScale = settings.TextScale,
            Tasbih = settings.Tasbih,
            Language = settings.Language,
            LanguageSelected = settings.LanguageSelected,
            ThemeMode = settings.ThemeMode,
            ThemeVariant = settings.ThemeVariant,
            AccentIndex = settings.AccentIndex
        };
        _dataService.SaveSettings(settings);
    }
}
