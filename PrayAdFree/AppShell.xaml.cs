using Microsoft.Maui.ApplicationModel;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Services;
using System.Linq;

namespace Pray_Ad_Free {
    public partial class AppShell : Shell {
        private readonly IAppLogger _logger;
        private static bool _routesRegistered;

        public AppShell(SettingsService settingsService, IAppLogger logger) {
            var settings = settingsService.Load();
            _logger = logger;
            var preferredLanguage = settings.LanguageSelected ? settings.Language : "auto";
            _logger.LogEvent("AppShellCtor", $"start:lang={preferredLanguage}");
            WindowsStartupSafety.Trace($"Shell.Ctor:start lang={preferredLanguage}");
            ThemeManager.ApplyTheme(settings);

            _logger.LogEvent("AppShellCtor", "beforeInitializeComponent");
            InitializeComponent();
            _logger.LogEvent("AppShellCtor", "afterInitializeComponent");
            _logger.LogEvent("AppShellCtor", "afterApplyTheme");
            MainThread.BeginInvokeOnMainThread(ThemeManager.RefreshTextScaleOnVisibleUIWithDeferredPasses);

            var tabTitles = Items
                .SelectMany(item => item.Items)
                .SelectMany(section => section.Items)
                .Select(content => content.Title ?? content.Route ?? "(untitled)")
                .ToList();
            _logger.LogEvent("AppShellTabs", $"count={tabTitles.Count};tabs={string.Join(",", tabTitles)}");

            RegisterRoutes();
            Navigated += (_, _) => {
                ThemeManager.RefreshTextScaleOnVisibleUIWithDeferredPasses();
                var route = Shell.Current?.CurrentState?.Location.ToString() ?? "Unknown";
                _logger.LogEvent("ShellNavigated", route);
                WindowsStartupSafety.Trace($"Shell.Navigated:{route}");
                App.NotifyUiActivated("ShellNavigated");
            };
            _logger.LogEvent("AppShellCtor", "end");
            WindowsStartupSafety.Trace("Shell.Ctor:end");
        }

        private static void RegisterRoutes() {
            if (_routesRegistered) {
                return;
            }

            Routing.RegisterRoute("settings_locations_page", typeof(Pages.SettingsLocationsPage));
            Routing.RegisterRoute("settings_diagnostics_page", typeof(Pages.SettingsDiagnosticsPage));
            Routing.RegisterRoute("settings_adhan_page", typeof(Pages.SettingsAdhanPage));
            Routing.RegisterRoute("settings_notifications_page", typeof(Pages.SettingsNotificationsPage));
            Routing.RegisterRoute("settings_permissions_page", typeof(Pages.SettingsPermissionsPage));
            Routing.RegisterRoute("settings_alarm_reminders_page", typeof(Pages.SettingsAlarmRemindersPage));
            Routing.RegisterRoute("settings_tasbih_page", typeof(Pages.SettingsTasbihPage));
            Routing.RegisterRoute("settings_about_page", typeof(Pages.AboutPage));
            _routesRegistered = true;
        }

    }
}
