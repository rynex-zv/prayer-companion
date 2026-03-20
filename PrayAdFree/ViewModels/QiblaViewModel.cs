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
    private double _manualHeading;
    private double _needleRotation;
    private double _compassRotation;
    private string _locationTitle = "";
    private string _directionLabel = "";
    private string _statusMessage = "";
    private LocationSettings? _location;
    private OptionItem<QiblaHeadingMode>? _selectedHeadingMode;
    private OptionItem<QiblaReadingMode>? _selectedReadingMode;
    private OptionItem<QiblaFilterMode>? _selectedFilterMode;
    private bool _suspendPreferenceSave;
    private Command<OptionItem<QiblaHeadingMode>>? _selectHeadingModeCommand;
    private Command<OptionItem<QiblaReadingMode>>? _selectReadingModeCommand;
    private Command<OptionItem<QiblaFilterMode>>? _selectFilterModeCommand;

    public QiblaViewModel(PrayerDataService dataService) {
        _dataService = dataService;
        HeadingModes = new ObservableCollection<OptionItem<QiblaHeadingMode>>();
        ReadingModes = new ObservableCollection<OptionItem<QiblaReadingMode>>();
        FilterModes = new ObservableCollection<OptionItem<QiblaFilterMode>>();
        BuildOptions();
        SelectHeadingModeCommand = new Command<OptionItem<QiblaHeadingMode>>(item => {
            if (item != null) {
                SelectedHeadingMode = item;
            }
        });
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
        LoadPreferences();
        LocalizationManager.LanguageChanged += (_, _) => {
            BuildOptions();
            LoadPreferences();
            DirectionLabel = ResolveDirectionLabel(Bearing);
        };
    }

    public ObservableCollection<OptionItem<QiblaHeadingMode>> HeadingModes { get; }
    public ObservableCollection<OptionItem<QiblaReadingMode>> ReadingModes { get; }
    public ObservableCollection<OptionItem<QiblaFilterMode>> FilterModes { get; }
    public Command<OptionItem<QiblaHeadingMode>> SelectHeadingModeCommand {
        get => _selectHeadingModeCommand!;
        private set => _selectHeadingModeCommand = value;
    }

    public Command<OptionItem<QiblaReadingMode>> SelectReadingModeCommand {
        get => _selectReadingModeCommand!;
        private set => _selectReadingModeCommand = value;
    }

    public Command<OptionItem<QiblaFilterMode>> SelectFilterModeCommand {
        get => _selectFilterModeCommand!;
        private set => _selectFilterModeCommand = value;
    }

    public OptionItem<QiblaHeadingMode>? SelectedHeadingMode {
        get => _selectedHeadingMode;
        set {
            if (SetProperty(ref _selectedHeadingMode, value)) {
                ApplyHeadingPreference();
                OnPropertyChanged(nameof(IsManualHeadingMode));
                SavePreferences();
            }
        }
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

    public bool IsManualHeadingMode => SelectedHeadingMode?.Value == QiblaHeadingMode.Manual;

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

    public string DirectionLabel {
        get => _directionLabel;
        set => SetProperty(ref _directionLabel, value);
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
            ApplyHeadingPreference();
            UpdateNeedle();
            StatusMessage = string.Empty;
        }
    }

    public void UpdateHeading(double heading) {
        if (IsManualHeadingMode) {
            return;
        }

        Heading = NormalizeHeading(heading);
        UpdateNeedle();
    }

    public void AdjustManualHeading(double delta) {
        if (!IsManualHeadingMode) {
            return;
        }

        SetManualHeading(_manualHeading + delta, persist: false);
    }

    public void CommitManualHeading() {
        if (!IsManualHeadingMode) {
            return;
        }

        SavePreferences();
    }

    private void UpdateNeedle() {
        NeedleRotation = (Bearing + 360) % 360;
        CompassRotation = (-Heading + 360) % 360;
        DirectionLabel = ResolveDirectionLabel(Bearing);
    }

    private static string ResolveDirectionLabel(double bearing) {
        var culture = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var sectors = culture == "ar"
            ? new[] { "شمال", "شمال شرق", "شرق", "جنوب شرق", "جنوب", "جنوب غرب", "غرب", "شمال غرب" }
            : new[] { "North", "North-East", "East", "South-East", "South", "South-West", "West", "North-West" };

        var normalized = (bearing % 360 + 360) % 360;
        var index = (int)Math.Round(normalized / 45d, MidpointRounding.AwayFromZero) % 8;
        return sectors[index];
    }

    private void BuildOptions() {
        HeadingModes.Clear();
        HeadingModes.Add(new OptionItem<QiblaHeadingMode>(QiblaHeadingMode.Sensor, LocalizationManager.Translate("QiblaHeadingMode_Auto")));
        HeadingModes.Add(new OptionItem<QiblaHeadingMode>(QiblaHeadingMode.Manual, LocalizationManager.Translate("QiblaHeadingMode_Manual")));

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
            SelectedHeadingMode = HeadingModes.FirstOrDefault(item => item.Value == settings.Qibla.HeadingMode)
                ?? HeadingModes.FirstOrDefault();
            SelectedReadingMode = ReadingModes.FirstOrDefault(item => item.Value == settings.Qibla.ReadingMode)
                ?? ReadingModes.FirstOrDefault();
            SelectedFilterMode = FilterModes.FirstOrDefault(item => item.Value == settings.Qibla.FilterMode)
                ?? FilterModes.FirstOrDefault();
            _manualHeading = NormalizeHeading(settings.Qibla.ManualHeading);
            ApplyHeadingPreference();
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
            SunAngles = settings.SunAngles,
            Offsets = settings.Offsets,
            FastingOffsets = settings.FastingOffsets,
            FastingReminders = settings.FastingReminders,
            Notifications = settings.Notifications,
            AlarmReminders = settings.AlarmReminders,
            Qibla = new QiblaPreferences {
                HeadingMode = SelectedHeadingMode?.Value ?? QiblaHeadingMode.Sensor,
                ManualHeading = _manualHeading,
                ReadingMode = SelectedReadingMode?.Value ?? QiblaReadingMode.Balanced,
                FilterMode = SelectedFilterMode?.Value ?? QiblaFilterMode.Normal,
                DirectionMode = settings.Qibla.DirectionMode
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

    private void ApplyHeadingPreference() {
        if (!IsManualHeadingMode) {
            return;
        }

        Heading = _manualHeading;
        UpdateNeedle();
    }

    private void SetManualHeading(double heading, bool persist) {
        _manualHeading = NormalizeHeading(heading);
        Heading = _manualHeading;
        UpdateNeedle();

        if (persist) {
            SavePreferences();
        }
    }

    private static double NormalizeHeading(double heading) {
        var normalized = heading % 360d;
        return normalized < 0 ? normalized + 360d : normalized;
    }
}
