using System.Linq;
using Pray_Ad_Free.Services;
using Pray_Ad_Free;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages.ThemeA;

public partial class LanguageSelectionPageA : ContentPage {
    private LanguageSelectionViewModel ViewModel => (LanguageSelectionViewModel)BindingContext;

    public LanguageSelectionPageA() : this( ServiceHelper.GetService<LanguageSelectionViewModel>() ) {
    }

    public LanguageSelectionPageA(LanguageSelectionViewModel viewModel) {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnContinueClicked(object? sender, EventArgs e) {
        ViewModel.ConfirmSelection();
        if (Navigation.ModalStack.Count > 0) {
            await Navigation.PopModalAsync();
            return;
        }

        var window = Application.Current?.Windows.FirstOrDefault();
        if (window != null) {
            window.Page = ServiceHelper.GetService<AppShell>();
        }
    }
}
