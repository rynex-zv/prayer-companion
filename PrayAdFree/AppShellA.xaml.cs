using Microsoft.Maui.ApplicationModel;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Pages;
using Pray_Ad_Free.Pages.ThemeA;
using Pray_Ad_Free.Services;
using System.Linq;

namespace Pray_Ad_Free;

public partial class AppShellA : Shell {
    private readonly IAppLogger _logger;
    private static bool _routesRegistered;

    public AppShellA(SettingsService settingsService, IAppLogger logger) {
        var settings = settingsService.Load();
        _logger = logger;
        var preferredLanguage = settings.LanguageSelected ? settings.Language : "auto";

        _logger.LogEvent("AppShellACtor", $"start:lang={preferredLanguage}");
        ThemeManager.ApplyTheme(settings);
        _logger.LogEvent("AppShellACtor", "beforeInitializeComponent");
        InitializeComponent();
        _logger.LogEvent("AppShellACtor", "afterInitializeComponent");
        MainThread.BeginInvokeOnMainThread(ThemeManager.RefreshTextScaleOnVisibleUIWithDeferredPasses);

        var tabTitles = Items
            .SelectMany(item => item.Items)
            .SelectMany(section => section.Items)
            .Select(content => content.Title ?? content.Route ?? "(untitled)")
            .ToList();
        _logger.LogEvent("AppShellATabs", $"count={tabTitles.Count};tabs={string.Join(",", tabTitles)}");

        RegisterRoutes();
        _ = InitializeLocalizationAsync(preferredLanguage);

        Navigated += (_, _) => {
            ThemeManager.RefreshTextScaleOnVisibleUIWithDeferredPasses();
            _logger.LogEvent("ShellANavigated", Shell.Current?.CurrentState?.Location.ToString() ?? "Unknown");
        };

        _logger.LogEvent("AppShellACtor", "end");
    }

    private void RegisterRoutes() {
        if (_routesRegistered) {
            return;
        }

        Routing.RegisterRoute("settingsA/locations", typeof(SettingsLocationsPageA));
        Routing.RegisterRoute("settingsA/diagnostics", typeof(SettingsDiagnosticsPage));
        Routing.RegisterRoute("settingsA/adhan", typeof(SettingsAdhanPageA));
        Routing.RegisterRoute("settingsA/notifications", typeof(SettingsNotificationsPageA));
        Routing.RegisterRoute("settingsA/tasbih", typeof(SettingsTasbihPage));
        Routing.RegisterRoute("settingsA/about", typeof(AboutPageA));
        _routesRegistered = true;
    }

    private async Task InitializeLocalizationAsync(string preferredLanguage) {
        try {
            _logger.LogEvent("AppShellALocalization", "syncStart");
            await Task.Run(() => new LocalizationFileSync().SyncIfNeeded()).ConfigureAwait(false);
            _logger.LogEvent("AppShellALocalization", "syncDone");
            await MainThread.InvokeOnMainThreadAsync(
                () => LocalizationManager.InitializeAsync(preferredLanguage)
            ).ConfigureAwait(false);
            _logger.LogEvent("AppShellALocalization", "initDone");
        } catch (Exception ex) {
            _logger.LogException(ex, "AppShellA.InitializeLocalizationAsync");
        }
    }
}
