using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages.ThemeA;

public partial class SettingsAdhanPageA : ContentPage {
    public SettingsAdhanPageA() : this(ServiceHelper.GetService<SettingsViewModel>()) {
    }

    public SettingsAdhanPageA(SettingsViewModel viewModel) {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
