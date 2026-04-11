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
        App.AppResumed += OnAppResumed;
        await _viewModel.RefreshAsync();
    }

    protected override void OnDisappearing() {
        App.AppResumed -= OnAppResumed;
        base.OnDisappearing();
    }

    private async void OnAppResumed(object? sender, EventArgs e) {
        await _viewModel.RefreshAsync();
    }
}
