using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages;

public partial class TasbihPage : ContentPage {
    public TasbihPage() : this(ServiceHelper.GetService<TasbihViewModel>()) {
    }

    public TasbihPage(TasbihViewModel viewModel) {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
