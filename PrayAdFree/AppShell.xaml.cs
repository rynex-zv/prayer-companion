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

            _ = InitializeLocalizationAsync(preferredLanguage);
            RegisterRoutes();
            Navigated += (_, _) => {
                ThemeManager.RefreshTextScaleOnVisibleUIWithDeferredPasses();
                var route = Shell.Current?.CurrentState?.Location.ToString() ?? "Unknown";
                _logger.LogEvent("ShellNavigated", route);
                WindowsStartupSafety.Trace($"Shell.Navigated:{route}");
            };
            _logger.LogEvent("AppShellCtor", "end");
            WindowsStartupSafety.Trace("Shell.Ctor:end");
        }

        private static void RegisterRoutes() {
            if (_routesRegistered) {
                return;
            }

            Routing.RegisterRoute("settings/locations", typeof(Pages.SettingsLocationsPage));
            Routing.RegisterRoute("settings/diagnostics", typeof(Pages.SettingsDiagnosticsPage));
            Routing.RegisterRoute("settings/adhan", typeof(Pages.SettingsAdhanPage));
            Routing.RegisterRoute("settings/notifications", typeof(Pages.SettingsNotificationsPage));
            Routing.RegisterRoute("settings/permissions", typeof(Pages.SettingsPermissionsPage));
            Routing.RegisterRoute("settings/alarm-reminders", typeof(Pages.SettingsAlarmRemindersPage));
            Routing.RegisterRoute("settings/tasbih", typeof(Pages.SettingsTasbihPage));
            Routing.RegisterRoute("settings/about", typeof(Pages.AboutPage));
            _routesRegistered = true;
        }

        private async Task InitializeLocalizationAsync(string preferredLanguage) {
            try {
                _logger.LogEvent("AppShellLocalization", "syncStart");
                WindowsStartupSafety.Trace("Shell.Localization:syncStart");
                await Task.Run(() => new LocalizationFileSync().SyncIfNeeded()).ConfigureAwait(false);
                _logger.LogEvent("AppShellLocalization", "syncDone");
                WindowsStartupSafety.Trace("Shell.Localization:syncDone");
                await MainThread.InvokeOnMainThreadAsync(
                    () => LocalizationManager.InitializeAsync(preferredLanguage)
                ).ConfigureAwait(false);
                _logger.LogEvent("AppShellLocalization", "initDone");
                WindowsStartupSafety.Trace("Shell.Localization:initDone");
            } catch (Exception ex) {
                _logger.LogException(ex, "AppShell.InitializeLocalizationAsync");
                WindowsStartupSafety.Trace($"Shell.Localization:exception {ex.GetType().Name}:{ex.Message}");
            }
        }
    }
}
