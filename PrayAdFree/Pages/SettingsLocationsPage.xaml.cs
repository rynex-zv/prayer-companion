using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages;

public partial class SettingsLocationsPage : ContentPage {
    public SettingsLocationsPage() : this(ServiceHelper.GetService<SettingsViewModel>()) {
    }

    public SettingsLocationsPage(SettingsViewModel viewModel) {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
