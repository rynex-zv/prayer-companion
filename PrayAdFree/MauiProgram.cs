using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using System;
using PrayAdFree.Core.Services;
using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;
using Plugin.LocalNotification.WindowsOption;
using Pray_Ad_Free.Pages;
using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free {
    public static class MauiProgram {
        public static MauiApp CreateMauiApp() {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseLocalNotification(options => {
                    var stopAction = new NotificationAction(AdhanPlaybackService.StopActionId) {
                        Title = ResolveStopActionTitle(),
                        Android = new AndroidAction {
                            LaunchAppWhenTapped = false
                        },
                        Windows = new WindowsAction {
                            LaunchAppWhenTapped = false,
                            DismissWhenTapped = true
                        }
                    };
                    options.AddCategory(new NotificationCategory(NotificationCategoryType.Service) {
                        ActionList = new HashSet<NotificationAction> { stopAction }
                    });
                })
                .ConfigureFonts( fonts => {
                    fonts.AddFont( "OpenSans-Regular.ttf" , "OpenSansRegular" );
                    fonts.AddFont( "OpenSans-Semibold.ttf" , "OpenSansSemibold" );
                } );
#if !WINDOWS
            builder.UseMauiMaps();
#endif
#if DEBUG
            builder.Logging.AddDebug();
#endif

            builder.Services.AddSingleton<ISettingsStore>( _ => new FileSettingsStore(
                Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ),
                    "PrayAdFree",
                    "app_settings.json" ) ) );
            builder.Services.AddSingleton<SettingsService>();
            builder.Services.AddHttpClient<PhotonGeoProvider>( client => {
                client.BaseAddress = new Uri( "https://photon.komoot.io/" );
            } );
            builder.Services.AddHttpClient<NominatimGeoProvider>( client => {
                client.BaseAddress = new Uri( "https://nominatim.openstreetmap.org/" );
                client.DefaultRequestHeaders.UserAgent.ParseAdd( "PrayAdFree/1.0 (contact: support@example.com)" );
            } );
            builder.Services.AddSingleton( sp => new GeoService(
                new IGeoProvider[] {
                    sp.GetRequiredService<PhotonGeoProvider>(),
                    sp.GetRequiredService<NominatimGeoProvider>()
                },
                Path.Combine( FileSystem.AppDataDirectory, "geo_cache.json" )
            ) );
            builder.Services.AddSingleton<ILocationProvider, LocationProvider>();
            builder.Services.AddSingleton<IWindowsBackgroundModeService, WindowsBackgroundModeService>();
            builder.Services.AddSingleton<PrayerSchedulePlanner>();
            builder.Services.AddSingleton<ILocalNotificationScheduler, LocalNotificationScheduler>();
            builder.Services.AddSingleton<IAdhanPlaybackService, AdhanPlaybackService>();
            builder.Services.AddSingleton<IAppLogger, AppLogger>();
            builder.Services.AddSingleton( _ => new PrayerTimesCache( FileSystem.AppDataDirectory ) );
            builder.Services.AddHttpClient<IPrayerTimesClient, AladhanPrayerTimesClient>();
            builder.Services.AddSingleton<PrayerTimesService>();
            builder.Services.AddSingleton<PrayerDataService>();

            builder.Services.AddTransient<HomeViewModel>();
            builder.Services.AddTransient<CalendarViewModel>();
            builder.Services.AddTransient<QiblaViewModel>();
            builder.Services.AddSingleton<SettingsViewModel>();
            builder.Services.AddTransient<AboutViewModel>();
            builder.Services.AddTransient<TasbihViewModel>();
            builder.Services.AddTransient<LanguageSelectionViewModel>();

            builder.Services.AddTransient<HomePage>();
            builder.Services.AddTransient<CalendarPage>();
            builder.Services.AddTransient<QiblaPage>();
            builder.Services.AddTransient<TasbihPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<SettingsLocationsPage>();
            builder.Services.AddTransient<SettingsDiagnosticsPage>();
            builder.Services.AddTransient<SettingsAdhanPage>();
            builder.Services.AddTransient<SettingsNotificationsPage>();
            builder.Services.AddTransient<SettingsTasbihPage>();
            builder.Services.AddTransient<AboutPage>();
            builder.Services.AddTransient<LanguageSelectionPage>();
            builder.Services.AddTransient<AppShell>();

            return builder.Build();
        }

        private static string ResolveStopActionTitle() {
            return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch {
                "ar" => "إيقاف",
                "fr" => "Arreter",
                "es" => "Detener",
                "tr" => "Durdur",
                _ => "Stop"
            };
        }
    }
}
