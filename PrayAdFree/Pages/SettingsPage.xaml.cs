using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages;

public partial class SettingsPage : ContentPage {
    public SettingsPage() : this( ServiceHelper.GetService<SettingsViewModel>() ) {
    }

    public SettingsPage(SettingsViewModel viewModel) {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnLocationsClicked(object? sender, EventArgs e) {
        await Shell.Current.GoToAsync("settings/locations");
    }

    private async void OnDiagnosticsClicked(object? sender, EventArgs e) {
        await Shell.Current.GoToAsync("settings/diagnostics");
    }

    private async void OnAdhanClicked(object? sender, EventArgs e) {
        await Shell.Current.GoToAsync("settings/adhan");
    }

    private async void OnNotificationsClicked(object? sender, EventArgs e) {
        await Shell.Current.GoToAsync("settings/notifications");
    }
}
