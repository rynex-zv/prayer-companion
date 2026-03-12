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
        var subject = Uri.EscapeDataString(LocalizationManager.Translate("IssueMailSubject"));
        var body = Uri.EscapeDataString(LocalizationManager.Translate("IssueMailBodyTemplate"));
        var uri = new Uri($"mailto:support@example.com?subject={subject}&body={body}");
        await Launcher.Default.OpenAsync(uri);
    }
}
