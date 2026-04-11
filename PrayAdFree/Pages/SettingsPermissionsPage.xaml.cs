using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages;

public partial class SettingsPermissionsPage : ContentPage {
    private readonly AppPermissionsViewModel _viewModel;

    public SettingsPermissionsPage() : this(ServiceHelper.GetService<AppPermissionsViewModel>()) {
    }

    public SettingsPermissionsPage(AppPermissionsViewModel viewModel) {
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
