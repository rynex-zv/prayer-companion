using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages;

public partial class SettingsTasbihPage : ContentPage {
    public SettingsTasbihPage() : this(ServiceHelper.GetService<SettingsViewModel>()) {
    }

    public SettingsTasbihPage(SettingsViewModel viewModel) {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
