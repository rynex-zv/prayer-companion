using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages;

public partial class SettingsAdhanPage : ContentPage {
    public SettingsAdhanPage() : this(ServiceHelper.GetService<SettingsViewModel>()) {
    }

    public SettingsAdhanPage(SettingsViewModel viewModel) {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
