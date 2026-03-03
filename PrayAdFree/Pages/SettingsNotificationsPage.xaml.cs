using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages;

public partial class SettingsNotificationsPage : ContentPage {
    public SettingsNotificationsPage() : this(ServiceHelper.GetService<SettingsViewModel>()) {
    }

    public SettingsNotificationsPage(SettingsViewModel viewModel) {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
