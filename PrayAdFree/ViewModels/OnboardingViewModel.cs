using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Microsoft.Maui.Networking;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Models;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.ViewModels;

public sealed class OnboardingViewModel : ViewModelBase {
    private static readonly AppPermissionKind[] PermissionOrder = {
        AppPermissionKind.Notifications,
        AppPermissionKind.ExactAlarms,
        AppPermissionKind.FullScreenIntents,
        AppPermissionKind.DisplayOverApps
    };

    private readonly SettingsService _settingsService;
    private readonly IAppPermissionCenterService _permissionCenterService;
    private readonly IStartupNavigationService _startupNavigationService;
    private readonly INotificationBootstrapper _notificationBootstrapper;
    private readonly IIpLocationService _ipLocationService;
    private readonly INetworkPrivacyService _networkPrivacyService;
    private readonly IAppLogger _logger;
    private readonly ObservableCollection<AppPermissionItemViewModel> _permissionSlides = new();
    private OptionItem<string>? _selectedLanguage;
    private int _currentStepIndex;
    private bool _isBusy;
    private bool _locationPermissionGranted;
    private bool _showVpnWarning;
    private int _languageVersion;

    public OnboardingViewModel(
        SettingsService settingsService,
        LocationSetupViewModel locationSetup,
        IAppPermissionCenterService permissionCenterService,
        IStartupNavigationService startupNavigationService,
        INotificationBootstrapper notificationBootstrapper,
        IIpLocationService ipLocationService,
        INetworkPrivacyService networkPrivacyService,
        IAppLogger logger) {
        _settingsService = settingsService;
        LocationSetup = locationSetup;
        _permissionCenterService = permissionCenterService;
        _startupNavigationService = startupNavigationService;
        _notificationBootstrapper = notificationBootstrapper;
        _ipLocationService = ipLocationService;
        _networkPrivacyService = networkPrivacyService;
        _logger = logger;
        Languages = new ObservableCollection<OptionItem<string>>();
        PermissionSlides = new ReadOnlyObservableCollection<AppPermissionItemViewModel>(_permissionSlides);
        AdvanceCommand = new Command(async () => await AdvanceAsync(), () => CanAdvance && !IsBusy);
        BackCommand = new Command(() => CurrentStepIndex--, () => CanGoBack && !IsBusy);
        RequestCurrentPermissionCommand = new Command(async () => await RequestCurrentPermissionAsync(), () => CurrentPermission != null && !IsBusy);
        RequestLocationPermissionCommand = new Command(async () => await RequestLocationPermissionAsync(), () => !IsBusy);

        LocationSetup.PropertyChanged += OnLocationSetupPropertyChanged;
        BuildLanguages();
    }

    public ObservableCollection<OptionItem<string>> Languages { get; }
    public ReadOnlyObservableCollection<AppPermissionItemViewModel> PermissionSlides { get; }
    public LocationSetupViewModel LocationSetup { get; }
    public Command AdvanceCommand { get; }
    public Command BackCommand { get; }
    public Command RequestCurrentPermissionCommand { get; }
    public Command RequestLocationPermissionCommand { get; }

    public OptionItem<string>? SelectedLanguage {
        get => _selectedLanguage;
        set {
            if (!SetProperty(ref _selectedLanguage, value) || value == null) {
                return;
            }

            _ = ApplyLanguageSelectionAsync(value.Value);
            RefreshCommandStates();
        }
    }

    public int CurrentStepIndex {
        get => _currentStepIndex;
        private set {
            if (!SetProperty(ref _currentStepIndex, value)) {
                return;
            }

            OnPropertyChanged(nameof(IsOnLanguageStep));
            OnPropertyChanged(nameof(IsOnPermissionStep));
            OnPropertyChanged(nameof(IsOnLocationStep));
            OnPropertyChanged(nameof(CurrentPermission));
            OnPropertyChanged(nameof(CurrentStepTitle));
            OnPropertyChanged(nameof(CurrentStepSubtitle));
            OnPropertyChanged(nameof(PrimaryButtonText));
            OnPropertyChanged(nameof(ShowManualLocationSetup));
            OnPropertyChanged(nameof(TotalSteps));
            OnPropertyChanged(nameof(StepCounterText));
            RefreshCommandStates();
        }
    }

    public bool IsBusy {
        get => _isBusy;
        private set {
            if (!MainThread.IsMainThread) {
                MainThread.BeginInvokeOnMainThread(() => IsBusy = value);
                return;
            }

            if (SetProperty(ref _isBusy, value)) {
                RefreshCommandStates();
            }
        }
    }

    public bool IsOnLanguageStep => CurrentStepIndex == 0;
    public bool IsOnPermissionStep => CurrentStepIndex > 0 && CurrentStepIndex <= _permissionSlides.Count;
    public bool IsOnLocationStep => CurrentStepIndex == TotalSteps - 1;

    public bool LocationPermissionGranted {
        get => _locationPermissionGranted;
        private set {
            if (SetProperty(ref _locationPermissionGranted, value)) {
                OnPropertyChanged(nameof(CurrentStepSubtitle));
                OnPropertyChanged(nameof(ShowManualLocationSetup));
                RefreshCommandStates();
            }
        }
    }

    public bool ShowManualLocationSetup => IsOnLocationStep;

    public bool ShowVpnWarning {
        get => _showVpnWarning;
        private set => SetProperty(ref _showVpnWarning, value);
    }

    public string VpnWarningText => LocalizationManager.Translate("OnboardingVpnWarning");

    public string CurrentStepTitle {
        get {
            if (IsOnLanguageStep) {
                return LocalizationManager.Translate("LanguageTitle");
            }

            if (IsOnPermissionStep) {
                return CurrentPermission?.Title ?? LocalizationManager.Translate("PermissionsTitle");
            }

            return LocalizationManager.Translate("OnboardingLocationTitle");
        }
    }

    public string CurrentStepSubtitle {
        get {
            if (IsOnLanguageStep) {
                return LocalizationManager.Translate("LanguageSubtitle");
            }

            if (IsOnPermissionStep) {
                return CurrentPermission?.FallbackText ?? string.Empty;
            }

            return LocationPermissionGranted
                ? LocalizationManager.Translate("OnboardingLocationGranted")
                : LocalizationManager.Translate("OnboardingLocationRequired");
        }
    }

    public AppPermissionItemViewModel? CurrentPermission {
        get {
            if (!IsOnPermissionStep) {
                return null;
            }

            var index = CurrentStepIndex - 1;
            return index >= 0 && index < _permissionSlides.Count
                ? _permissionSlides[index]
                : null;
        }
    }

    public bool CanGoBack => CurrentStepIndex > 0;

    public bool CanAdvance {
        get {
            if (IsOnLanguageStep) {
                return SelectedLanguage != null;
            }

            if (IsOnLocationStep) {
                return CanCompleteOnboarding;
            }

            return true;
        }
    }

    public bool CanCompleteOnboarding => LocationPermissionGranted || LocationSetup.HasUsableLocation;

    public int TotalSteps => _permissionSlides.Count + 2;

    public string StepCounterText => string.Format(
        LocalizationManager.Translate("OnboardingProgress"),
        CurrentStepIndex + 1,
        TotalSteps);

    public string PrimaryButtonText => IsOnLocationStep
        ? LocalizationManager.Translate("OnboardingFinish")
        : LocalizationManager.Translate("OnboardingNext");

    public async Task InitializeAsync() {
        var settings = _settingsService.Load();
        var preferredLanguage = string.IsNullOrWhiteSpace(settings.Language) || settings.Language == "auto"
            ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            : settings.Language;
        SelectedLanguage = Languages.FirstOrDefault(item => item.Value.Equals(preferredLanguage, StringComparison.OrdinalIgnoreCase))
            ?? Languages.FirstOrDefault(item => item.Value.Equals("en", StringComparison.OrdinalIgnoreCase))
            ?? Languages.FirstOrDefault();

        LocationSetup.Load(settings.Location, startGpsTracking: false);
        await RefreshPermissionSlidesAsync().ConfigureAwait(false);
        await RefreshLocationStepAsync().ConfigureAwait(false);
        if (LocationPermissionGranted) {
            await LocationSetup.RefreshGpsAsync().ConfigureAwait(false);
        } else {
            await TryAutofillLocationFromNetworkAsync().ConfigureAwait(false);
        }
        await _startupNavigationService.PrepareShellAsync(settings).ConfigureAwait(false);
        CurrentStepIndex = Math.Min(CurrentStepIndex, TotalSteps - 1);
        RefreshCommandStates();
    }

    private async Task AdvanceAsync() {
        if (IsBusy) {
            return;
        }

        if (IsOnLocationStep) {
            await CompleteAsync().ConfigureAwait(false);
            return;
        }

        CurrentStepIndex = Math.Min(CurrentStepIndex + 1, TotalSteps - 1);
    }

    private async Task CompleteAsync() {
        if (!CanCompleteOnboarding) {
            return;
        }

        try {
            IsBusy = true;
            var current = _settingsService.Load();
            var updated = new AppSettings {
                Location = LocationSetup.BuildLocationSettings(current.Location),
                Method = current.Method,
                Madhhab = current.Madhhab,
                HighLatitudeRule = current.HighLatitudeRule,
                SunAngles = current.SunAngles,
                Offsets = current.Offsets,
                FastingOffsets = current.FastingOffsets,
                FastingReminders = current.FastingReminders,
                Notifications = current.Notifications,
                AlarmReminders = current.AlarmReminders,
                Qibla = current.Qibla,
                ClockFormat = current.ClockFormat,
                TextScale = current.TextScale,
                Tasbih = current.Tasbih,
                Language = SelectedLanguage?.Value ?? current.Language,
                LanguageSelected = true,
                ThemeMode = current.ThemeMode,
                AccentIndex = current.AccentIndex,
                OnboardingCompleted = true
            };
            _settingsService.Save(updated);
            await _startupNavigationService.PrepareShellAsync(updated).ConfigureAwait(false);
            await _notificationBootstrapper.EnsureScheduledAsync("OnboardingCompleted", requestPermissions: false).ConfigureAwait(false);
            await _startupNavigationService.ActivateShellAsync(updated).ConfigureAwait(false);
        } catch (Exception ex) {
            _logger.LogException(ex, "OnboardingViewModel.CompleteAsync");
        } finally {
            IsBusy = false;
        }
    }

    private async Task RequestCurrentPermissionAsync() {
        if (CurrentPermission == null || IsBusy) {
            return;
        }

        try {
            IsBusy = true;
            await _permissionCenterService.ResolveAsync(CurrentPermission.Kind).ConfigureAwait(false);
            await RefreshPermissionSlidesAsync().ConfigureAwait(false);
        } catch (Exception ex) {
            _logger.LogException(ex, "OnboardingViewModel.RequestCurrentPermissionAsync");
        } finally {
            IsBusy = false;
        }
    }

    private async Task RequestLocationPermissionAsync() {
        if (IsBusy) {
            return;
        }

        try {
            IsBusy = true;
            await _permissionCenterService.ResolveAsync(AppPermissionKind.Location).ConfigureAwait(false);
            await RefreshLocationStepAsync().ConfigureAwait(false);
            if (LocationPermissionGranted) {
                await LocationSetup.RefreshGpsAsync().ConfigureAwait(false);
            } else {
                await TryAutofillLocationFromNetworkAsync().ConfigureAwait(false);
            }
        } catch (Exception ex) {
            _logger.LogException(ex, "OnboardingViewModel.RequestLocationPermissionAsync");
        } finally {
            IsBusy = false;
        }
    }

    private async Task RefreshPermissionSlidesAsync() {
        var snapshots = await _permissionCenterService.GetSnapshotsAsync().ConfigureAwait(false);
        var lookup = snapshots
            .Where(item => item.IsSupported)
            .ToDictionary(item => item.Kind);

        RunOnMainThread(() => {
            _permissionSlides.Clear();
            foreach (var kind in PermissionOrder) {
                if (!lookup.TryGetValue(kind, out var snapshot)) {
                    continue;
                }

                _permissionSlides.Add(BuildPermissionItem(snapshot));
            }

            OnPropertyChanged(nameof(PermissionSlides));
            OnPropertyChanged(nameof(CurrentPermission));
            OnPropertyChanged(nameof(TotalSteps));
            OnPropertyChanged(nameof(StepCounterText));
            OnPropertyChanged(nameof(CurrentStepTitle));
            OnPropertyChanged(nameof(CurrentStepSubtitle));
            RefreshCommandStates();
        });
    }

    private async Task RefreshLocationStepAsync() {
        var snapshot = (await _permissionCenterService.GetSnapshotsAsync().ConfigureAwait(false))
            .FirstOrDefault(item => item.Kind == AppPermissionKind.Location);
        LocationPermissionGranted = snapshot.IsSupported && snapshot.IsGranted;
        if (!LocationPermissionGranted && LocationSetup.UseGps) {
            RunOnMainThread(() => {
                LocationSetup.UseGps = false;
            });
        }
        UpdateVpnWarning();
        OnPropertyChanged(nameof(CanCompleteOnboarding));
        OnPropertyChanged(nameof(CurrentStepSubtitle));
        OnPropertyChanged(nameof(ShowManualLocationSetup));
        RefreshCommandStates();
    }

    private async Task TryAutofillLocationFromNetworkAsync() {
        if (LocationPermissionGranted ||
            LocationSetup.HasUsableLocation ||
            LocationSetup.HasUserEditedLocation ||
            Connectivity.Current.NetworkAccess != NetworkAccess.Internet) {
            UpdateVpnWarning();
            return;
        }

        try {
            var location = await _ipLocationService.GetCurrentLocationAsync(CancellationToken.None).ConfigureAwait(false);
            if (location == null ||
                LocationSetup.HasUsableLocation ||
                LocationSetup.HasUserEditedLocation ||
                LocationPermissionGranted) {
                UpdateVpnWarning();
                return;
            }

            RunOnMainThread(() => {
                LocationSetup.ApplyAutofillLocation(location);
                OnPropertyChanged(nameof(CanCompleteOnboarding));
                RefreshCommandStates();
                UpdateVpnWarning();
            });
        } catch (Exception ex) {
            _logger.LogException(ex, "OnboardingViewModel.TryAutofillLocationFromNetworkAsync");
            UpdateVpnWarning();
        }
    }

    private void UpdateVpnWarning() {
        var show = !LocationPermissionGranted &&
            !LocationSetup.HasUserEditedLocation &&
            _networkPrivacyService.IsVpnActive();

        RunOnMainThread(() => {
            ShowVpnWarning = show;
            OnPropertyChanged(nameof(VpnWarningText));
        });
    }

    private async Task ApplyLanguageSelectionAsync(string language) {
        var version = ++_languageVersion;
        try {
            var current = _settingsService.Load();
            var updated = new AppSettings {
                Location = current.Location,
                Method = current.Method,
                Madhhab = current.Madhhab,
                HighLatitudeRule = current.HighLatitudeRule,
                SunAngles = current.SunAngles,
                Offsets = current.Offsets,
                FastingOffsets = current.FastingOffsets,
                FastingReminders = current.FastingReminders,
                Notifications = current.Notifications,
                AlarmReminders = current.AlarmReminders,
                Qibla = current.Qibla,
                ClockFormat = current.ClockFormat,
                TextScale = current.TextScale,
                Tasbih = current.Tasbih,
                Language = language,
                LanguageSelected = true,
                ThemeMode = current.ThemeMode,
                AccentIndex = current.AccentIndex,
                OnboardingCompleted = current.OnboardingCompleted
            };
            _settingsService.Save(updated);
            LocalizationManager.SetLanguage(language);
            LocationSetup.RefreshLocalizedPlaceOptions();
            await RefreshPermissionSlidesAsync().ConfigureAwait(false);
            if (version == _languageVersion) {
                await _startupNavigationService.PrepareShellAsync(updated).ConfigureAwait(false);
                RunOnMainThread(() => {
                    OnPropertyChanged(nameof(StepCounterText));
                    OnPropertyChanged(nameof(CurrentStepTitle));
                    OnPropertyChanged(nameof(CurrentStepSubtitle));
                    OnPropertyChanged(nameof(PrimaryButtonText));
                });
            }
        } catch (Exception ex) {
            _logger.LogException(ex, "OnboardingViewModel.ApplyLanguageSelectionAsync");
        }
    }

    private void BuildLanguages() {
        Languages.Clear();
        foreach (var language in LocalizationManager.GetAvailableLanguages()) {
            Languages.Add(new OptionItem<string>(language.Code, language.Name));
        }

        if (Languages.Count == 0) {
            Languages.Add(new OptionItem<string>("en", "English"));
            Languages.Add(new OptionItem<string>("ar", "Arabic"));
            Languages.Add(new OptionItem<string>("fr", "French"));
            Languages.Add(new OptionItem<string>("tr", "Turkish"));
            Languages.Add(new OptionItem<string>("es", "Spanish"));
        }
    }

    private void OnLocationSetupPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName is nameof(LocationSetupViewModel.HasUsableLocation)
            or nameof(LocationSetupViewModel.City)
            or nameof(LocationSetupViewModel.Country)
            or nameof(LocationSetupViewModel.Latitude)
            or nameof(LocationSetupViewModel.Longitude)
            or nameof(LocationSetupViewModel.UseGps)
            or nameof(LocationSetupViewModel.HasUserEditedLocation)) {
            OnPropertyChanged(nameof(CanCompleteOnboarding));
            OnPropertyChanged(nameof(ShowManualLocationSetup));
            UpdateVpnWarning();
            RefreshCommandStates();
        }
    }

    private void RefreshCommandStates() {
        RunOnMainThread(() => {
            AdvanceCommand.ChangeCanExecute();
            BackCommand.ChangeCanExecute();
            RequestCurrentPermissionCommand.ChangeCanExecute();
            RequestLocationPermissionCommand.ChangeCanExecute();
            OnPropertyChanged(nameof(CanAdvance));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanCompleteOnboarding));
        });
    }

    private static AppPermissionItemViewModel BuildPermissionItem(AppPermissionSnapshot snapshot) {
        return new AppPermissionItemViewModel(snapshot.Kind) {
            Title = GetTitle(snapshot.Kind),
            Description = GetDescription(snapshot.Kind),
            RoleText = LocalizationManager.Translate(snapshot.IsCritical
                ? "PermissionCategory_Critical"
                : "PermissionCategory_Optional"),
            FallbackText = GetFallbackDescription(snapshot.Kind),
            IsCritical = snapshot.IsCritical,
            IsGranted = snapshot.IsGranted,
            StatusText = LocalizationManager.Translate(snapshot.IsGranted
                ? "PermissionStatus_Enabled"
                : "PermissionStatus_Disabled"),
            ActionText = LocalizationManager.Translate(snapshot.IsGranted || snapshot.UsesSettingsFlow
                ? "PermissionAction_OpenSettings"
                : "PermissionAction_Request")
        };
    }

    private static string GetTitle(AppPermissionKind kind) {
        return kind switch {
            AppPermissionKind.Notifications => LocalizationManager.Translate("PermissionsNotificationsTitle"),
            AppPermissionKind.FullScreenIntents => LocalizationManager.Translate("PermissionsFullScreenIntentTitle"),
            AppPermissionKind.DisplayOverApps => LocalizationManager.Translate("PermissionsOverlayTitle"),
            AppPermissionKind.ExactAlarms => LocalizationManager.Translate("PermissionsExactAlarmTitle"),
            AppPermissionKind.Location => LocalizationManager.Translate("PermissionsLocationTitle"),
            _ => LocalizationManager.Translate("PermissionsTitle")
        };
    }

    private static string GetDescription(AppPermissionKind kind) {
        return kind switch {
            AppPermissionKind.Notifications => LocalizationManager.Translate("PermissionsNotificationsDescription"),
            AppPermissionKind.FullScreenIntents => LocalizationManager.Translate("PermissionsFullScreenIntentDescription"),
            AppPermissionKind.DisplayOverApps => LocalizationManager.Translate("PermissionsOverlayDescription"),
            AppPermissionKind.ExactAlarms => LocalizationManager.Translate("PermissionsExactAlarmDescription"),
            AppPermissionKind.Location => LocalizationManager.Translate("PermissionsLocationDescription"),
            _ => string.Empty
        };
    }

    private static string GetFallbackDescription(AppPermissionKind kind) {
        return kind switch {
            AppPermissionKind.Notifications => LocalizationManager.Translate("PermissionsNotificationsFallback"),
            AppPermissionKind.FullScreenIntents => LocalizationManager.Translate("PermissionsFullScreenIntentFallback"),
            AppPermissionKind.DisplayOverApps => LocalizationManager.Translate("PermissionsOverlayFallback"),
            AppPermissionKind.ExactAlarms => LocalizationManager.Translate("PermissionsExactAlarmFallback"),
            AppPermissionKind.Location => LocalizationManager.Translate("PermissionsLocationFallback"),
            _ => string.Empty
        };
    }

    private static void RunOnMainThread(Action action) {
        if (MainThread.IsMainThread) {
            action();
        } else {
            MainThread.BeginInvokeOnMainThread(action);
        }
    }
}
