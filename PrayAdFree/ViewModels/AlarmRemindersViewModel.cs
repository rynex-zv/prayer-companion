using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Models;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free.ViewModels;

public sealed class AlarmRemindersViewModel : ViewModelBase {
    private readonly SettingsService _settingsService;
    private readonly AlarmReminderCatalogService _catalogService;
    private readonly IAppLogger _logger;
    private string _newReminderText = string.Empty;

    public AlarmRemindersViewModel(
        SettingsService settingsService,
        AlarmReminderCatalogService catalogService,
        IAppLogger logger) {
        _settingsService = settingsService;
        _catalogService = catalogService;
        _logger = logger;
        BuiltInReminders = new ObservableCollection<AlarmReminderEditorItem>();
        UserReminders = new ObservableCollection<AlarmReminderEditorItem>();
        AddUserReminderCommand = new Command(AddUserReminder);
        ToggleBuiltInReminderCommand = new Command<AlarmReminderEditorItem>(ToggleBuiltInReminder);
        ToggleUserReminderCommand = new Command<AlarmReminderEditorItem>(ToggleUserReminder);
        RemoveUserReminderCommand = new Command<AlarmReminderEditorItem>(RemoveUserReminder);
        EditUserReminderCommand = new Command<AlarmReminderEditorItem>(async item => await EditUserReminderAsync(item));
        Load();

        LocalizationManager.LanguageChanged += (_, _) => Load();
    }

    public ObservableCollection<AlarmReminderEditorItem> BuiltInReminders { get; }
    public ObservableCollection<AlarmReminderEditorItem> UserReminders { get; }

    public Command AddUserReminderCommand { get; }
    public Command ToggleBuiltInReminderCommand { get; }
    public Command ToggleUserReminderCommand { get; }
    public Command RemoveUserReminderCommand { get; }
    public Command EditUserReminderCommand { get; }

    public string NewReminderText {
        get => _newReminderText;
        set => SetProperty(ref _newReminderText, value);
    }

    private void Load() {
        try {
            var settings = _settingsService.Load();
            var alarmSettings = settings.AlarmReminders ?? new AlarmRemindersSettings();
            var disabled = new HashSet<string>(alarmSettings.DisabledBuiltInIds ?? [], StringComparer.OrdinalIgnoreCase);
            var builtIn = _catalogService.LoadForCurrentLanguage();

            BuiltInReminders.Clear();
            foreach (var item in builtIn) {
                BuiltInReminders.Add(new AlarmReminderEditorItem(
                    item.Id,
                    item.Text,
                    !disabled.Contains(item.Id),
                    isBuiltIn: true));
            }

            UserReminders.Clear();
            foreach (var item in alarmSettings.UserItems ?? []) {
                if (string.IsNullOrWhiteSpace(item.Id)) {
                    continue;
                }

                UserReminders.Add(new AlarmReminderEditorItem(
                    item.Id,
                    item.Text,
                    item.IsEnabled,
                    isBuiltIn: false));
            }
        } catch (Exception ex) {
            _logger.LogException(ex, "AlarmRemindersViewModel.Load");
        }
    }

    private void AddUserReminder() {
        var text = NewReminderText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) {
            return;
        }

        UserReminders.Add(new AlarmReminderEditorItem(
            $"user_{Guid.NewGuid():N}",
            text,
            true,
            isBuiltIn: false));
        NewReminderText = string.Empty;
        Save();
    }

    private void ToggleBuiltInReminder(AlarmReminderEditorItem? item) {
        if (item == null) {
            return;
        }

        item.IsEnabled = !item.IsEnabled;
        Save();
    }

    private void ToggleUserReminder(AlarmReminderEditorItem? item) {
        if (item == null) {
            return;
        }

        item.IsEnabled = !item.IsEnabled;
        Save();
    }

    private void RemoveUserReminder(AlarmReminderEditorItem? item) {
        if (item == null) {
            return;
        }

        UserReminders.Remove(item);
        Save();
    }

    private async Task EditUserReminderAsync(AlarmReminderEditorItem? item) {
        if (item == null) {
            return;
        }

        var page = GetActivePage();
        if (page == null) {
            return;
        }

        try {
            var value = await MainThread.InvokeOnMainThreadAsync(async () =>
                await page.DisplayPromptAsync(
                    LocalizationManager.Translate("AlarmReminderEditTitle"),
                    LocalizationManager.Translate("AlarmReminderEditHint"),
                    accept: LocalizationManager.Translate("Save"),
                    cancel: LocalizationManager.Translate("Cancel"),
                    initialValue: item.Text)).ConfigureAwait(false);

            if (value == null) {
                return;
            }

            var text = value.Trim();
            if (string.IsNullOrWhiteSpace(text)) {
                return;
            }

            item.Text = text;
            Save();
        } catch (Exception ex) {
            _logger.LogException(ex, "AlarmRemindersViewModel.EditUserReminderAsync");
        }
    }

    private void Save() {
        try {
            var current = _settingsService.Load();
            var nextAlarmSettings = new AlarmRemindersSettings {
                DisabledBuiltInIds = BuiltInReminders
                    .Where(item => !item.IsEnabled)
                    .Select(item => item.Id)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                UserItems = UserReminders
                    .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                    .Select(item => new AlarmUserReminderItem {
                        Id = item.Id,
                        Text = item.Text?.Trim() ?? string.Empty,
                        IsEnabled = item.IsEnabled
                    })
                    .Where(item => !string.IsNullOrWhiteSpace(item.Text))
                    .ToList()
            };

            _settingsService.Save(CloneWithAlarmReminders(current, nextAlarmSettings));
        } catch (Exception ex) {
            _logger.LogException(ex, "AlarmRemindersViewModel.Save");
        }
    }

    private static AppSettings CloneWithAlarmReminders(AppSettings current, AlarmRemindersSettings reminders) {
        return new AppSettings {
            Location = current.Location,
            Method = current.Method,
            Madhhab = current.Madhhab,
            HighLatitudeRule = current.HighLatitudeRule,
            SunAngles = current.SunAngles,
            Offsets = current.Offsets,
            FastingOffsets = current.FastingOffsets,
            FastingReminders = current.FastingReminders,
            Notifications = current.Notifications,
            AlarmReminders = reminders,
            Qibla = current.Qibla,
            ClockFormat = current.ClockFormat,
            TextScale = current.TextScale,
            Tasbih = current.Tasbih,
            Language = current.Language,
            LanguageSelected = current.LanguageSelected,
            ThemeMode = current.ThemeMode,
            ThemeVariant = current.ThemeVariant,
            AccentIndex = current.AccentIndex,
            OnboardingCompleted = current.OnboardingCompleted
        };
    }

    private static Page? GetActivePage() {
        if (Shell.Current?.CurrentPage != null) {
            return Shell.Current.CurrentPage;
        }

        return Application.Current?.Windows.FirstOrDefault()?.Page;
    }
}
