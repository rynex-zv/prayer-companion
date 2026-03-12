using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages;

public partial class HomePage : ContentPage {
    private HomeViewModel ViewModel => (HomeViewModel)BindingContext;
    private bool _timerStarted;
    private bool _animated;

    public HomePage() : this( ServiceHelper.GetService<HomeViewModel>() ) {
    }

    public HomePage(HomeViewModel viewModel) {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing() {
        base.OnAppearing();
        ThemeManager.RefreshTextScaleOnVisibleUIWithDeferredPasses();
        _ = RefreshAndScaleAsync();

        if (!_animated) {
            _animated = true;
            Opacity = 0;
            await this.FadeToAsync(1, 600, Easing.CubicOut);
        }

        if (_timerStarted) {
            return;
        }

        _timerStarted = true;
        Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () => {
            ViewModel.UpdateCountdown(DateTime.Now);
            return true;
        });
    }

    private async Task RefreshAndScaleAsync() {
        await ViewModel.RefreshAsync().ConfigureAwait(false);
        await MainThread.InvokeOnMainThreadAsync(ThemeManager.RefreshTextScaleOnVisibleUIWithDeferredPasses);
    }
}
