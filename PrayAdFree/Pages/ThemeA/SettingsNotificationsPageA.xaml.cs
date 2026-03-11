using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages.ThemeA;

public partial class SettingsNotificationsPageA : ContentPage {
    public SettingsNotificationsPageA() : this(ServiceHelper.GetService<SettingsViewModel>()) {
    }

    public SettingsNotificationsPageA(SettingsViewModel viewModel) {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
