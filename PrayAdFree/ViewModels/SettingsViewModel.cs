using System.Collections.ObjectModel;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Pray_Ad_Free;
using PrayAdFree.Core.Models;
using Pray_Ad_Free.Models;
using Pray_Ad_Free.Services;
using System.Linq;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Graphics;

namespace Pray_Ad_Free.ViewModels;

public sealed class SettingsViewModel : ViewModelBase {
    private readonly PrayerDataService _dataService;
    private readonly GeoService _geoService;
    private readonly IAppLogger _logger;
    private AppSettings _settings = new();
    private bool _useGps;
    private string _city = "";
    private string _country = "";
    private string _latitude = "";
    private string _longitude = "";
    private OptionItem<CalculationMethod>? _selectedMethod;
    private OptionItem<Madhhab>? _selectedMadhhab;
    private OptionItem<HighLatitudeRule>? _selectedHighLatitude;
    private string _fajrOffset = "0";
    private string _sunriseOffset = "0";
    private string _dhuhrOffset = "0";
    private string _asrOffset = "0";
    private string _maghribOffset = "0";
    private string _ishaOffset = "0";
    private string _imsakOffset = "0";
    private string _imsakAdvance = "0";
    private string _iftarDelay = "0";
    private string _imsakReminderValue = "";
    private string _iftarReminderValue = "";
    private string _adhanReminderValue = "";
    private bool _notificationsEnabled;
    private bool _vibrationEnabled;
    private string _minutesBefore = "0";
    private OptionItem<string>? _selectedLanguage;
    private OptionItem<ThemeMode>? _selectedThemeMode;
    private OptionItem<ThemeVariant>? _selectedThemeVariant;
    private AccentOption? _selectedAccent;
    private Color _accentPreviewColor = Colors.Transparent;
    private string _statusMessage = "";
    private bool _suspendSave;
    private int _saveVersion;
    private int _geoVersion;
    private PlaceOption? _selectedCountry;
    private PlaceOption? _selectedCity;
    private bool _gpsBusy;
    private CancellationTokenSource? _gpsLoopCts;
    private bool _suspendPlaceSelection;
    private OptionItem<int>? _selectedImsakReminderUnit;
    private OptionItem<int>? _selectedImsakReminderDirection;
    private OptionItem<int>? _selectedIftarReminderUnit;
    private OptionItem<int>? _selectedIftarReminderDirection;
    private OptionItem<int>? _selectedAdhanReminderUnit;
    private OptionItem<int>? _selectedAdhanReminderDirection;
    private OptionItem<ClockFormat>? _selectedClockFormat;
    private OptionItem<string>? _selectedAdhanSound;
    private OptionItem<VibrationStrength>? _selectedVibrationStrength;
    private OptionItem<VibrationPattern>? _selectedVibrationPattern;
    private OptionItem<AdhanReminderScope>? _selectedAdhanReminderScope;
    private OptionItem<PrayerId>? _selectedAdhanReminderPrayer;
    private readonly ObservableCollection<AdhanPrayerOverrideViewModel> _adhanPrayerOverrides;
    private int _textScale;
    private string _textScaleLabel = "";
    private TasbihPresetEditorViewModel? _selectedTasbihPreset;
    private string _newTasbihText = "";
    private string _newTasbihCount = "";
    private OptionItem<TasbihRepeatMode>? _selectedTasbihRepeatMode;
    private string _newTasbihPresetName = "";

    public SettingsViewModel(PrayerDataService dataService, GeoService geoService, IAppLogger logger) {
        _dataService = dataService;
        _geoService = geoService;
        _logger = logger;
        Methods = new ObservableCollection<OptionItem<CalculationMethod>>();
        Madhhabs = new ObservableCollection<OptionItem<Madhhab>>();
        HighLatitudeRules = new ObservableCollection<OptionItem<HighLatitudeRule>>();

        ThemeModes = new ObservableCollection<OptionItem<ThemeMode>>();
        ThemeVariants = new ObservableCollection<OptionItem<ThemeVariant>>();
        AccentOptions = new ObservableCollection<AccentOption>(ThemeManager.GetAccentOptions(ThemeVariant.A));
        Languages = new ObservableCollection<OptionItem<string>>();
        CountryOptions = new ObservableCollection<PlaceOption>();
        CityOptions = new ObservableCollection<PlaceOption>();
        ReminderUnits = new ObservableCollection<OptionItem<int>>();
        ReminderDirections = new ObservableCollection<OptionItem<int>>();
        ImsakReminders = new ObservableCollection<ReminderOffsetItem>();
        IftarReminders = new ObservableCollection<ReminderOffsetItem>();
        AdhanReminders = new ObservableCollection<ReminderOffsetItem>();
        ClockFormats = new ObservableCollection<OptionItem<ClockFormat>>();
        AdhanSounds = new ObservableCollection<OptionItem<string>>();
        AdhanOverrideSounds = new ObservableCollection<OptionItem<string>>();
        AdhanOverrideVibrations = new ObservableCollection<OptionItem<int>>();
        VibrationStrengths = new ObservableCollection<OptionItem<VibrationStrength>>();
        VibrationPatterns = new ObservableCollection<OptionItem<VibrationPattern>>();
        AdhanReminderScopes = new ObservableCollection<OptionItem<AdhanReminderScope>>();
        AdhanReminderPrayers = new ObservableCollection<OptionItem<PrayerId>>();
        _adhanPrayerOverrides = new ObservableCollection<AdhanPrayerOverrideViewModel>();
        TasbihPresets = new ObservableCollection<TasbihPresetEditorViewModel>();
        TasbihRepeatModes = new ObservableCollection<OptionItem<TasbihRepeatMode>>();
        BuildLocalizedPickers();
        BuildReminderOptions();
        BuildClockFormats();
        BuildNotificationOptions();
        BuildAdhanOverrideOptions();
        BuildTasbihRepeatModes();
        RefreshGpsCommand = new Command(async () => await RefreshGpsAsync(), () => !GpsBusy);
        AddImsakReminderCommand = new Command(AddImsakReminder);
        AddIftarReminderCommand = new Command(AddIftarReminder);
        RemoveImsakReminderCommand = new Command<ReminderOffsetItem>(RemoveImsakReminder);
        RemoveIftarReminderCommand = new Command<ReminderOffsetItem>(RemoveIftarReminder);
        AddAdhanReminderCommand = new Command(AddAdhanReminder);
        RemoveAdhanReminderCommand = new Command<ReminderOffsetItem>(RemoveAdhanReminder);
        IncreaseTextSizeCommand = new Command(IncreaseTextSize);
        DecreaseTextSizeCommand = new Command(DecreaseTextSize);
        AddTasbihItemCommand = new Command(AddTasbihItem);
        RemoveTasbihItemCommand = new Command<TasbihItemEditorViewModel>(RemoveTasbihItem);
        MoveTasbihItemUpCommand = new Command<TasbihItemEditorViewModel>(MoveTasbihItemUp);
        MoveTasbihItemDownCommand = new Command<TasbihItemEditorViewModel>(MoveTasbihItemDown);
        AddTasbihPresetCommand = new Command(AddTasbihPreset);

        Load();
        PropertyChanged += OnSettingsPropertyChanged;
        LocalizationManager.LanguageChanged += (_, _) => {
            BuildLocalizedPickers();
            BuildPlaceOptions();
            BuildReminderOptions();
            RebuildReminderLabels();
            BuildClockFormats();
            BuildNotificationOptions();
            BuildAdhanOverrideOptions();
            BuildTasbihRepeatModes();
            RefreshTasbihLocalization();
            RefreshAdhanOverridesLocalization();
        };
    }

    public ObservableCollection<OptionItem<CalculationMethod>> Methods { get; }
    public ObservableCollection<OptionItem<Madhhab>> Madhhabs { get; }
    public ObservableCollection<OptionItem<HighLatitudeRule>> HighLatitudeRules { get; }
    public ObservableCollection<OptionItem<ThemeMode>> ThemeModes { get; }
    public ObservableCollection<OptionItem<ThemeVariant>> ThemeVariants { get; }
    public ObservableCollection<AccentOption> AccentOptions { get; }
    public ObservableCollection<OptionItem<string>> Languages { get; }
    public ObservableCollection<PlaceOption> CountryOptions { get; }
    public ObservableCollection<PlaceOption> CityOptions { get; }
    public ObservableCollection<OptionItem<int>> ReminderUnits { get; }
    public ObservableCollection<OptionItem<int>> ReminderDirections { get; }
    public ObservableCollection<ReminderOffsetItem> ImsakReminders { get; }
    public ObservableCollection<ReminderOffsetItem> IftarReminders { get; }
    public ObservableCollection<ReminderOffsetItem> AdhanReminders { get; }
    public ObservableCollection<OptionItem<ClockFormat>> ClockFormats { get; }
    public ObservableCollection<OptionItem<string>> AdhanSounds { get; }
    public ObservableCollection<OptionItem<string>> AdhanOverrideSounds { get; }
    public ObservableCollection<OptionItem<int>> AdhanOverrideVibrations { get; }
    public ObservableCollection<OptionItem<VibrationStrength>> VibrationStrengths { get; }
    public ObservableCollection<OptionItem<VibrationPattern>> VibrationPatterns { get; }
    public ObservableCollection<OptionItem<AdhanReminderScope>> AdhanReminderScopes { get; }
    public ObservableCollection<OptionItem<PrayerId>> AdhanReminderPrayers { get; }
    public ObservableCollection<AdhanPrayerOverrideViewModel> AdhanPrayerOverrides => _adhanPrayerOverrides;
    public ObservableCollection<TasbihPresetEditorViewModel> TasbihPresets { get; }
    public ObservableCollection<OptionItem<TasbihRepeatMode>> TasbihRepeatModes { get; }
    public Command RefreshGpsCommand { get; }
    public Command AddImsakReminderCommand { get; }
    public Command AddIftarReminderCommand { get; }
    public Command RemoveImsakReminderCommand { get; }
    public Command RemoveIftarReminderCommand { get; }
    public Command AddAdhanReminderCommand { get; }
    public Command RemoveAdhanReminderCommand { get; }
    public Command IncreaseTextSizeCommand { get; }
    public Command DecreaseTextSizeCommand { get; }
    public Command AddTasbihItemCommand { get; }
    public Command RemoveTasbihItemCommand { get; }
    public Command MoveTasbihItemUpCommand { get; }
    public Command MoveTasbihItemDownCommand { get; }
    public Command AddTasbihPresetCommand { get; }
    public bool UseGps {
        get => _useGps;
        set {
            if (SetProperty(ref _useGps, value)) {
                OnPropertyChanged(nameof(IsManualLocationEnabled));
                if (_suspendSave) {
                    return;
                }

                ScheduleSave();
                if (value) {
                    StartGpsLoop();
                } else {
                    StopGpsLoop();
                }
            }
        }
    }

    public bool IsManualLocationEnabled => !UseGps;

    public bool GpsBusy => _gpsBusy;
    public string City {
        get => _city;
        set {
            if (SetProperty(ref _city, value) && !_suspendSave && UseGps) {
                UseGps = false;
            }
        }
    }

    public string Country {
        get => _country;
        set {
            if (SetProperty(ref _country, value) && !_suspendSave && UseGps) {
                UseGps = false;
            }
        }
    }

    public string Latitude {
        get => _latitude;
        set {
            if (SetProperty(ref _latitude, value) && !_suspendSave) {
                if (UseGps) {
                    UseGps = false;
                }
                ScheduleReverseLookup();
            }
        }
    }

    public string Longitude {
        get => _longitude;
        set {
            if (SetProperty(ref _longitude, value) && !_suspendSave) {
                if (UseGps) {
                    UseGps = false;
                }
                ScheduleReverseLookup();
            }
        }
    }

    public PlaceOption? SelectedCountry {
        get => _selectedCountry;
        set {
            if (SetProperty(ref _selectedCountry, value)) {
                if (!_suspendPlaceSelection) {
                    _ = ApplyCountrySelectionAsync(value);
                }
            }
        }
    }

    public PlaceOption? SelectedCity {
        get => _selectedCity;
        set {
            if (SetProperty(ref _selectedCity, value)) {
                if (!_suspendPlaceSelection) {
                    ApplyCitySelection(value);
                }
            }
        }
    }

    public OptionItem<CalculationMethod>? SelectedMethod {
        get => _selectedMethod;
        set => SetProperty(ref _selectedMethod, value);
    }

    public OptionItem<Madhhab>? SelectedMadhhab {
        get => _selectedMadhhab;
        set => SetProperty(ref _selectedMadhhab, value);
    }

    public OptionItem<HighLatitudeRule>? SelectedHighLatitude {
        get => _selectedHighLatitude;
        set => SetProperty(ref _selectedHighLatitude, value);
    }

    public string FajrOffset {
        get => _fajrOffset;
        set => SetProperty(ref _fajrOffset, value);
    }

    public string SunriseOffset {
        get => _sunriseOffset;
        set => SetProperty(ref _sunriseOffset, value);
    }

    public string DhuhrOffset {
        get => _dhuhrOffset;
        set => SetProperty(ref _dhuhrOffset, value);
    }

    public string AsrOffset {
        get => _asrOffset;
        set => SetProperty(ref _asrOffset, value);
    }

    public string MaghribOffset {
        get => _maghribOffset;
        set => SetProperty(ref _maghribOffset, value);
    }

    public string IshaOffset {
        get => _ishaOffset;
        set => SetProperty(ref _ishaOffset, value);
    }

    public string ImsakOffset {
        get => _imsakOffset;
        set => SetProperty(ref _imsakOffset, value);
    }

    public string ImsakAdvance {
        get => _imsakAdvance;
        set => SetProperty(ref _imsakAdvance, value);
    }

    public string IftarDelay {
        get => _iftarDelay;
        set => SetProperty(ref _iftarDelay, value);
    }

    public string ImsakReminderValue {
        get => _imsakReminderValue;
        set => SetProperty(ref _imsakReminderValue, value);
    }

    public string IftarReminderValue {
        get => _iftarReminderValue;
        set => SetProperty(ref _iftarReminderValue, value);
    }

    public string AdhanReminderValue {
        get => _adhanReminderValue;
        set => SetProperty(ref _adhanReminderValue, value);
    }

    public OptionItem<int>? SelectedImsakReminderUnit {
        get => _selectedImsakReminderUnit;
        set => SetProperty(ref _selectedImsakReminderUnit, value);
    }

    public OptionItem<int>? SelectedImsakReminderDirection {
        get => _selectedImsakReminderDirection;
        set => SetProperty(ref _selectedImsakReminderDirection, value);
    }

    public OptionItem<int>? SelectedIftarReminderUnit {
        get => _selectedIftarReminderUnit;
        set => SetProperty(ref _selectedIftarReminderUnit, value);
    }

    public OptionItem<int>? SelectedIftarReminderDirection {
        get => _selectedIftarReminderDirection;
        set => SetProperty(ref _selectedIftarReminderDirection, value);
    }

    public OptionItem<int>? SelectedAdhanReminderUnit {
        get => _selectedAdhanReminderUnit;
        set => SetProperty(ref _selectedAdhanReminderUnit, value);
    }

    public OptionItem<int>? SelectedAdhanReminderDirection {
        get => _selectedAdhanReminderDirection;
        set => SetProperty(ref _selectedAdhanReminderDirection, value);
    }

    public OptionItem<ClockFormat>? SelectedClockFormat {
        get => _selectedClockFormat;
        set => SetProperty(ref _selectedClockFormat, value);
    }

    public OptionItem<string>? SelectedAdhanSound {
        get => _selectedAdhanSound;
        set => SetProperty(ref _selectedAdhanSound, value);
    }

    public OptionItem<VibrationStrength>? SelectedVibrationStrength {
        get => _selectedVibrationStrength;
        set => SetProperty(ref _selectedVibrationStrength, value);
    }

    public OptionItem<VibrationPattern>? SelectedVibrationPattern {
        get => _selectedVibrationPattern;
        set => SetProperty(ref _selectedVibrationPattern, value);
    }

    public OptionItem<AdhanReminderScope>? SelectedAdhanReminderScope {
        get => _selectedAdhanReminderScope;
        set {
            if (SetProperty(ref _selectedAdhanReminderScope, value)) {
                OnPropertyChanged(nameof(IsReminderPrayerEnabled));
            }
        }
    }

    public OptionItem<PrayerId>? SelectedAdhanReminderPrayer {
        get => _selectedAdhanReminderPrayer;
        set => SetProperty(ref _selectedAdhanReminderPrayer, value);
    }

    public bool IsReminderPrayerEnabled => SelectedAdhanReminderScope?.Value == AdhanReminderScope.SpecificPrayer;

    public int TextScale {
        get => _textScale;
        set {
            var clamped = Math.Clamp(value, -2, 6);
            if (SetProperty(ref _textScale, clamped)) {
                UpdateTextScaleLabel();
            }
        }
    }

    public string TextScaleLabel {
        get => _textScaleLabel;
        set => SetProperty(ref _textScaleLabel, value);
    }

    public TasbihPresetEditorViewModel? SelectedTasbihPreset {
        get => _selectedTasbihPreset;
        set {
            if (SetProperty(ref _selectedTasbihPreset, value)) {
                SelectedTasbihRepeatMode = TasbihRepeatModes.FirstOrDefault(item => item.Value == value?.RepeatMode)
                    ?? TasbihRepeatModes.FirstOrDefault();
                RecalculateTasbihStartIndices();
            }
        }
    }

    public OptionItem<TasbihRepeatMode>? SelectedTasbihRepeatMode {
        get => _selectedTasbihRepeatMode;
        set {
            if (SetProperty(ref _selectedTasbihRepeatMode, value) && SelectedTasbihPreset != null) {
                SelectedTasbihPreset.RepeatMode = value?.Value ?? TasbihRepeatMode.None;
                ScheduleSave();
            }
        }
    }

    public string NewTasbihText {
        get => _newTasbihText;
        set => SetProperty(ref _newTasbihText, value);
    }

    public string NewTasbihCount {
        get => _newTasbihCount;
        set => SetProperty(ref _newTasbihCount, value);
    }

    public string NewTasbihPresetName {
        get => _newTasbihPresetName;
        set => SetProperty(ref _newTasbihPresetName, value);
    }

    public bool NotificationsEnabled {
        get => _notificationsEnabled;
        set => SetProperty(ref _notificationsEnabled, value);
    }

    public bool VibrationEnabled {
        get => _vibrationEnabled;
        set => SetProperty(ref _vibrationEnabled, value);
    }

    public string MinutesBefore {
        get => _minutesBefore;
        set => SetProperty(ref _minutesBefore, value);
    }

    public OptionItem<string>? SelectedLanguage {
        get => _selectedLanguage;
        set => SetProperty(ref _selectedLanguage, value);
    }

    public OptionItem<ThemeMode>? SelectedThemeMode {
        get => _selectedThemeMode;
        set => SetProperty(ref _selectedThemeMode, value);
    }

    public OptionItem<ThemeVariant>? SelectedThemeVariant {
        get => _selectedThemeVariant;
        set {
            if (SetProperty(ref _selectedThemeVariant, value)) {
                UpdateAccentOptions(value?.Value ?? ThemeVariant.A, _selectedAccent?.Index ?? 0);
            }
        }
    }

    public AccentOption? SelectedAccent {
        get => _selectedAccent;
        set {
            if (SetProperty(ref _selectedAccent, value)) {
                AccentPreviewColor = value?.Color ?? Colors.Transparent;
            }
        }
    }

    public Color AccentPreviewColor {
        get => _accentPreviewColor;
        set => SetProperty(ref _accentPreviewColor, value);
    }

    public string StatusMessage {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private void Load() {
        _suspendSave = true;
        _settings = _dataService.LoadSettings();
        UseGps = _settings.Location.Mode == LocationMode.Gps;
        City = _settings.Location.City;
        Country = _settings.Location.Country;
        Latitude = _settings.Location.Latitude == 0 ? "" : _settings.Location.Latitude.ToString("F4");
        Longitude = _settings.Location.Longitude == 0 ? "" : _settings.Location.Longitude.ToString("F4");
        SelectedMethod = Methods.FirstOrDefault(item => item.Value == _settings.Method);
        SelectedMadhhab = Madhhabs.FirstOrDefault(item => item.Value == _settings.Madhhab);
        SelectedHighLatitude = HighLatitudeRules.FirstOrDefault(item => item.Value == _settings.HighLatitudeRule);
        FajrOffset = _settings.Offsets.Fajr.ToString();
        SunriseOffset = _settings.Offsets.Sunrise.ToString();
        DhuhrOffset = _settings.Offsets.Dhuhr.ToString();
        AsrOffset = _settings.Offsets.Asr.ToString();
        MaghribOffset = _settings.Offsets.Maghrib.ToString();
        IshaOffset = _settings.Offsets.Isha.ToString();
        ImsakOffset = _settings.Offsets.Imsak.ToString();
        ImsakAdvance = _settings.FastingOffsets.ImsakAdvanceMinutes.ToString();
        IftarDelay = _settings.FastingOffsets.IftarDelayMinutes.ToString();
        LoadReminders();
        LoadAdhanReminders();
        NotificationsEnabled = _settings.Notifications.EnableAdhan;
        VibrationEnabled = _settings.Notifications.EnableVibration;
        MinutesBefore = _settings.Notifications.MinutesBefore.ToString();
        SelectedAdhanSound = AdhanSounds.FirstOrDefault(item => item.Value == _settings.Notifications.SoundKey)
            ?? AdhanSounds.FirstOrDefault();
        SelectedVibrationStrength = VibrationStrengths.FirstOrDefault(item => item.Value == _settings.Notifications.VibrationStrength)
            ?? VibrationStrengths.FirstOrDefault();
        SelectedVibrationPattern = VibrationPatterns.FirstOrDefault(item => item.Value == _settings.Notifications.VibrationPattern)
            ?? VibrationPatterns.FirstOrDefault();
        SelectedAdhanReminderScope = AdhanReminderScopes.FirstOrDefault(item => item.Value == _settings.Notifications.ReminderScope)
            ?? AdhanReminderScopes.FirstOrDefault();
        SelectedAdhanReminderPrayer = AdhanReminderPrayers.FirstOrDefault(item => item.Value == _settings.Notifications.ReminderPrayer)
            ?? AdhanReminderPrayers.FirstOrDefault();
        SelectedLanguage = Languages.FirstOrDefault(item => item.Value == _settings.Language)
            ?? Languages.FirstOrDefault();
        SelectedThemeMode = ThemeModes.FirstOrDefault(item => item.Value == _settings.ThemeMode)
            ?? ThemeModes.FirstOrDefault();
        SelectedThemeVariant = ThemeVariants.FirstOrDefault(item => item.Value == _settings.ThemeVariant)
            ?? ThemeVariants.FirstOrDefault();
        SelectedClockFormat = ClockFormats.FirstOrDefault(item => item.Value == _settings.ClockFormat)
            ?? ClockFormats.FirstOrDefault();
        TextScale = _settings.TextScale;
        EnsureTasbihDefaults();
        LoadTasbihPresets();
        LoadAdhanOverrides();
        UpdateAccentOptions(SelectedThemeVariant?.Value ?? ThemeVariant.A, _settings.AccentIndex);
        BuildPlaceOptions();
        _suspendSave = false;
        if (UseGps) {
            _ = RefreshGpsAsync();
        }
    }

    private void Save() {
        var mode = UseGps ? LocationMode.Gps : LocationMode.Manual;
        var location = new LocationSettings {
            Mode = mode,
            City = NormalizeName(City?.Trim()),
            Country = NormalizeName(Country?.Trim()),
            Latitude = ParseDouble(Latitude),
            Longitude = ParseDouble(Longitude),
            CountryCode = _settings.Location.CountryCode,
            TimeZoneId = _settings.Location.TimeZoneId,
            LastUpdatedUtc = _settings.Location.LastUpdatedUtc
        };

        var offsets = new PrayerOffsets {
            Fajr = ParseInt(FajrOffset),
            Sunrise = ParseInt(SunriseOffset),
            Dhuhr = ParseInt(DhuhrOffset),
            Asr = ParseInt(AsrOffset),
            Maghrib = ParseInt(MaghribOffset),
            Isha = ParseInt(IshaOffset),
            Imsak = ParseInt(ImsakOffset)
        };

        var fastingOffsets = new FastingOffsets {
            ImsakAdvanceMinutes = ParseInt(ImsakAdvance),
            IftarDelayMinutes = ParseInt(IftarDelay)
        };
        var fastingReminders = new FastingReminderSettings {
            ImsakRemindersMinutes = ImsakReminders.Select(item => item.Minutes).ToList(),
            IftarRemindersMinutes = IftarReminders.Select(item => item.Minutes).ToList()
        };

        var notifications = new NotificationSettings {
            EnableAdhan = NotificationsEnabled,
            EnableVibration = VibrationEnabled,
            MinutesBefore = ParseInt(MinutesBefore),
            SoundKey = SelectedAdhanSound?.Value ?? "adhan_default",
            VibrationStrength = SelectedVibrationStrength?.Value ?? VibrationStrength.Medium,
            VibrationPattern = SelectedVibrationPattern?.Value ?? VibrationPattern.Short,
            ReminderScope = SelectedAdhanReminderScope?.Value ?? AdhanReminderScope.All,
            ReminderPrayer = SelectedAdhanReminderPrayer?.Value ?? PrayerId.Fajr,
            ReminderOffsetsMinutes = AdhanReminders.Select(item => item.Minutes).ToList(),
            PrayerOverrides = BuildAdhanOverrides()
        };

        var tasbih = new TasbihSettings {
            Presets = TasbihPresets.Select(preset => new TasbihPresetSettings {
                Name = preset.Name,
                RepeatMode = preset.RepeatMode,
                Items = preset.Items.Select(item => new TasbihItemSettings {
                    Text = item.Text,
                    TargetCount = item.TargetCount
                }).ToList()
            }).ToList(),
            SelectedPresetIndex = SelectedTasbihPreset == null ? 0 : Math.Max(0, TasbihPresets.IndexOf(SelectedTasbihPreset))
        };

        var previousLanguage = _settings.Language;
        _settings = new AppSettings {
            Location = location,
            Method = SelectedMethod?.Value ?? CalculationMethod.Auto,
            Madhhab = SelectedMadhhab?.Value ?? Madhhab.Shafi,
            HighLatitudeRule = SelectedHighLatitude?.Value ?? HighLatitudeRule.MiddleOfTheNight,
            Offsets = offsets,
            FastingOffsets = fastingOffsets,
            FastingReminders = fastingReminders,
            Notifications = notifications,
            ClockFormat = SelectedClockFormat?.Value ?? ClockFormat.Auto,
            TextScale = TextScale,
            Tasbih = tasbih,
            Language = SelectedLanguage?.Value ?? "auto",
            LanguageSelected = true,
            ThemeMode = SelectedThemeMode?.Value ?? ThemeMode.Auto,
            ThemeVariant = SelectedThemeVariant?.Value ?? ThemeVariant.A,
            AccentIndex = SelectedAccent?.Index ?? 0
        };

        _dataService.SaveSettings(_settings);
        try {
            LocalizationManager.SetLanguage(_settings.Language);
        } catch {
        }
        try {
            ThemeManager.ApplyTheme(_settings);
        } catch {
        }
        if (!string.Equals(previousLanguage, _settings.Language, StringComparison.OrdinalIgnoreCase)) {
            var window = Application.Current?.Windows.FirstOrDefault();
            if (window != null) {
                window.Page = ServiceHelper.GetService<AppShell>();
            }
        }
        StatusMessage = "Settings saved";
    }

    private static int ParseInt(string value) {
        return int.TryParse(value, out var parsed) ? parsed : 0;
    }

    private static double ParseDouble(string value) {
        return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private void UpdateAccentOptions(ThemeVariant variant, int selectedIndex) {
        AccentOptions.Clear();
        foreach (var option in ThemeManager.GetAccentOptions(variant)) {
            AccentOptions.Add(option);
        }

        SelectedAccent = AccentOptions.FirstOrDefault(item => item.Index == selectedIndex)
            ?? AccentOptions.FirstOrDefault();
    }

    private void BuildPlaceOptions() {
        var known = _geoService.GetKnownPlaces()
            .Where(item => !string.IsNullOrWhiteSpace(item.Country))
            .ToList();

        var current = _settings.Location;
        if (!string.IsNullOrWhiteSpace(current.Country)) {
            known.Insert(0, new GeoLocationResult {
                Country = current.Country,
                City = current.City,
                CountryCode = current.CountryCode,
                Latitude = current.Latitude,
                Longitude = current.Longitude
            });
        }

        var countries = known
            .GroupBy(item => NormalizeName(item.Country))
            .Select(group => group.First())
            .Select(country => new PlaceOption(
                NormalizeName(country.Country),
                NormalizeName(country.City),
                country.Latitude,
                country.Longitude,
                true))
            .ToList();

        if (countries.Count == 0) {
            countries.Add(new PlaceOption(LocalizationManager.Translate("UnknownCountry"), "", 0, 0, true));
        }

        RunOnMainThread(() => {
            CountryOptions.Clear();
            foreach (var option in countries) {
                CountryOptions.Add(option);
            }

            _suspendPlaceSelection = true;
            SelectedCountry = CountryOptions.FirstOrDefault(option => option.Country == current.Country)
                ?? CountryOptions.FirstOrDefault();
            _suspendPlaceSelection = false;

            UpdateCityOptions(current.Country);
        });
    }

    private void UpdateCityOptions(string? country) {
        var known = _geoService.GetKnownPlaces()
            .Where(item => string.Equals(NormalizeName(item.Country), NormalizeName(country ?? ""), StringComparison.OrdinalIgnoreCase))
            .ToList();

        var cities = known
            .Select(city => new PlaceOption(
                NormalizeName(city.Country),
                NormalizeName(city.City),
                city.Latitude,
                city.Longitude,
                false))
            .Where(option => !string.IsNullOrWhiteSpace(option.City))
            .Where(option => !string.Equals(option.City, option.Country, StringComparison.OrdinalIgnoreCase))
            .GroupBy(option => option.City, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (cities.Count == 0 && !string.IsNullOrWhiteSpace(country)) {
            cities.Add(new PlaceOption(country, LocalizationManager.Translate("UnknownCity"), 0, 0, false));
        }

        RunOnMainThread(() => {
            CityOptions.Clear();
            foreach (var city in cities) {
                CityOptions.Add(city);
            }

            _suspendPlaceSelection = true;
            SelectedCity = CityOptions.FirstOrDefault(option => option.City == _settings.Location.City)
                ?? CityOptions.FirstOrDefault();
            _suspendPlaceSelection = false;
        });
    }

    private async Task ApplyCountrySelectionAsync(PlaceOption? option) {
        if (_suspendSave || option == null) {
            return;
        }

        var country = option.Country;
        if (string.IsNullOrWhiteSpace(country)) {
            return;
        }

        var result = await _geoService.ForwardAsync(country, CancellationToken.None).ConfigureAwait(false);
        RunOnMainThread(() => {
            _suspendSave = true;
            UseGps = false;
            Country = NormalizeName(country);
            if (result != null) {
                City = string.IsNullOrWhiteSpace(result.City) ? LocalizationManager.Translate("UnknownCity") : NormalizeName(result.City);
                Latitude = result.Latitude.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
                Longitude = result.Longitude.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
            } else {
                City = LocalizationManager.Translate("UnknownCity");
                Latitude = "0";
                Longitude = "0";
            }

            UpdateCityOptions(country);
            _suspendSave = false;
            ScheduleSave();
        });
    }

    private static void RunOnMainThread(Action action) {
        if (MainThread.IsMainThread) {
            action();
        } else {
            MainThread.BeginInvokeOnMainThread(action);
        }
    }

    private void ApplyCitySelection(PlaceOption? option) {
        if (_suspendSave || option == null) {
            return;
        }

        _suspendSave = true;
        var matchesGps = UseGps
            && string.Equals(option.City, _settings.Location.City, StringComparison.OrdinalIgnoreCase)
            && string.Equals(option.Country, _settings.Location.Country, StringComparison.OrdinalIgnoreCase);
        if (matchesGps) {
            City = _settings.Location.City;
            Country = _settings.Location.Country;
            Latitude = _settings.Location.Latitude.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
            Longitude = _settings.Location.Longitude.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
            UseGps = true;
        } else {
            UseGps = false;
            City = NormalizeName(option.City);
            Country = NormalizeName(option.Country);
            Latitude = option.Latitude.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
            Longitude = option.Longitude.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
        }
        _suspendSave = false;
        ScheduleSave();
    }

    private void BuildLocalizedPickers() {
        var restoreSuspend = _suspendSave;
        _suspendSave = true;
        var previousLanguage = SelectedLanguage?.Value;
        var previousThemeMode = SelectedThemeMode?.Value;
        var previousThemeVariant = SelectedThemeVariant?.Value;
        var previousMethod = SelectedMethod?.Value;
        var previousMadhhab = SelectedMadhhab?.Value;
        var previousHighLatitude = SelectedHighLatitude?.Value;

        Methods.Clear();
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Auto, LocalizationManager.Translate("Method_Auto")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Jafari, LocalizationManager.Translate("Method_Jafari")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Karachi, LocalizationManager.Translate("Method_Karachi")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Isna, LocalizationManager.Translate("Method_Isna")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.MuslimWorldLeague, LocalizationManager.Translate("Method_MuslimWorldLeague")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.UmmAlQura, LocalizationManager.Translate("Method_UmmAlQura")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Egypt, LocalizationManager.Translate("Method_Egypt")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Tehran, LocalizationManager.Translate("Method_Tehran")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Gulf, LocalizationManager.Translate("Method_Gulf")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Kuwait, LocalizationManager.Translate("Method_Kuwait")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Qatar, LocalizationManager.Translate("Method_Qatar")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Singapore, LocalizationManager.Translate("Method_Singapore")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.France, LocalizationManager.Translate("Method_France")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Turkey, LocalizationManager.Translate("Method_Turkey")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Russia, LocalizationManager.Translate("Method_Russia")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Moonsighting, LocalizationManager.Translate("Method_Moonsighting")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Dubai, LocalizationManager.Translate("Method_Dubai")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Jakim, LocalizationManager.Translate("Method_Jakim")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Tunisia, LocalizationManager.Translate("Method_Tunisia")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Algeria, LocalizationManager.Translate("Method_Algeria")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Kemenag, LocalizationManager.Translate("Method_Kemenag")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Morocco, LocalizationManager.Translate("Method_Morocco")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Portugal, LocalizationManager.Translate("Method_Portugal")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Jordan, LocalizationManager.Translate("Method_Jordan")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Custom, LocalizationManager.Translate("Method_Custom")));

        Madhhabs.Clear();
        Madhhabs.Add(new OptionItem<Madhhab>(Madhhab.Shafi, LocalizationManager.Translate("Madhhab_Shafi")));
        Madhhabs.Add(new OptionItem<Madhhab>(Madhhab.Maliki, LocalizationManager.Translate("Madhhab_Maliki")));
        Madhhabs.Add(new OptionItem<Madhhab>(Madhhab.Hanbali, LocalizationManager.Translate("Madhhab_Hanbali")));
        Madhhabs.Add(new OptionItem<Madhhab>(Madhhab.Hanafi, LocalizationManager.Translate("Madhhab_Hanafi")));

        HighLatitudeRules.Clear();
        HighLatitudeRules.Add(new OptionItem<HighLatitudeRule>(HighLatitudeRule.MiddleOfTheNight, LocalizationManager.Translate("HighLatitude_MiddleOfTheNight")));
        HighLatitudeRules.Add(new OptionItem<HighLatitudeRule>(HighLatitudeRule.SeventhOfTheNight, LocalizationManager.Translate("HighLatitude_SeventhOfTheNight")));
        HighLatitudeRules.Add(new OptionItem<HighLatitudeRule>(HighLatitudeRule.TwilightAngle, LocalizationManager.Translate("HighLatitude_TwilightAngle")));

        ThemeModes.Clear();
        ThemeModes.Add(new OptionItem<ThemeMode>(ThemeMode.Auto, LocalizationManager.Translate("ThemeAuto")));
        ThemeModes.Add(new OptionItem<ThemeMode>(ThemeMode.Light, LocalizationManager.Translate("ThemeLight")));
        ThemeModes.Add(new OptionItem<ThemeMode>(ThemeMode.Dark, LocalizationManager.Translate("ThemeDark")));

        ThemeVariants.Clear();
        ThemeVariants.Add(new OptionItem<ThemeVariant>(ThemeVariant.A, LocalizationManager.Translate("ThemeVariantA")));
        ThemeVariants.Add(new OptionItem<ThemeVariant>(ThemeVariant.B, LocalizationManager.Translate("ThemeVariantB")));

        Languages.Clear();
        Languages.Add(new OptionItem<string>("auto", LocalizationManager.Translate("Auto")));
        foreach (var option in LocalizationManager.GetAvailableLanguages()) {
            Languages.Add(new OptionItem<string>(option.Code, option.Name));
        }
        if (Languages.Count == 1) {
            Languages.Add(new OptionItem<string>("en", "English"));
            Languages.Add(new OptionItem<string>("ar", "Arabic"));
            Languages.Add(new OptionItem<string>("fr", "French"));
            Languages.Add(new OptionItem<string>("tr", "Turkish"));
            Languages.Add(new OptionItem<string>("es", "Spanish"));
        }

        SelectedMethod = Methods.FirstOrDefault(item => item.Value == previousMethod)
            ?? SelectedMethod
            ?? Methods.FirstOrDefault();
        SelectedMadhhab = Madhhabs.FirstOrDefault(item => item.Value == previousMadhhab)
            ?? SelectedMadhhab
            ?? Madhhabs.FirstOrDefault();
        SelectedHighLatitude = HighLatitudeRules.FirstOrDefault(item => item.Value == previousHighLatitude)
            ?? SelectedHighLatitude
            ?? HighLatitudeRules.FirstOrDefault();
        SelectedLanguage = Languages.FirstOrDefault(item => item.Value == previousLanguage)
            ?? SelectedLanguage
            ?? Languages.FirstOrDefault();
        SelectedThemeMode = ThemeModes.FirstOrDefault(item => item.Value == previousThemeMode)
            ?? SelectedThemeMode
            ?? ThemeModes.FirstOrDefault();
        SelectedThemeVariant = ThemeVariants.FirstOrDefault(item => item.Value == previousThemeVariant)
            ?? SelectedThemeVariant
            ?? ThemeVariants.FirstOrDefault();
        _suspendSave = restoreSuspend;
    }

    private void BuildReminderOptions() {
        var currentImsakUnit = SelectedImsakReminderUnit?.Value ?? 1;
        var currentIftarUnit = SelectedIftarReminderUnit?.Value ?? 1;
        var currentImsakDirection = SelectedImsakReminderDirection?.Value ?? -1;
        var currentIftarDirection = SelectedIftarReminderDirection?.Value ?? 1;
        var currentAdhanUnit = SelectedAdhanReminderUnit?.Value ?? 1;
        var currentAdhanDirection = SelectedAdhanReminderDirection?.Value ?? -1;

        ReminderUnits.Clear();
        ReminderUnits.Add(new OptionItem<int>(1, LocalizationManager.Translate("Minutes")));
        ReminderUnits.Add(new OptionItem<int>(60, LocalizationManager.Translate("Hours")));

        ReminderDirections.Clear();
        ReminderDirections.Add(new OptionItem<int>(-1, LocalizationManager.Translate("Before")));
        ReminderDirections.Add(new OptionItem<int>(1, LocalizationManager.Translate("After")));

        SelectedImsakReminderUnit = ReminderUnits.FirstOrDefault(item => item.Value == currentImsakUnit)
            ?? ReminderUnits.FirstOrDefault();
        SelectedIftarReminderUnit = ReminderUnits.FirstOrDefault(item => item.Value == currentIftarUnit)
            ?? ReminderUnits.FirstOrDefault();
        SelectedImsakReminderDirection = ReminderDirections.FirstOrDefault(item => item.Value == currentImsakDirection)
            ?? ReminderDirections.FirstOrDefault();
        SelectedIftarReminderDirection = ReminderDirections.FirstOrDefault(item => item.Value == currentIftarDirection)
            ?? ReminderDirections.LastOrDefault();
        SelectedAdhanReminderUnit = ReminderUnits.FirstOrDefault(item => item.Value == currentAdhanUnit)
            ?? ReminderUnits.FirstOrDefault();
        SelectedAdhanReminderDirection = ReminderDirections.FirstOrDefault(item => item.Value == currentAdhanDirection)
            ?? ReminderDirections.FirstOrDefault();
    }

    private void LoadReminders() {
        ImsakReminders.Clear();
        foreach (var minutes in _settings.FastingReminders.ImsakRemindersMinutes.Distinct().OrderBy(item => item)) {
            ImsakReminders.Add(new ReminderOffsetItem(minutes, BuildReminderLabel(minutes)));
        }

        IftarReminders.Clear();
        foreach (var minutes in _settings.FastingReminders.IftarRemindersMinutes.Distinct().OrderBy(item => item)) {
            IftarReminders.Add(new ReminderOffsetItem(minutes, BuildReminderLabel(minutes)));
        }
    }

    private void LoadAdhanReminders() {
        AdhanReminders.Clear();
        foreach (var minutes in _settings.Notifications.ReminderOffsetsMinutes.Distinct().OrderBy(item => item)) {
            if (minutes == 0) {
                continue;
            }
            AdhanReminders.Add(new ReminderOffsetItem(minutes, BuildReminderLabel(minutes)));
        }
    }

    private void RebuildReminderLabels() {
        var imsak = ImsakReminders.Select(item => item.Minutes).ToList();
        var iftar = IftarReminders.Select(item => item.Minutes).ToList();
        var adhan = AdhanReminders.Select(item => item.Minutes).ToList();
        ImsakReminders.Clear();
        foreach (var minutes in imsak) {
            ImsakReminders.Add(new ReminderOffsetItem(minutes, BuildReminderLabel(minutes)));
        }
        IftarReminders.Clear();
        foreach (var minutes in iftar) {
            IftarReminders.Add(new ReminderOffsetItem(minutes, BuildReminderLabel(minutes)));
        }
        AdhanReminders.Clear();
        foreach (var minutes in adhan) {
            AdhanReminders.Add(new ReminderOffsetItem(minutes, BuildReminderLabel(minutes)));
        }
    }

    private void AddImsakReminder() {
        AddReminder(ImsakReminders, ImsakReminderValue, SelectedImsakReminderUnit, SelectedImsakReminderDirection);
        ImsakReminderValue = "";
    }

    private void AddIftarReminder() {
        AddReminder(IftarReminders, IftarReminderValue, SelectedIftarReminderUnit, SelectedIftarReminderDirection);
        IftarReminderValue = "";
    }

    private void AddAdhanReminder() {
        AddReminder(AdhanReminders, AdhanReminderValue, SelectedAdhanReminderUnit, SelectedAdhanReminderDirection);
        AdhanReminderValue = "";
    }

    private void RemoveImsakReminder(ReminderOffsetItem? item) {
        if (item == null) {
            return;
        }
        ImsakReminders.Remove(item);
        ScheduleSave();
    }

    private void RemoveIftarReminder(ReminderOffsetItem? item) {
        if (item == null) {
            return;
        }
        IftarReminders.Remove(item);
        ScheduleSave();
    }

    private void RemoveAdhanReminder(ReminderOffsetItem? item) {
        if (item == null) {
            return;
        }
        AdhanReminders.Remove(item);
        ScheduleSave();
    }

    private void AddReminder(
        ObservableCollection<ReminderOffsetItem> list,
        string rawValue,
        OptionItem<int>? unit,
        OptionItem<int>? direction) {
        if (!int.TryParse(rawValue, out var value)) {
            return;
        }
        if (value == 0) {
            return;
        }

        var multiplier = unit?.Value ?? 1;
        var sign = direction?.Value ?? -1;
        var minutes = value * multiplier * sign;
        if (list.Any(item => item.Minutes == minutes)) {
            return;
        }

        list.Add(new ReminderOffsetItem(minutes, BuildReminderLabel(minutes)));
        SortReminderList(list);
        ScheduleSave();
    }

    private void SortReminderList(ObservableCollection<ReminderOffsetItem> list) {
        var ordered = list.OrderBy(item => item.Minutes).ToList();
        list.Clear();
        foreach (var item in ordered) {
            list.Add(item);
        }
    }

    private string BuildReminderLabel(int minutes) {
        var directionKey = minutes < 0 ? "Before" : "After";
        var abs = Math.Abs(minutes);
        if (abs >= 60 && abs % 60 == 0) {
            var hours = abs / 60;
            return $"{LocalizationManager.Translate(directionKey)} {hours} {LocalizationManager.Translate("Hours")}";
        }

        return $"{LocalizationManager.Translate(directionKey)} {abs} {LocalizationManager.Translate("Minutes")}";
    }

    private void BuildClockFormats() {
        var current = SelectedClockFormat?.Value ?? ClockFormat.Auto;
        ClockFormats.Clear();
        ClockFormats.Add(new OptionItem<ClockFormat>(ClockFormat.Auto, LocalizationManager.Translate("Clock_Auto")));
        ClockFormats.Add(new OptionItem<ClockFormat>(ClockFormat.TwelveHour, LocalizationManager.Translate("Clock_12h")));
        ClockFormats.Add(new OptionItem<ClockFormat>(ClockFormat.TwentyFourHour, LocalizationManager.Translate("Clock_24h")));
        SelectedClockFormat = ClockFormats.FirstOrDefault(item => item.Value == current)
            ?? ClockFormats.FirstOrDefault();
    }

    private void BuildNotificationOptions() {
        var currentSound = SelectedAdhanSound?.Value ?? "adhan_default";
        var currentStrength = SelectedVibrationStrength?.Value ?? VibrationStrength.Medium;
        var currentPattern = SelectedVibrationPattern?.Value ?? VibrationPattern.Short;
        var currentScope = SelectedAdhanReminderScope?.Value ?? AdhanReminderScope.All;
        var currentPrayer = SelectedAdhanReminderPrayer?.Value ?? PrayerId.Fajr;

        AdhanSounds.Clear();
        AdhanSounds.Add(new OptionItem<string>("adhan_default", LocalizationManager.Translate("Sound_Default")));
        AdhanSounds.Add(new OptionItem<string>("adhan_silent", LocalizationManager.Translate("Sound_Silent")));

        VibrationStrengths.Clear();
        VibrationStrengths.Add(new OptionItem<VibrationStrength>(VibrationStrength.Low, LocalizationManager.Translate("Vibration_Low")));
        VibrationStrengths.Add(new OptionItem<VibrationStrength>(VibrationStrength.Medium, LocalizationManager.Translate("Vibration_Medium")));
        VibrationStrengths.Add(new OptionItem<VibrationStrength>(VibrationStrength.High, LocalizationManager.Translate("Vibration_High")));

        VibrationPatterns.Clear();
        VibrationPatterns.Add(new OptionItem<VibrationPattern>(VibrationPattern.Short, LocalizationManager.Translate("Vibration_Short")));
        VibrationPatterns.Add(new OptionItem<VibrationPattern>(VibrationPattern.Long, LocalizationManager.Translate("Vibration_Long")));
        VibrationPatterns.Add(new OptionItem<VibrationPattern>(VibrationPattern.Pulse, LocalizationManager.Translate("Vibration_Pulse")));

        AdhanReminderScopes.Clear();
        AdhanReminderScopes.Add(new OptionItem<AdhanReminderScope>(AdhanReminderScope.All, LocalizationManager.Translate("Reminder_All")));
        AdhanReminderScopes.Add(new OptionItem<AdhanReminderScope>(AdhanReminderScope.SpecificPrayer, LocalizationManager.Translate("Reminder_Specific")));

        AdhanReminderPrayers.Clear();
        AdhanReminderPrayers.Add(new OptionItem<PrayerId>(PrayerId.Fajr, LocalizationManager.Translate("Prayer_Fajr")));
        AdhanReminderPrayers.Add(new OptionItem<PrayerId>(PrayerId.Dhuhr, LocalizationManager.Translate("Prayer_Dhuhr")));
        AdhanReminderPrayers.Add(new OptionItem<PrayerId>(PrayerId.Asr, LocalizationManager.Translate("Prayer_Asr")));
        AdhanReminderPrayers.Add(new OptionItem<PrayerId>(PrayerId.Maghrib, LocalizationManager.Translate("Prayer_Maghrib")));
        AdhanReminderPrayers.Add(new OptionItem<PrayerId>(PrayerId.Isha, LocalizationManager.Translate("Prayer_Isha")));

        SelectedAdhanSound = AdhanSounds.FirstOrDefault(item => item.Value == currentSound)
            ?? AdhanSounds.FirstOrDefault();
        SelectedVibrationStrength = VibrationStrengths.FirstOrDefault(item => item.Value == currentStrength)
            ?? VibrationStrengths.FirstOrDefault();
        SelectedVibrationPattern = VibrationPatterns.FirstOrDefault(item => item.Value == currentPattern)
            ?? VibrationPatterns.FirstOrDefault();
        SelectedAdhanReminderScope = AdhanReminderScopes.FirstOrDefault(item => item.Value == currentScope)
            ?? AdhanReminderScopes.FirstOrDefault();
        SelectedAdhanReminderPrayer = AdhanReminderPrayers.FirstOrDefault(item => item.Value == currentPrayer)
            ?? AdhanReminderPrayers.FirstOrDefault();
    }

    private void BuildAdhanOverrideOptions() {
        AdhanOverrideSounds.Clear();
        AdhanOverrideSounds.Add(new OptionItem<string>("use_global", LocalizationManager.Translate("UseGlobal")));
        AdhanOverrideSounds.Add(new OptionItem<string>("adhan_default", LocalizationManager.Translate("Sound_Default")));
        AdhanOverrideSounds.Add(new OptionItem<string>("adhan_silent", LocalizationManager.Translate("Sound_Silent")));

        AdhanOverrideVibrations.Clear();
        AdhanOverrideVibrations.Add(new OptionItem<int>(-1, LocalizationManager.Translate("UseGlobal")));
        AdhanOverrideVibrations.Add(new OptionItem<int>(1, LocalizationManager.Translate("Vibration_On")));
        AdhanOverrideVibrations.Add(new OptionItem<int>(0, LocalizationManager.Translate("Vibration_Off")));
    }

    private void LoadAdhanOverrides() {
        _adhanPrayerOverrides.Clear();
        var overrides = _settings.Notifications.PrayerOverrides ?? new List<AdhanPrayerOverride>();
        var lookup = overrides.ToDictionary(item => item.Prayer, item => item);
        var prayers = new[] { PrayerId.Fajr, PrayerId.Dhuhr, PrayerId.Asr, PrayerId.Maghrib, PrayerId.Isha };

        foreach (var prayer in prayers) {
            lookup.TryGetValue(prayer, out var existing);
            var viewModel = new AdhanPrayerOverrideViewModel(prayer);
            var soundKey = existing?.SoundKey ?? "use_global";
            var vibrationValue = existing?.EnableVibration.HasValue == true
                ? (existing.EnableVibration.Value ? 1 : 0)
                : -1;

            viewModel.SelectedSound = AdhanOverrideSounds.FirstOrDefault(item => item.Value == soundKey)
                ?? AdhanOverrideSounds.FirstOrDefault();
            viewModel.SelectedVibration = AdhanOverrideVibrations.FirstOrDefault(item => item.Value == vibrationValue)
                ?? AdhanOverrideVibrations.FirstOrDefault();
            viewModel.PropertyChanged += (_, _) => {
                if (_suspendSave) {
                    return;
                }
                ScheduleSave();
            };
            _adhanPrayerOverrides.Add(viewModel);
        }
    }

    private IReadOnlyList<AdhanPrayerOverride> BuildAdhanOverrides() {
        var results = new List<AdhanPrayerOverride>();
        foreach (var item in _adhanPrayerOverrides) {
            var soundKey = item.SelectedSound?.Value;
            var vibrationValue = item.SelectedVibration?.Value ?? -1;
            var overrideSound = string.Equals(soundKey, "use_global", StringComparison.OrdinalIgnoreCase) ? null : soundKey;
            bool? overrideVibration = vibrationValue switch {
                1 => true,
                0 => false,
                _ => null
            };

            if (overrideSound == null && overrideVibration == null) {
                continue;
            }

            results.Add(new AdhanPrayerOverride {
                Prayer = item.Prayer,
                SoundKey = overrideSound,
                EnableVibration = overrideVibration
            });
        }

        return results;
    }

    private void RefreshAdhanOverridesLocalization() {
        var previous = _suspendSave;
        _suspendSave = true;
        try {
            foreach (var item in _adhanPrayerOverrides) {
                item.RefreshLocalization();
                var soundValue = item.SelectedSound?.Value ?? "use_global";
                var vibrationValue = item.SelectedVibration?.Value ?? -1;
                item.SelectedSound = AdhanOverrideSounds.FirstOrDefault(option => option.Value == soundValue)
                    ?? AdhanOverrideSounds.FirstOrDefault();
                item.SelectedVibration = AdhanOverrideVibrations.FirstOrDefault(option => option.Value == vibrationValue)
                    ?? AdhanOverrideVibrations.FirstOrDefault();
            }
        } finally {
            _suspendSave = previous;
        }
    }

    private void BuildTasbihRepeatModes() {
        var current = SelectedTasbihRepeatMode?.Value ?? TasbihRepeatMode.None;
        TasbihRepeatModes.Clear();
        TasbihRepeatModes.Add(new OptionItem<TasbihRepeatMode>(TasbihRepeatMode.None, LocalizationManager.Translate("TasbihRepeat_None")));
        TasbihRepeatModes.Add(new OptionItem<TasbihRepeatMode>(TasbihRepeatMode.RepeatReset, LocalizationManager.Translate("TasbihRepeat_Reset")));
        TasbihRepeatModes.Add(new OptionItem<TasbihRepeatMode>(TasbihRepeatMode.RepeatContinue, LocalizationManager.Translate("TasbihRepeat_Continue")));
        SelectedTasbihRepeatMode = TasbihRepeatModes.FirstOrDefault(item => item.Value == current)
            ?? TasbihRepeatModes.FirstOrDefault();
    }

    private void EnsureTasbihDefaults() {
        if (_settings.Tasbih.Presets.Count > 0) {
            return;
        }

        _settings = new AppSettings {
            Location = _settings.Location,
            Method = _settings.Method,
            Madhhab = _settings.Madhhab,
            HighLatitudeRule = _settings.HighLatitudeRule,
            Offsets = _settings.Offsets,
            FastingOffsets = _settings.FastingOffsets,
            FastingReminders = _settings.FastingReminders,
            Notifications = _settings.Notifications,
            ClockFormat = _settings.ClockFormat,
            TextScale = _settings.TextScale,
            Tasbih = TasbihDefaults.BuildDefaults(),
            Language = _settings.Language,
            LanguageSelected = _settings.LanguageSelected,
            ThemeMode = _settings.ThemeMode,
            ThemeVariant = _settings.ThemeVariant,
            AccentIndex = _settings.AccentIndex
        };

        _dataService.SaveSettings(_settings);
    }

    private void LoadTasbihPresets() {
        TasbihPresets.Clear();
        foreach (var preset in _settings.Tasbih.Presets) {
            var items = preset.Items.Select(item => new TasbihItemEditorViewModel(item.Text, item.TargetCount)).ToList();
            var viewModel = new TasbihPresetEditorViewModel(preset.Name, preset.RepeatMode, items);
            viewModel.PropertyChanged += (_, _) => ScheduleSave();
            foreach (var item in viewModel.Items) {
                item.PropertyChanged += (_, _) => {
                    RecalculateTasbihStartIndices();
                    ScheduleSave();
                };
            }
            TasbihPresets.Add(viewModel);
        }

        var index = Math.Clamp(_settings.Tasbih.SelectedPresetIndex, 0, Math.Max(0, TasbihPresets.Count - 1));
        SelectedTasbihPreset = TasbihPresets.Count > 0 ? TasbihPresets[index] : null;
        RecalculateTasbihStartIndices();
        RefreshTasbihLocalization();
    }

    private void RecalculateTasbihStartIndices() {
        if (SelectedTasbihPreset == null) {
            return;
        }

        var start = 1;
        foreach (var item in SelectedTasbihPreset.Items) {
            item.StartIndex = start;
            start += Math.Max(0, item.TargetCount);
        }
    }

    private void AddTasbihItem() {
        if (SelectedTasbihPreset == null) {
            return;
        }
        if (string.IsNullOrWhiteSpace(NewTasbihText)) {
            return;
        }
        if (!int.TryParse(NewTasbihCount, out var count) || count <= 0) {
            return;
        }

        var item = new TasbihItemEditorViewModel(NewTasbihText.Trim(), count);
        item.PropertyChanged += (_, _) => RecalculateTasbihStartIndices();
        SelectedTasbihPreset.Items.Add(item);
        NewTasbihText = "";
        NewTasbihCount = "";
        RecalculateTasbihStartIndices();
        ScheduleSave();
    }

    private void AddTasbihPreset() {
        var baseName = string.IsNullOrWhiteSpace(NewTasbihPresetName)
            ? LocalizationManager.Translate("TasbihPreset_New")
            : NewTasbihPresetName.Trim();
        var name = baseName;
        var suffix = 2;
        while (TasbihPresets.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))) {
            name = $"{baseName} {suffix++}";
        }

        var viewModel = new TasbihPresetEditorViewModel(name, TasbihRepeatMode.None, new List<TasbihItemEditorViewModel>());
        viewModel.PropertyChanged += (_, _) => ScheduleSave();
        TasbihPresets.Add(viewModel);
        SelectedTasbihPreset = viewModel;
        NewTasbihPresetName = "";
        RecalculateTasbihStartIndices();
        ScheduleSave();
    }

    private void RemoveTasbihItem(TasbihItemEditorViewModel? item) {
        if (SelectedTasbihPreset == null || item == null) {
            return;
        }
        SelectedTasbihPreset.Items.Remove(item);
        RecalculateTasbihStartIndices();
        ScheduleSave();
    }

    private void MoveTasbihItemUp(TasbihItemEditorViewModel? item) {
        if (SelectedTasbihPreset == null || item == null) {
            return;
        }
        var index = SelectedTasbihPreset.Items.IndexOf(item);
        if (index <= 0) {
            return;
        }
        SelectedTasbihPreset.Items.Move(index, index - 1);
        RecalculateTasbihStartIndices();
        ScheduleSave();
    }

    private void MoveTasbihItemDown(TasbihItemEditorViewModel? item) {
        if (SelectedTasbihPreset == null || item == null) {
            return;
        }
        var index = SelectedTasbihPreset.Items.IndexOf(item);
        if (index < 0 || index >= SelectedTasbihPreset.Items.Count - 1) {
            return;
        }
        SelectedTasbihPreset.Items.Move(index, index + 1);
        RecalculateTasbihStartIndices();
        ScheduleSave();
    }

    private void RefreshTasbihLocalization() {
        foreach (var preset in TasbihPresets) {
            preset.RefreshDisplayName();
            foreach (var item in preset.Items) {
                item.RefreshDisplayText();
            }
        }
    }

    private void IncreaseTextSize() {
        TextScale = Math.Min(TextScale + 1, 6);
        ScheduleSave();
    }

    private void DecreaseTextSize() {
        TextScale = Math.Max(TextScale - 1, -2);
        ScheduleSave();
    }

    private void UpdateTextScaleLabel() {
        var percent = 100 + (TextScale * 7);
        TextScaleLabel = $"{percent}%";
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (_suspendSave) {
            return;
        }

        if (!ShouldAutoSave(e.PropertyName)) {
            return;
        }

#if DEBUG
        if (!string.IsNullOrWhiteSpace(e.PropertyName)) {
            var value = GetType().GetProperty(e.PropertyName)?.GetValue(this);
            _logger.LogEvent("SettingChanged", $"{e.PropertyName}={value}");
        }
#endif

        ScheduleSave();
    }

    private static bool ShouldAutoSave(string? propertyName) {
        return propertyName is nameof(UseGps)
            or nameof(City)
            or nameof(Country)
            or nameof(Latitude)
            or nameof(Longitude)
            or nameof(SelectedMethod)
            or nameof(SelectedMadhhab)
            or nameof(SelectedHighLatitude)
            or nameof(FajrOffset)
            or nameof(SunriseOffset)
            or nameof(DhuhrOffset)
            or nameof(AsrOffset)
            or nameof(MaghribOffset)
            or nameof(IshaOffset)
            or nameof(ImsakOffset)
            or nameof(ImsakAdvance)
            or nameof(IftarDelay)
            or nameof(SelectedClockFormat)
            or nameof(TextScale)
            or nameof(NotificationsEnabled)
            or nameof(VibrationEnabled)
            or nameof(MinutesBefore)
            or nameof(SelectedAdhanSound)
            or nameof(SelectedVibrationStrength)
            or nameof(SelectedVibrationPattern)
            or nameof(SelectedAdhanReminderScope)
            or nameof(SelectedAdhanReminderPrayer)
            or nameof(SelectedTasbihRepeatMode)
            or nameof(SelectedLanguage)
            or nameof(SelectedThemeMode)
            or nameof(SelectedThemeVariant)
            or nameof(SelectedAccent)
            or nameof(SelectedCountry)
            or nameof(SelectedCity);
    }

    private void ScheduleSave() {
        _saveVersion++;
        var version = _saveVersion;
        _ = DebounceSaveAsync(version);
    }

    private async Task DebounceSaveAsync(int version) {
        await Task.Delay(500).ConfigureAwait(false);
        if (version != _saveVersion) {
            return;
        }

        MainThread.BeginInvokeOnMainThread(Save);
    }

    private void ScheduleReverseLookup() {
        _geoVersion++;
        var version = _geoVersion;
        _ = DebounceReverseAsync(version);
    }

    private async Task DebounceReverseAsync(int version) {
        await Task.Delay(700).ConfigureAwait(false);
        if (version != _geoVersion) {
            return;
        }

        if (!double.TryParse(Latitude, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lat)) {
            return;
        }
        if (!double.TryParse(Longitude, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lon)) {
            return;
        }

        var result = await _geoService.ReverseAsync(lat, lon, CancellationToken.None).ConfigureAwait(false);
        MainThread.BeginInvokeOnMainThread(() => {
            _suspendSave = true;
            UseGps = false;
            if (result != null) {
                City = string.IsNullOrWhiteSpace(result.City) ? LocalizationManager.Translate("UnknownCity") : NormalizeName(result.City);
                Country = string.IsNullOrWhiteSpace(result.Country) ? LocalizationManager.Translate("UnknownCountry") : NormalizeName(result.Country);
            } else {
                City = LocalizationManager.Translate("UnknownCity");
                Country = LocalizationManager.Translate("UnknownCountry");
            }
            _suspendSave = false;
            BuildPlaceOptions();
        });
    }

    private async Task RefreshGpsAsync() {
        if (_suspendSave || GpsBusy) {
            return;
        }

        try {
            SetGpsBusy(true);
            var settings = _dataService.LoadSettings();
            settings = new AppSettings {
                Location = new LocationSettings {
                    Mode = LocationMode.Gps,
                    City = settings.Location.City,
                    Country = settings.Location.Country,
                    CountryCode = settings.Location.CountryCode,
                    Latitude = settings.Location.Latitude,
                    Longitude = settings.Location.Longitude,
                    TimeZoneId = settings.Location.TimeZoneId,
                    LastUpdatedUtc = settings.Location.LastUpdatedUtc
                },
                Method = settings.Method,
                Madhhab = settings.Madhhab,
                HighLatitudeRule = settings.HighLatitudeRule,
                Offsets = settings.Offsets,
                FastingOffsets = settings.FastingOffsets,
                FastingReminders = settings.FastingReminders,
                Notifications = settings.Notifications,
                ClockFormat = settings.ClockFormat,
                TextScale = settings.TextScale,
                Tasbih = settings.Tasbih,
                Language = settings.Language,
                LanguageSelected = settings.LanguageSelected,
                ThemeMode = settings.ThemeMode,
                ThemeVariant = settings.ThemeVariant,
                AccentIndex = settings.AccentIndex
            };

            var updated = await _dataService.UpdateLocationAsync(settings, CancellationToken.None).ConfigureAwait(false);
            MainThread.BeginInvokeOnMainThread(() => {
                _suspendSave = true;
                _settings = updated;
                City = NormalizeName(updated.Location.City);
                Country = NormalizeName(updated.Location.Country);
                Latitude = updated.Location.Latitude.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
                Longitude = updated.Location.Longitude.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
                _suspendSave = false;
                BuildPlaceOptions();
                ScheduleSave();
            });
        } catch (Exception ex) {
            _logger.LogException(ex, "SettingsViewModel.RefreshGpsAsync");
        } finally {
            SetGpsBusy(false);
        }
    }

    private void StartGpsLoop() {
        StopGpsLoop();
        _gpsLoopCts = new CancellationTokenSource();
        _ = GpsLoopAsync(_gpsLoopCts.Token);
    }

    private void StopGpsLoop() {
        _gpsLoopCts?.Cancel();
        _gpsLoopCts?.Dispose();
        _gpsLoopCts = null;
    }

    private async Task GpsLoopAsync(CancellationToken token) {
        while (!token.IsCancellationRequested) {
            try {
                await RefreshGpsAsync().ConfigureAwait(false);
            } catch (Exception ex) {
                _logger.LogException(ex, "SettingsViewModel.GpsLoopAsync");
            }
            try {
                await Task.Delay(TimeSpan.FromMinutes(15), token).ConfigureAwait(false);
            } catch (TaskCanceledException) {
                break;
            }
        }
    }

    private void SetGpsBusy(bool value) {
        if (_gpsBusy == value) {
            return;
        }

        _gpsBusy = value;
        RunOnMainThread(() => {
            OnPropertyChanged(nameof(GpsBusy));
            RefreshGpsCommand.ChangeCanExecute();
        });
    }

    private static string NormalizeName(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return "";
        }

        var trimmed = value.Trim();
        for (var len = 1; len <= trimmed.Length / 2; len++) {
            if (trimmed.Length % len != 0) {
                continue;
            }

            var segment = trimmed.Substring(0, len);
            var repeats = trimmed.Length / len;
            var allMatch = true;
            for (var i = 1; i < repeats; i++) {
                if (!string.Equals(segment, trimmed.Substring(i * len, len), StringComparison.Ordinal)) {
                    allMatch = false;
                    break;
                }
            }
            if (allMatch) {
                return segment;
            }
        }

        return trimmed;
    }
}
