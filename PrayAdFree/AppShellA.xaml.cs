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
        WindowsStartupSafety.Trace($"ShellA.Ctor:start lang={preferredLanguage}");
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
        Navigated += (_, _) => {
            ThemeManager.RefreshTextScaleOnVisibleUIWithDeferredPasses();
            var route = Shell.Current?.CurrentState?.Location.ToString() ?? "Unknown";
            _logger.LogEvent("ShellANavigated", route);
            WindowsStartupSafety.Trace($"ShellA.Navigated:{route}");
            App.NotifyUiActivated("ShellANavigated");
        };

        _logger.LogEvent("AppShellACtor", "end");
        WindowsStartupSafety.Trace("ShellA.Ctor:end");
    }

    private void RegisterRoutes() {
        if (_routesRegistered) {
            return;
        }

        Routing.RegisterRoute("settingsA_locations_page", typeof(SettingsLocationsPageA));
        Routing.RegisterRoute("settingsA_diagnostics_page", typeof(SettingsDiagnosticsPage));
        Routing.RegisterRoute("settingsA_adhan_page", typeof(SettingsAdhanPageA));
        Routing.RegisterRoute("settingsA_notifications_page", typeof(SettingsNotificationsPageA));
        Routing.RegisterRoute("settingsA_permissions_page", typeof(SettingsPermissionsPageA));
        Routing.RegisterRoute("settingsA_alarm_reminders_page", typeof(SettingsAlarmRemindersPageA));
        Routing.RegisterRoute("settingsA_tasbih_page", typeof(SettingsTasbihPage));
        Routing.RegisterRoute("settingsA_about_page", typeof(AboutPageA));
        _routesRegistered = true;
    }

}
