using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages.ThemeA;

public partial class TasbihPageA : ContentPage {
    public TasbihPageA() : this(ServiceHelper.GetService<TasbihViewModel>()) {
    }

    public TasbihPageA(TasbihViewModel viewModel) {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
