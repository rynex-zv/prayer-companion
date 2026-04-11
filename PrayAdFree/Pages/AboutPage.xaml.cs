using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages;

public partial class AboutPage : ContentPage {
    private const string SupportEmail = "rynex@rynex.nl";
    private const string SupportPhone = "+31610331734";
    private const string WebsiteUrl = "https://rynex.nl/cv";

    public AboutPage() : this( ServiceHelper.GetService<AboutViewModel>() ) {
    }

    public AboutPage(AboutViewModel viewModel) {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnReportClicked(object? sender, EventArgs e) {
        var message = new EmailMessage {
            Subject = LocalizationManager.Translate("IssueMailSubject"),
            Body = LocalizationManager.Translate("IssueMailBodyTemplate"),
            To = new List<string> { SupportEmail }
        };
        await Email.Default.ComposeAsync(message);
    }

    private async void OnEmailClicked(object? sender, EventArgs e) {
        var message = new EmailMessage {
            To = new List<string> { SupportEmail }
        };
        await Email.Default.ComposeAsync(message);
    }

    private void OnPhoneClicked(object? sender, EventArgs e) {
        PhoneDialer.Default.Open(SupportPhone);
    }

    private async void OnWebsiteClicked(object? sender, EventArgs e) {
        await Browser.Default.OpenAsync(WebsiteUrl, BrowserLaunchMode.External);
    }
}
