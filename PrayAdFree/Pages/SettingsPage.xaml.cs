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
}
