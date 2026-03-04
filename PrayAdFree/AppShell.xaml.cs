using PrayAdFree.Core.Services;
using Pray_Ad_Free.Pages;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free {
    public partial class AppShell : Shell {
        private readonly IAppLogger _logger;
        private bool _languagePrompted;

        public AppShell(SettingsService settingsService, IAppLogger logger) {
            var settings = settingsService.Load();
            _logger = logger;
            new LocalizationFileSync().SyncIfNeeded();
            LocalizationManager.InitializeAsync(settings.LanguageSelected ? settings.Language : "auto").GetAwaiter().GetResult();

            InitializeComponent();
            ThemeManager.ApplyTheme(settings);
            Routing.RegisterRoute("settings/locations", typeof(Pages.SettingsLocationsPage));
            Routing.RegisterRoute("settings/diagnostics", typeof(Pages.SettingsDiagnosticsPage));
            Routing.RegisterRoute("settings/adhan", typeof(Pages.SettingsAdhanPage));
            Routing.RegisterRoute("settings/notifications", typeof(Pages.SettingsNotificationsPage));
            Routing.RegisterRoute("settings/tasbih", typeof(Pages.SettingsTasbihPage));

            Navigated += async (_, _) => {
                _logger.LogEvent("ShellNavigated", Shell.Current?.CurrentState?.Location.ToString() ?? "Unknown");
                var current = settingsService.Load();
                if (_languagePrompted || current.LanguageSelected) {
                    return;
                }

                _languagePrompted = true;
                await Navigation.PushModalAsync(new LanguageSelectionPage());
            };
        }
    }
}
