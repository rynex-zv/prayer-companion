using Microsoft.Maui.ApplicationModel;
using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages;

public partial class AboutPage : ContentPage {
    public AboutPage() : this( ServiceHelper.GetService<AboutViewModel>() ) {
    }

    public AboutPage(AboutViewModel viewModel) {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnReportClicked(object? sender, EventArgs e) {
        var body = Uri.EscapeDataString("Issue details:\nCity:\nCountry:\nMethod:\nMadhhab:\nOffsets:");
        var uri = new Uri($"mailto:support@example.com?subject=Pray%20Ad%20Free%20Issue&body={body}");
        await Launcher.Default.OpenAsync(uri);
    }
}
