using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using System;
using PrayAdFree.Core.Services;
using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;
using Plugin.LocalNotification.iOSOption;
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
                        IOS = new iOSAction {
                            Action = iOSActionType.None
                        },
                        Windows = new WindowsAction {
                            LaunchAppWhenTapped = false,
                            DismissWhenTapped = true
                        }
                    };
                    var snooze10Action = new NotificationAction(AdhanPlaybackService.Snooze10ActionId) {
                        Title = ResolveSnooze10ActionTitle(),
                        Android = new AndroidAction {
                            LaunchAppWhenTapped = false
                        },
                        IOS = new iOSAction {
                            Action = iOSActionType.None
                        },
                        Windows = new WindowsAction {
                            LaunchAppWhenTapped = false,
                            DismissWhenTapped = true
                        }
                    };
                    var customSnoozeAction = new NotificationAction(AdhanPlaybackService.OpenCustomSnoozeActionId) {
                        Title = ResolveCustomSnoozeActionTitle(),
                        Android = new AndroidAction {
                            LaunchAppWhenTapped = true
                        },
                        IOS = new iOSAction {
                            Action = iOSActionType.Foreground
                        },
                        Windows = new WindowsAction {
                            LaunchAppWhenTapped = true,
                            DismissWhenTapped = true
                        }
                    };

                    options.AddCategory(new NotificationCategory(NotificationCategoryType.Service) {
                        ActionList = new HashSet<NotificationAction> { stopAction }
                    });
                    options.AddCategory(new NotificationCategory(NotificationCategoryType.Alarm) {
                        ActionList = new HashSet<NotificationAction> { customSnoozeAction, stopAction }
                    });
                    options.AddCategory(new NotificationCategory(NotificationCategoryType.Reminder) {
                        ActionList = new HashSet<NotificationAction> { snooze10Action, customSnoozeAction, stopAction }
                    });
                })
                .ConfigureFonts(fonts => {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });
#if !WINDOWS
            builder.UseMauiMaps();
#endif
#if DEBUG
            builder.Logging.AddDebug();
#endif

            builder.Services.AddSingleton<ISettingsStore>(_ => new FileSettingsStore(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PrayAdFree",
                    "app_settings.json")));
            builder.Services.AddSingleton<SettingsService>();
            builder.Services.AddHttpClient<PhotonGeoProvider>(client => {
                client.BaseAddress = new Uri("https://photon.komoot.io/");
            });
            builder.Services.AddHttpClient<NominatimGeoProvider>(client => {
                client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("PrayAdFree/1.0 (contact: support@example.com)");
            });
            builder.Services.AddSingleton(sp => new GeoService(
                new IGeoProvider[] {
                    sp.GetRequiredService<PhotonGeoProvider>(),
                    sp.GetRequiredService<NominatimGeoProvider>()
                },
                Path.Combine(FileSystem.AppDataDirectory, "geo_cache.json")
            ));
            builder.Services.AddSingleton<ILocationProvider, LocationProvider>();
            builder.Services.AddSingleton<IWindowsBackgroundModeService, WindowsBackgroundModeService>();
            builder.Services.AddSingleton<IAppLogger, AppLogger>();
            builder.Services.AddSingleton<AdhanPlaybackService>();
            builder.Services.AddSingleton<IAdhanPlaybackService>(sp => sp.GetRequiredService<AdhanPlaybackService>());
#if WINDOWS
            builder.Services.AddSingleton<IWindowsNotificationQueueService, WindowsNotificationQueueService>();
#else
            builder.Services.AddSingleton<IWindowsNotificationQueueService, NullWindowsNotificationQueueService>();
#endif
            builder.Services.AddSingleton<PrayerSchedulePlanner>();
            builder.Services.AddSingleton<ILocalNotificationScheduler, LocalNotificationScheduler>();
            builder.Services.AddSingleton(_ => new PrayerTimesCache(FileSystem.AppDataDirectory));
            builder.Services.AddHttpClient<IPrayerTimesClient, AladhanPrayerTimesClient>();
            builder.Services.AddSingleton<PrayerTimesService>();
            builder.Services.AddSingleton<PrayerDataService>();
            builder.Services.AddSingleton<NotificationBootstrapper>();

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
                "ar" => "\u0625\u064A\u0642\u0627\u0641",
                "fr" => "Arreter",
                "es" => "Detener",
                "tr" => "Durdur",
                _ => "Stop"
            };
        }

        private static string ResolveSnooze10ActionTitle() {
            return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch {
                "ar" => "\u0628\u0639\u062f 10\u062f",
                "fr" => "Dans 10 min",
                "es" => "En 10 min",
                "tr" => "10 dk sonra",
                _ => "After 10m"
            };
        }

        private static string ResolveCustomSnoozeActionTitle() {
            return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch {
                "ar" => "\u0627\u0644\u062A\u0630\u0643\u064A\u0631 \u0628\u0639\u062F",
                "fr" => "Rappeler plus tard",
                "es" => "Recordarme despues",
                "tr" => "Daha sonra hatirlat",
                _ => "Remind me after"
            };
        }
    }
}
