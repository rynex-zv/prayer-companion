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
    private OptionItem<int>? _selectedImsakReminderUnit;
    private OptionItem<int>? _selectedImsakReminderDirection;
    private OptionItem<int>? _selectedIftarReminderUnit;
    private OptionItem<int>? _selectedIftarReminderDirection;

    public SettingsViewModel(PrayerDataService dataService, GeoService geoService) {
        _dataService = dataService;
        _geoService = geoService;
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
        BuildLocalizedPickers();
        BuildReminderOptions();
        RefreshGpsCommand = new Command(async () => await RefreshGpsAsync(), () => !GpsBusy);
        AddImsakReminderCommand = new Command(AddImsakReminder);
        AddIftarReminderCommand = new Command(AddIftarReminder);
        RemoveImsakReminderCommand = new Command<ReminderOffsetItem>(RemoveImsakReminder);
        RemoveIftarReminderCommand = new Command<ReminderOffsetItem>(RemoveIftarReminder);

        Load();
        PropertyChanged += OnSettingsPropertyChanged;
        LocalizationManager.LanguageChanged += (_, _) => {
            BuildLocalizedPickers();
            BuildPlaceOptions();
            BuildReminderOptions();
            RebuildReminderLabels();
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
    public Command RefreshGpsCommand { get; }
    public Command AddImsakReminderCommand { get; }
    public Command AddIftarReminderCommand { get; }
    public Command RemoveImsakReminderCommand { get; }
    public Command RemoveIftarReminderCommand { get; }
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

    public bool GpsBusy {
        get => _gpsBusy;
        set {
            if (SetProperty(ref _gpsBusy, value)) {
                MainThread.BeginInvokeOnMainThread(() => RefreshGpsCommand.ChangeCanExecute());
            }
        }
    }
    public string City {
        get => _city;
        set => SetProperty(ref _city, value);
    }

    public string Country {
        get => _country;
        set => SetProperty(ref _country, value);
    }

    public string Latitude {
        get => _latitude;
        set {
            if (SetProperty(ref _latitude, value) && !_suspendSave && !UseGps) {
                ScheduleReverseLookup();
            }
        }
    }

    public string Longitude {
        get => _longitude;
        set {
            if (SetProperty(ref _longitude, value) && !_suspendSave && !UseGps) {
                ScheduleReverseLookup();
            }
        }
    }

    public PlaceOption? SelectedCountry {
        get => _selectedCountry;
        set {
            if (SetProperty(ref _selectedCountry, value)) {
                _ = ApplyCountrySelectionAsync(value);
            }
        }
    }

    public PlaceOption? SelectedCity {
        get => _selectedCity;
        set {
            if (SetProperty(ref _selectedCity, value)) {
                ApplyCitySelection(value);
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
        NotificationsEnabled = _settings.Notifications.EnableAdhan;
        VibrationEnabled = _settings.Notifications.EnableVibration;
        MinutesBefore = _settings.Notifications.MinutesBefore.ToString();
        SelectedLanguage = Languages.FirstOrDefault(item => item.Value == _settings.Language)
            ?? Languages.FirstOrDefault();
        SelectedThemeMode = ThemeModes.FirstOrDefault(item => item.Value == _settings.ThemeMode)
            ?? ThemeModes.FirstOrDefault();
        SelectedThemeVariant = ThemeVariants.FirstOrDefault(item => item.Value == _settings.ThemeVariant)
            ?? ThemeVariants.FirstOrDefault();
        UpdateAccentOptions(SelectedThemeVariant?.Value ?? ThemeVariant.A, _settings.AccentIndex);
        BuildPlaceOptions();
        _suspendSave = false;
        if (UseGps) {
            StartGpsLoop();
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
            SoundKey = "adhan_default"
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
            Language = SelectedLanguage?.Value ?? "auto",
            LanguageSelected = true,
            ThemeMode = SelectedThemeMode?.Value ?? ThemeMode.Auto,
            ThemeVariant = SelectedThemeVariant?.Value ?? ThemeVariant.A,
            AccentIndex = SelectedAccent?.Index ?? 0
        };

        _dataService.SaveSettings(_settings);
        LocalizationManager.SetLanguage(_settings.Language);
        ThemeManager.ApplyTheme(_settings);
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

        CountryOptions.Clear();
        foreach (var country in known.GroupBy(item => NormalizeName(item.Country)).Select(group => group.First())) {
            var countryName = NormalizeName(country.Country);
            var cityName = NormalizeName(country.City);
            CountryOptions.Add(new PlaceOption(countryName, cityName, country.Latitude, country.Longitude, true));
        }

        if (CountryOptions.Count == 0) {
            CountryOptions.Add(new PlaceOption(LocalizationManager.Translate("UnknownCountry"), "", 0, 0, true));
        }

        SelectedCountry = CountryOptions.FirstOrDefault(option => option.Country == current.Country)
            ?? CountryOptions.FirstOrDefault();

        UpdateCityOptions(current.Country);
    }

    private void UpdateCityOptions(string? country) {
        CityOptions.Clear();
        var known = _geoService.GetKnownPlaces()
            .Where(item => string.Equals(NormalizeName(item.Country), NormalizeName(country ?? ""), StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var city in known.Where(item => !string.IsNullOrWhiteSpace(item.City))) {
            CityOptions.Add(new PlaceOption(NormalizeName(city.Country), NormalizeName(city.City), city.Latitude, city.Longitude, false));
        }

        if (CityOptions.Count == 0 && !string.IsNullOrWhiteSpace(country)) {
            CityOptions.Add(new PlaceOption(country, LocalizationManager.Translate("UnknownCity"), 0, 0, false));
        }

        SelectedCity = CityOptions.FirstOrDefault(option => option.City == _settings.Location.City)
            ?? CityOptions.FirstOrDefault();
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

    private void RebuildReminderLabels() {
        var imsak = ImsakReminders.Select(item => item.Minutes).ToList();
        var iftar = IftarReminders.Select(item => item.Minutes).ToList();
        ImsakReminders.Clear();
        foreach (var minutes in imsak) {
            ImsakReminders.Add(new ReminderOffsetItem(minutes, BuildReminderLabel(minutes)));
        }
        IftarReminders.Clear();
        foreach (var minutes in iftar) {
            IftarReminders.Add(new ReminderOffsetItem(minutes, BuildReminderLabel(minutes)));
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

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (_suspendSave) {
            return;
        }

        if (!ShouldAutoSave(e.PropertyName)) {
            return;
        }

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
            or nameof(NotificationsEnabled)
            or nameof(VibrationEnabled)
            or nameof(MinutesBefore)
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
            GpsBusy = true;
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
                Language = settings.Language,
                LanguageSelected = settings.LanguageSelected,
                ThemeMode = settings.ThemeMode,
                ThemeVariant = settings.ThemeVariant,
                AccentIndex = settings.AccentIndex
            };

            var updated = await _dataService.UpdateLocationAsync(settings, CancellationToken.None).ConfigureAwait(false);
            MainThread.BeginInvokeOnMainThread(() => {
                _suspendSave = true;
                City = NormalizeName(updated.Location.City);
                Country = NormalizeName(updated.Location.Country);
                Latitude = updated.Location.Latitude.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
                Longitude = updated.Location.Longitude.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
                _suspendSave = false;
                BuildPlaceOptions();
                ScheduleSave();
            });
        } finally {
            GpsBusy = false;
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
            await RefreshGpsAsync().ConfigureAwait(false);
            try {
                await Task.Delay(TimeSpan.FromMinutes(2), token).ConfigureAwait(false);
            } catch (TaskCanceledException) {
                break;
            }
        }
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
