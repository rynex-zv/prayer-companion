using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages.ThemeA;

public partial class SettingsLocationsPageA : ContentPage {
    public SettingsLocationsPageA() : this(ServiceHelper.GetService<SettingsViewModel>()) {
    }

    public SettingsLocationsPageA(SettingsViewModel viewModel) {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
