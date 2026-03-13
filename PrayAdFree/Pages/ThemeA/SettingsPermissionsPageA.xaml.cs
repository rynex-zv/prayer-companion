using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages.ThemeA;

public partial class SettingsPermissionsPageA : ContentPage {
    private readonly AppPermissionsViewModel _viewModel;

    public SettingsPermissionsPageA() : this(ServiceHelper.GetService<AppPermissionsViewModel>()) {
    }

    public SettingsPermissionsPageA(AppPermissionsViewModel viewModel) {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override async void OnAppearing() {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}
