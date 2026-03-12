using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages;

public partial class SettingsAdhanPage : ContentPage {
    private bool _animated;

    public SettingsAdhanPage() : this(ServiceHelper.GetService<SettingsViewModel>()) {
    }

    public SettingsAdhanPage(SettingsViewModel viewModel) {
        WindowsStartupSafety.Trace("SettingsAdhanPage.Ctor:start");
        InitializeComponent();
        BindingContext = viewModel;
        WindowsStartupSafety.Trace("SettingsAdhanPage.Ctor:end");
    }

    protected override async void OnAppearing() {
        base.OnAppearing();
        WindowsStartupSafety.Trace("SettingsAdhanPage.OnAppearing:start");

        if (_animated) {
            WindowsStartupSafety.Trace("SettingsAdhanPage.OnAppearing:alreadyAnimated");
            return;
        }

        if (OperatingSystem.IsWindows()) {
            _animated = true;
            WindowsStartupSafety.Trace("SettingsAdhanPage.OnAppearing:skipAnimation_windows");
            return;
        }

        try {
            _animated = true;
            MainCard.Opacity = 0;
            MainCard.TranslationY = 20;
            await Task.WhenAll(
                MainCard.FadeToAsync(1, 280, Easing.CubicOut),
                MainCard.TranslateToAsync(0, 0, 280, Easing.CubicOut));

            var views = MainContentStack.Children.OfType<View>().ToList();
            for (var i = 0; i < views.Count; i++) {
                var view = views[i];
                view.Opacity = 0;
                view.TranslationY = 16;
            }

            for (var i = 0; i < views.Count; i++) {
                var view = views[i];
                await Task.Delay(28);
                _ = Task.WhenAll(
                    view.FadeToAsync(1, 220, Easing.CubicOut),
                    view.TranslateToAsync(0, 0, 220, Easing.CubicOut));
            }

            // Subtle pulse to emphasize the active sound segment.
            await SoundCollection.ScaleToAsync(1.012, 220, Easing.CubicInOut);
            await SoundCollection.ScaleToAsync(1, 220, Easing.CubicInOut);
            WindowsStartupSafety.Trace("SettingsAdhanPage.OnAppearing:animationDone");
        } catch (Exception ex) {
            WindowsStartupSafety.Trace($"SettingsAdhanPage.OnAppearing:exception {ex.GetType().Name}:{ex.Message}");
        }
    }
}
