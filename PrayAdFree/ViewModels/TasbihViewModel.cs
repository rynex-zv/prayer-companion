using System.Collections.ObjectModel;
using PrayAdFree.Core.Models;
using Pray_Ad_Free.Models;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.ViewModels;

public sealed class TasbihViewModel : ViewModelBase {
    private readonly PrayerDataService _dataService;
    private AppSettings _settings = new();
    private int _count;
    private string _currentPhrase = "";
    private string _progressText = "";
    private TasbihPresetItem? _selectedPreset;

    public TasbihViewModel(PrayerDataService dataService) {
        _dataService = dataService;
        Presets = new ObservableCollection<TasbihPresetItem>();
        PresetItems = new ObservableCollection<TasbihPresetItemEntry>();
        IncrementCommand = new Command(Increment);
        ResetCommand = new Command(Reset);
        LoadPresets();
        LocalizationManager.LanguageChanged += (_, _) => LoadPresets();
        _dataService.SettingsChanged += (_, _) => LoadPresets();
    }

    public ObservableCollection<TasbihPresetItem> Presets { get; }
    public ObservableCollection<TasbihPresetItemEntry> PresetItems { get; }
    public Command IncrementCommand { get; }
    public Command ResetCommand { get; }

    public int Count {
        get => _count;
        set => SetProperty(ref _count, value);
    }

    public string CurrentPhrase {
        get => _currentPhrase;
        set => SetProperty(ref _currentPhrase, value);
    }

    public string ProgressText {
        get => _progressText;
        set => SetProperty(ref _progressText, value);
    }

    public TasbihPresetItem? SelectedPreset {
        get => _selectedPreset;
        set {
            if (SetProperty(ref _selectedPreset, value)) {
                Count = 0;
                BuildPresetItems();
                UpdateCurrentPhrase();
                SaveSelectedPreset();
            }
        }
    }

    private void Increment() {
        if (SelectedPreset == null) {
            return;
        }

        var totalTarget = SelectedPreset.TotalTarget;
        if (totalTarget == 0) {
            return;
        }

        if (SelectedPreset.RepeatMode == TasbihRepeatMode.None && Count >= totalTarget) {
            return;
        }

        Count++;
        if (SelectedPreset.RepeatMode == TasbihRepeatMode.RepeatReset && Count >= totalTarget) {
            Count = 0;
        }

        UpdateCurrentPhrase();
    }

    private void Reset() {
        Count = 0;
        UpdateCurrentPhrase();
    }

    private void LoadPresets() {
        _settings = _dataService.LoadSettings();
        if (_settings.Tasbih.Presets.Count == 0) {
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

        Presets.Clear();
        foreach (var preset in _settings.Tasbih.Presets) {
            Presets.Add(new TasbihPresetItem(preset.Name, preset.RepeatMode, preset.Items));
        }

        var index = Math.Clamp(_settings.Tasbih.SelectedPresetIndex, 0, Math.Max(0, Presets.Count - 1));
        SelectedPreset = Presets.Count > 0 ? Presets[index] : null;
    }

    private void BuildPresetItems() {
        PresetItems.Clear();
        if (SelectedPreset == null) {
            return;
        }

        foreach (var item in SelectedPreset.Items) {
            PresetItems.Add(new TasbihPresetItemEntry(item.Text, item.TargetCount));
        }
    }

    private void UpdateCurrentPhrase() {
        if (SelectedPreset == null || SelectedPreset.Items.Count == 0) {
            CurrentPhrase = LocalizationManager.Translate("Tasbih_Empty");
            ProgressText = "";
            return;
        }

        var totalTarget = SelectedPreset.TotalTarget;
        if (totalTarget == 0) {
            CurrentPhrase = LocalizationManager.Translate("Tasbih_Empty");
            ProgressText = "";
            return;
        }

        var position = SelectedPreset.RepeatMode == TasbihRepeatMode.RepeatContinue
            ? Count % totalTarget
            : Math.Min(Count, totalTarget);
        var running = 0;
        foreach (var item in SelectedPreset.Items) {
            if (item.TargetCount <= 0) {
                continue;
            }
            var next = running + item.TargetCount;
            if (position < next) {
                CurrentPhrase = item.Text;
                var localCount = position - running;
                ProgressText = $"{localCount}/{item.TargetCount}";
                return;
            }
            running = next;
        }

        var last = SelectedPreset.Items.Last();
        CurrentPhrase = last.Text;
        ProgressText = $"{last.TargetCount}/{last.TargetCount}";
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
            Offsets = _settings.Offsets,
            FastingOffsets = _settings.FastingOffsets,
            FastingReminders = _settings.FastingReminders,
            Notifications = _settings.Notifications,
            ClockFormat = _settings.ClockFormat,
            TextScale = _settings.TextScale,
            Tasbih = new TasbihSettings {
                Presets = _settings.Tasbih.Presets,
                SelectedPresetIndex = index
            },
            Language = _settings.Language,
            LanguageSelected = _settings.LanguageSelected,
            ThemeMode = _settings.ThemeMode,
            ThemeVariant = _settings.ThemeVariant,
            AccentIndex = _settings.AccentIndex
        };

        _dataService.SaveSettings(_settings);
    }
}
