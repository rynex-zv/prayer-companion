using PrayAdFree.Core.Services;
using Pray_Ad_Free.Pages;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free {
    public partial class AppShell : Shell {
        private bool _languagePrompted;

        public AppShell(SettingsService settingsService) {
            var settings = settingsService.Load();
            new LocalizationFileSync().SyncIfNeeded();
            LocalizationManager.InitializeAsync(settings.LanguageSelected ? settings.Language : "auto").GetAwaiter().GetResult();

            InitializeComponent();
            ThemeManager.ApplyTheme(settings);

            Navigated += async (_, _) => {
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
