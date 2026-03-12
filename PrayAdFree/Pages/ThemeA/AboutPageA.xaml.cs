using Microsoft.Maui.ApplicationModel;
using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Pages.ThemeA;

public partial class AboutPageA : ContentPage {
    public AboutPageA() : this( ServiceHelper.GetService<AboutViewModel>() ) {
    }

    public AboutPageA(AboutViewModel viewModel) {
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
