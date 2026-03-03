using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages;

public partial class SettingsDiagnosticsPage : ContentPage {
    public SettingsDiagnosticsPage() : this(ServiceHelper.GetService<SettingsViewModel>()) {
    }

    public SettingsDiagnosticsPage(SettingsViewModel viewModel) {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
