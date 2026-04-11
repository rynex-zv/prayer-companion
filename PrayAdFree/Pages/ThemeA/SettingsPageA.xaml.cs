using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages.ThemeA;

public partial class SettingsPageA : ContentPage {
    public SettingsPageA() : this( ServiceHelper.GetService<SettingsViewModel>(), ServiceHelper.GetService<IAppLogger>() ) {
    }

    public SettingsPageA(SettingsViewModel viewModel, IAppLogger logger) {
        InitializeComponent();
        BindingContext = viewModel;
        Logger = logger;
    }

    public IAppLogger Logger { get; }

    private async void OnLocationsClicked(object? sender, EventArgs e) {
        Logger.LogEvent("SettingsGroupClick", "Locations");
        await Shell.Current.GoToAsync("settingsA_locations_page");
    }

    private async void OnDiagnosticsClicked(object? sender, EventArgs e) {
        Logger.LogEvent("SettingsGroupClick", "Diagnostics");
        await Shell.Current.GoToAsync("settingsA_diagnostics_page");
    }

    private async void OnAdhanClicked(object? sender, EventArgs e) {
        Logger.LogEvent("SettingsGroupClick", "Adhan");
        WindowsStartupSafety.Trace("SettingsPageA.Click:Adhan:start");
        await Shell.Current.GoToAsync("settingsA_adhan_page");
        WindowsStartupSafety.Trace("SettingsPageA.Click:Adhan:end");
    }

    private async void OnNotificationsClicked(object? sender, EventArgs e) {
        Logger.LogEvent("SettingsGroupClick", "Notifications");
        await Shell.Current.GoToAsync("settingsA_notifications_page");
    }

    private async void OnPermissionsClicked(object? sender, EventArgs e) {
        Logger.LogEvent("SettingsGroupClick", "Permissions");
        await Shell.Current.GoToAsync("settingsA_permissions_page");
    }

    private async void OnAlarmRemindersClicked(object? sender, EventArgs e) {
        Logger.LogEvent("SettingsGroupClick", "AlarmReminders");
        await Shell.Current.GoToAsync("settingsA_alarm_reminders_page");
    }

    private async void OnTasbihClicked(object? sender, EventArgs e) {
        Logger.LogEvent("SettingsGroupClick", "Tasbih");
        await Shell.Current.GoToAsync("settingsA_tasbih_page");
    }

    private async void OnAboutClicked(object? sender, EventArgs e) {
        Logger.LogEvent("SettingsGroupClick", "About");
        await Shell.Current.GoToAsync("settingsA_about_page");
    }
}
