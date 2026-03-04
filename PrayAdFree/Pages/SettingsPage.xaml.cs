using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages;

public partial class SettingsPage : ContentPage {
    public SettingsPage() : this( ServiceHelper.GetService<SettingsViewModel>(), ServiceHelper.GetService<IAppLogger>() ) {
    }

    public SettingsPage(SettingsViewModel viewModel, IAppLogger logger) {
        InitializeComponent();
        BindingContext = viewModel;
        Logger = logger;
    }

    public IAppLogger Logger { get; }

    private async void OnLocationsClicked(object? sender, EventArgs e) {
        Logger.LogEvent("SettingsGroupClick", "Locations");
        await Shell.Current.GoToAsync("settings/locations");
    }

    private async void OnDiagnosticsClicked(object? sender, EventArgs e) {
        Logger.LogEvent("SettingsGroupClick", "Diagnostics");
        await Shell.Current.GoToAsync("settings/diagnostics");
    }

    private async void OnAdhanClicked(object? sender, EventArgs e) {
        Logger.LogEvent("SettingsGroupClick", "Adhan");
        await Shell.Current.GoToAsync("settings/adhan");
    }

    private async void OnNotificationsClicked(object? sender, EventArgs e) {
        Logger.LogEvent("SettingsGroupClick", "Notifications");
        await Shell.Current.GoToAsync("settings/notifications");
    }

    private async void OnTasbihClicked(object? sender, EventArgs e) {
        Logger.LogEvent("SettingsGroupClick", "Tasbih");
        await Shell.Current.GoToAsync("settings/tasbih");
    }
}
