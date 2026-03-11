using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages.ThemeA;

public partial class CalendarPageA : ContentPage {
    private CalendarViewModel ViewModel => (CalendarViewModel)BindingContext;
    private bool _animated;

    public CalendarPageA() : this( ServiceHelper.GetService<CalendarViewModel>() ) {
    }

    public CalendarPageA(CalendarViewModel viewModel) {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing() {
        base.OnAppearing();
        _ = ViewModel.LoadAsync();

        if (!_animated) {
            _animated = true;
            TranslationY = 10;
            Opacity = 0;
            await Task.WhenAll(
                this.TranslateToAsync(0, 0, 400, Easing.CubicOut),
                this.FadeToAsync(1, 400, Easing.CubicOut)
            );
        }
    }
}
