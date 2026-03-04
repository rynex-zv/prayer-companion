using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Models;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.ViewModels;

public sealed class LanguageSelectionViewModel : ViewModelBase {
    private readonly SettingsService _settingsService;
    private OptionItem<string>? _selectedLanguage;

    public LanguageSelectionViewModel(SettingsService settingsService) {
        _settingsService = settingsService;
        var languages = LocalizationManager.GetAvailableLanguages()
            .Select(option => new OptionItem<string>(option.Code, option.Name))
            .ToList();
        if (languages.Count == 0) {
            languages.Add(new OptionItem<string>("en", "English"));
            languages.Add(new OptionItem<string>("ar", "Arabic"));
            languages.Add(new OptionItem<string>("fr", "French"));
            languages.Add(new OptionItem<string>("tr", "Turkish"));
            languages.Add(new OptionItem<string>("es", "Spanish"));
        }
        Languages = new ObservableCollection<OptionItem<string>>(languages);

        var deviceLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        SelectedLanguage = Languages.FirstOrDefault(item => item.Value.Equals(deviceLanguage, StringComparison.OrdinalIgnoreCase))
            ?? Languages.FirstOrDefault(item => item.Value.Equals("en", StringComparison.OrdinalIgnoreCase))
            ?? Languages.FirstOrDefault();
    }

    public ObservableCollection<OptionItem<string>> Languages { get; }

    public OptionItem<string>? SelectedLanguage {
        get => _selectedLanguage;
        set => SetProperty(ref _selectedLanguage, value);
    }

    public void ConfirmSelection() {
        var settings = _settingsService.Load();
        var language = SelectedLanguage?.Value ?? "en";
        settings = new AppSettings {
            Location = settings.Location,
            Method = settings.Method,
            Madhhab = settings.Madhhab,
            HighLatitudeRule = settings.HighLatitudeRule,
            Offsets = settings.Offsets,
            FastingOffsets = settings.FastingOffsets,
            FastingReminders = settings.FastingReminders,
            Notifications = settings.Notifications,
            Qibla = settings.Qibla,
            ClockFormat = settings.ClockFormat,
            TextScale = settings.TextScale,
            Tasbih = settings.Tasbih,
            Language = language,
            LanguageSelected = true,
            ThemeMode = settings.ThemeMode,
            ThemeVariant = settings.ThemeVariant,
            AccentIndex = settings.AccentIndex
        };
        _settingsService.Save(settings);
        LocalizationManager.SetLanguage(language);
    }
}
