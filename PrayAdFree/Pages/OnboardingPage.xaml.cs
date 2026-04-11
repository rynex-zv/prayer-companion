using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages;

public partial class OnboardingPage : ContentPage {
    private readonly OnboardingViewModel _viewModel;
    private bool _initialized;

    public OnboardingPage() : this(ServiceHelper.GetService<OnboardingViewModel>()) {
    }

    public OnboardingPage(OnboardingViewModel viewModel) {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing() {
        base.OnAppearing();
        if (_initialized) {
            return;
        }

        _initialized = true;
        await _viewModel.InitializeAsync().ConfigureAwait(false);
        await MainThread.InvokeOnMainThreadAsync(ThemeManager.RefreshTextScaleOnVisibleUIWithDeferredPasses);
    }
}
