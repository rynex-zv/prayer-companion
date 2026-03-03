using System.Linq;
using Pray_Ad_Free.Services;
using Pray_Ad_Free;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages;

public partial class LanguageSelectionPage : ContentPage {
    private LanguageSelectionViewModel ViewModel => (LanguageSelectionViewModel)BindingContext;

    public LanguageSelectionPage() : this( ServiceHelper.GetService<LanguageSelectionViewModel>() ) {
    }

    public LanguageSelectionPage(LanguageSelectionViewModel viewModel) {
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
