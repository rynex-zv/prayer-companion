using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Models;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.ViewModels;

public sealed class TasbihViewModel : ViewModelBase, ITasbihProjectionSource {
    private readonly PrayerDataService _dataService;
    private readonly IAppLogger _logger;
    private readonly TasbihProgressCalculator _progressCalculator = new();
    private AppSettings _settings = new();
    private int _count;
    private string _currentPhrase = "";
    private string _progressText = "";
    private TasbihPresetItem? _selectedPreset;
    private bool _isPresetSelectionEnabled = true;
    private bool _suspendSelectionSave;
    private bool _suppressReload;

    public TasbihViewModel(PrayerDataService dataService, IAppLogger logger) {
        _dataService = dataService;
        _logger = logger;
        Presets = new ObservableCollection<TasbihPresetItem>();
        PresetItems = new ObservableCollection<TasbihPresetItemEntry>();
        IncrementCommand = new Command(Increment);
        ResetCommand = new Command(Reset);
        LoadPresets();
        LocalizationManager.LanguageChanged += (_, _) => RunOnMainThread(LoadPresets);
        _dataService.SettingsChanged += OnSettingsChanged;
    }

    public ObservableCollection<TasbihPresetItem> Presets { get; }
    public ObservableCollection<TasbihPresetItemEntry> PresetItems { get; }
    public Command IncrementCommand { get; }
    public Command ResetCommand { get; }

    public int Count {
        get => _count;
        set {
            if (SetProperty(ref _count, value)) {
                IsPresetSelectionEnabled = _count == 0;
            }
        }
    }

    public string CurrentPhrase {
        get => _currentPhrase;
        set => SetProperty(ref _currentPhrase, value);
    }

    public string ProgressText {
        get => _progressText;
        set => SetProperty(ref _progressText, value);
    }

    public bool IsPresetSelectionEnabled {
        get => _isPresetSelectionEnabled;
        set => SetProperty(ref _isPresetSelectionEnabled, value);
    }

    public TasbihPresetItem? SelectedPreset {
        get => _selectedPreset;
        set {
            if (SetProperty(ref _selectedPreset, value)) {
                Count = 0;
                BuildPresetItems();
                UpdateCurrentPhrase();
                if (!_suspendSelectionSave) {
                    SaveSelectedPreset();
                }
            }
        }
    }

    public void Increment() {
        if (SelectedPreset == null) {
            return;
        }

        var preset = ToSettings(SelectedPreset);
        if (_progressCalculator.GetTotalTarget(preset) == 0) {
            return;
        }

#if DEBUG
        _logger.LogEvent("TasbihIncrement", $"Preset={SelectedPreset.Name} Count={Count + 1}");
#endif
        Count = _progressCalculator.GetNextCount(preset, Count);
        UpdateCurrentPhrase();
    }

    public void Reset() {
        Count = 0;
        TryVibrateReset();
#if DEBUG
        _logger.LogEvent("TasbihReset", SelectedPreset?.Name ?? "None");
#endif
        UpdateCurrentPhrase();
    }

    public void SelectPreset(int index) {
        if (index >= 0 && index < Presets.Count) SelectedPreset = Presets[index];
    }

    IReadOnlyList<TasbihPresetItem> ITasbihProjectionSource.Presets => Presets;

    private void LoadPresets() {
        _settings = _dataService.LoadSettings();
        if (_settings.Tasbih.Presets.Count == 0) {
            _settings = new AppSettings {
                Location = _settings.Location,
                Method = _settings.Method,
                Madhhab = _settings.Madhhab,
                HighLatitudeRule = _settings.HighLatitudeRule,
                SunAngles = _settings.SunAngles,
                Offsets = _settings.Offsets,
                FastingOffsets = _settings.FastingOffsets,
                FastingReminders = _settings.FastingReminders,
                Notifications = _settings.Notifications,
                AlarmReminders = _settings.AlarmReminders,
                Qibla = _settings.Qibla,
                ClockFormat = _settings.ClockFormat,
                TextScale = _settings.TextScale,
                Tasbih = PrayAdFree.Core.Services.TasbihDefaults.BuildDefaults(),
                Language = _settings.Language,
                LanguageSelected = _settings.LanguageSelected,
                ThemeMode = _settings.ThemeMode,
                AccentIndex = _settings.AccentIndex,
                OnboardingCompleted = _settings.OnboardingCompleted
            };
            _dataService.SaveSettings(_settings);
        }

        var presets = _settings.Tasbih.Presets
            .Select(preset => new TasbihPresetItem(
                TranslateValue(preset.Name),
                preset.RepeatMode,
                preset.Items
                    .Select(item => new TasbihItemSettings {
                        Text = TranslateValue(item.Text),
                        TargetCount = item.TargetCount
                    })
                    .ToList()))
            .ToList();
        var index = Math.Clamp(_settings.Tasbih.SelectedPresetIndex, 0, Math.Max(0, presets.Count - 1));
        ApplyPresets(presets, index);
    }

    private void BuildPresetItems() {
        PresetItems.Clear();
        if (SelectedPreset == null) {
            return;
        }

        foreach (var item in SelectedPreset.Items) {
            PresetItems.Add(new TasbihPresetItemEntry(TranslateValue(item.Text), item.TargetCount));
        }
    }

    private void UpdateCurrentPhrase() {
        if (SelectedPreset == null || SelectedPreset.Items.Count == 0) {
            CurrentPhrase = LocalizationManager.Translate("Tasbih_Empty");
            ProgressText = "";
            return;
        }

        var snapshot = _progressCalculator.BuildSnapshot(ToSettings(SelectedPreset), Count);
        if (snapshot.IsEmpty) {
            CurrentPhrase = LocalizationManager.Translate("Tasbih_Empty");
            ProgressText = "";
            return;
        }

        CurrentPhrase = TranslateValue(snapshot.CurrentText);
        ProgressText = $"{snapshot.LocalCount}/{snapshot.LocalTarget}";
    }

    private void SaveSelectedPreset() {
        if (SelectedPreset == null) {
            return;
        }

        var index = Presets.IndexOf(SelectedPreset);
        if (index < 0 || index == _settings.Tasbih.SelectedPresetIndex) {
            return;
        }

        _settings = new AppSettings {
            Location = _settings.Location,
            Method = _settings.Method,
            Madhhab = _settings.Madhhab,
            HighLatitudeRule = _settings.HighLatitudeRule,
            SunAngles = _settings.SunAngles,
            Offsets = _settings.Offsets,
            FastingOffsets = _settings.FastingOffsets,
            FastingReminders = _settings.FastingReminders,
            Notifications = _settings.Notifications,
            AlarmReminders = _settings.AlarmReminders,
            Qibla = _settings.Qibla,
            ClockFormat = _settings.ClockFormat,
            TextScale = _settings.TextScale,
            Tasbih = new TasbihSettings {
                Presets = _settings.Tasbih.Presets,
                SelectedPresetIndex = index
            },
            Language = _settings.Language,
            LanguageSelected = _settings.LanguageSelected,
            ThemeMode = _settings.ThemeMode,
            AccentIndex = _settings.AccentIndex,
            OnboardingCompleted = _settings.OnboardingCompleted
        };

        _suppressReload = true;
        _dataService.SaveSettings(_settings);
    }

    private void OnSettingsChanged(object? sender, EventArgs args) {
        if (_suppressReload) {
            _suppressReload = false;
            _settings = _dataService.LoadSettings();
            return;
        }

        RunOnMainThread(LoadPresets);
    }

    private void ApplyPresets(IReadOnlyList<TasbihPresetItem> presets, int selectedIndex) {
        RunOnMainThread(() => {
            _suspendSelectionSave = true;
            try {
                Presets.Clear();
                foreach (var preset in presets) {
                    Presets.Add(preset);
                }

                SelectedPreset = Presets.Count > 0 ? Presets[Math.Clamp(selectedIndex, 0, Presets.Count - 1)] : null;
                IsPresetSelectionEnabled = Count == 0;
            } finally {
                _suspendSelectionSave = false;
            }
        });
    }

    private static string TranslateValue(string value) {
        return TasbihTextResolver.Translate(value);
    }

    private static TasbihPresetSettings ToSettings(TasbihPresetItem preset) {
        return new TasbihPresetSettings {
            Name = preset.Name,
            RepeatMode = preset.RepeatMode,
            Items = preset.Items
                .Select(item => new TasbihItemSettings {
                    Text = item.Text,
                    TargetCount = item.TargetCount
                })
                .ToList()
        };
    }

    private static void RunOnMainThread(Action action) {
        if (MainThread.IsMainThread) {
            action();
        } else {
            MainThread.BeginInvokeOnMainThread(action);
        }
    }

    private static void TryVibrateReset() {
        try {
            Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(80));
        } catch (FeatureNotSupportedException) {
        } catch (Exception) {
        }
    }
}
