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
        BuildLocalizedPickers();

        Load();
        PropertyChanged += OnSettingsPropertyChanged;
        LocalizationManager.LanguageChanged += (_, _) => {
            BuildLocalizedPickers();
            BuildPlaceOptions();
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
    public bool UseGps {
        get => _useGps;
        set {
            if (SetProperty(ref _useGps, value) && !_suspendSave) {
                ScheduleSave();
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
    }

    private void Save() {
        var mode = UseGps ? LocationMode.Gps : LocationMode.Manual;
        var location = new LocationSettings {
            Mode = mode,
            City = City?.Trim() ?? "",
            Country = Country?.Trim() ?? "",
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
        foreach (var country in known.GroupBy(item => item.Country).Select(group => group.First())) {
            CountryOptions.Add(new PlaceOption(country.Country, country.City, country.Latitude, country.Longitude, true));
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
            .Where(item => string.Equals(item.Country, country, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var city in known.Where(item => !string.IsNullOrWhiteSpace(item.City))) {
            CityOptions.Add(new PlaceOption(city.Country, city.City, city.Latitude, city.Longitude, false));
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
        Country = country;
        if (result != null) {
            City = string.IsNullOrWhiteSpace(result.City) ? LocalizationManager.Translate("UnknownCity") : result.City;
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
            City = option.City;
            Country = option.Country;
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
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.MuslimWorldLeague, LocalizationManager.Translate("Method_MuslimWorldLeague")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.UmmAlQura, LocalizationManager.Translate("Method_UmmAlQura")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Egypt, LocalizationManager.Translate("Method_Egypt")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Karachi, LocalizationManager.Translate("Method_Karachi")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Isna, LocalizationManager.Translate("Method_Isna")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Turkey, LocalizationManager.Translate("Method_Turkey")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Kuwait, LocalizationManager.Translate("Method_Kuwait")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Qatar, LocalizationManager.Translate("Method_Qatar")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Tehran, LocalizationManager.Translate("Method_Tehran")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Gulf, LocalizationManager.Translate("Method_Gulf")));
        Methods.Add(new OptionItem<CalculationMethod>(CalculationMethod.Singapore, LocalizationManager.Translate("Method_Singapore")));

        Madhhabs.Clear();
        Madhhabs.Add(new OptionItem<Madhhab>(Madhhab.Shafi, LocalizationManager.Translate("Madhhab_Shafi")));
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
                City = string.IsNullOrWhiteSpace(result.City) ? LocalizationManager.Translate("UnknownCity") : result.City;
                Country = string.IsNullOrWhiteSpace(result.Country) ? LocalizationManager.Translate("UnknownCountry") : result.Country;
            } else {
                City = LocalizationManager.Translate("UnknownCity");
                Country = LocalizationManager.Translate("UnknownCountry");
            }
            _suspendSave = false;
            BuildPlaceOptions();
        });
    }
}
