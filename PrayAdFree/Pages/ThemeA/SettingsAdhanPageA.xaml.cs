using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages.ThemeA;

public partial class SettingsAdhanPageA : ContentPage {
    public SettingsAdhanPageA() : this(ServiceHelper.GetService<SettingsViewModel>()) {
    }

    public SettingsAdhanPageA(SettingsViewModel viewModel) {
        WindowsStartupSafety.Trace("SettingsAdhanPageA.Ctor:start");
        InitializeComponent();
        BindingContext = viewModel;
        WindowsStartupSafety.Trace("SettingsAdhanPageA.Ctor:end");
    }

    protected override void OnAppearing() {
        base.OnAppearing();
        WindowsStartupSafety.Trace("SettingsAdhanPageA.OnAppearing");
    }
}
