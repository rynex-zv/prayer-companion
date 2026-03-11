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
        await Shell.Current.GoToAsync("///settingsA/locations");
    }

    private async void OnDiagnosticsClicked(object? sender, EventArgs e) {
        Logger.LogEvent("SettingsGroupClick", "Diagnostics");
        await Shell.Current.GoToAsync("///settingsA/diagnostics");
    }

    private async void OnAdhanClicked(object? sender, EventArgs e) {
        Logger.LogEvent("SettingsGroupClick", "Adhan");
        await Shell.Current.GoToAsync("///settingsA/adhan");
    }

    private async void OnNotificationsClicked(object? sender, EventArgs e) {
        Logger.LogEvent("SettingsGroupClick", "Notifications");
        await Shell.Current.GoToAsync("///settingsA/notifications");
    }

    private async void OnTasbihClicked(object? sender, EventArgs e) {
        Logger.LogEvent("SettingsGroupClick", "Tasbih");
        await Shell.Current.GoToAsync("///settingsA/tasbih");
    }

    private async void OnAboutClicked(object? sender, EventArgs e) {
        Logger.LogEvent("SettingsGroupClick", "About");
        await Shell.Current.GoToAsync("///settingsA/about");
    }
}
