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
using MauiWebber;

namespace Pray_Ad_Free {
    public static class MauiProgram {
        public static MauiApp CreateMauiApp() {
            LocalizationBootstrapper.EnsureInitialized();

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureMauiHandlers(handlers => {
#if ANDROID
                    Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("PrayAdFree.EntryTint", static (handler, _) => {
                        if (handler.PlatformView is null) {
                            return;
                        }

                        handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(
                            ResolveAndroidThemeColor("InputTint", "InputTintDark", "#2FB79D"));
                        ApplyAndroidInputSurface(handler.PlatformView);
                    });

                    Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("PrayAdFree.PickerTint", static (handler, _) => {
                        if (handler.PlatformView is null) {
                            return;
                        }

                        handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(
                            ResolveAndroidThemeColor("InputTint", "InputTintDark", "#2FB79D"));
                        ApplyAndroidInputSurface(handler.PlatformView);
                    });

                    Microsoft.Maui.Handlers.DatePickerHandler.Mapper.AppendToMapping("PrayAdFree.DatePickerTint", static (handler, _) => {
                        if (handler.PlatformView is null) {
                            return;
                        }

                        handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(
                            ResolveAndroidThemeColor("InputTint", "InputTintDark", "#2FB79D"));
                        ApplyAndroidInputSurface(handler.PlatformView);
                    });

                    Microsoft.Maui.Handlers.TimePickerHandler.Mapper.AppendToMapping("PrayAdFree.TimePickerTint", static (handler, _) => {
                        if (handler.PlatformView is null) {
                            return;
                        }

                        handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(
                            ResolveAndroidThemeColor("InputTint", "InputTintDark", "#2FB79D"));
                        ApplyAndroidInputSurface(handler.PlatformView);
                    });

                    Microsoft.Maui.Handlers.SwitchHandler.Mapper.AppendToMapping("PrayAdFree.SwitchTint", static (handler, _) => {
                        if (handler.PlatformView is not AndroidX.AppCompat.Widget.SwitchCompat switchCompat) {
                            return;
                        }

                        ApplyAndroidSwitchSurface(switchCompat);
                    });
#endif
                })
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

                    options.AddCategory(new NotificationCategory(NotificationCategoryType.Status) {
                        ActionList = new HashSet<NotificationAction> { stopAction }
                    });
                    options.AddCategory(new NotificationCategory(NotificationCategoryType.Event) {
                        ActionList = new HashSet<NotificationAction> { customSnoozeAction, stopAction }
                    });
                    options.AddCategory(new NotificationCategory(NotificationCategoryType.Recommendation) {
                        ActionList = new HashSet<NotificationAction> { snooze10Action, customSnoozeAction, stopAction }
                    });
                })
                .ConfigureFonts(fonts => {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("Cairo-Variable.ttf", "Cairo");
                    fonts.AddFont("NotoNaskhArabic-Variable.ttf", "NotoNaskhArabic");
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
                client.DefaultRequestHeaders.UserAgent.ParseAdd("PrayAdFree/1.0 (contact: rynex@rynex.nl)");
            });
            builder.Services.AddSingleton<GeoService>(sp => new GeoService(
                new IGeoProvider[] {
                    sp.GetRequiredService<PhotonGeoProvider>(),
                    sp.GetRequiredService<NominatimGeoProvider>()
                },
                Path.Combine(FileSystem.AppDataDirectory, "geo_cache.json")
            ));
            builder.Services.AddSingleton<IGeoLookupService>(sp => sp.GetRequiredService<GeoService>());
            builder.Services.AddSingleton<ILocationProvider, LocationProvider>();
            builder.Services.AddHttpClient<IIpLocationService, IpLocationService>(client => {
                client.BaseAddress = new Uri("https://ipapi.co/");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("PrayAdFree/1.0 (contact: rynex@rynex.nl)");
            });
            builder.Services.AddSingleton<INetworkPrivacyService, NetworkPrivacyService>();
            builder.Services.AddSingleton<IWindowsBackgroundModeService, WindowsBackgroundModeService>();
            builder.Services.AddSingleton<IAppLogger, AppLogger>();
            builder.Services.AddSingleton<IMauiWebberLogger, MauiWebberAppLogger>();
            builder.Services.AddSingleton<AlarmReminderCatalogService>();
            builder.Services.AddSingleton<AdhanPlaybackService>();
            builder.Services.AddSingleton<IAdhanPlaybackService>(sp => sp.GetRequiredService<AdhanPlaybackService>());
#if WINDOWS
            builder.Services.AddSingleton<IWindowsNotificationQueueService, WindowsNotificationQueueService>();
#else
            builder.Services.AddSingleton<IWindowsNotificationQueueService, NullWindowsNotificationQueueService>();
#endif
            builder.Services.AddSingleton<PrayerSchedulePlanner>();
            builder.Services.AddSingleton<AndroidAlarmCapabilityService>();
            builder.Services.AddSingleton<ILocalNotificationScheduler, LocalNotificationScheduler>();
            builder.Services.AddSingleton(_ => new PrayerTimesCache(FileSystem.AppDataDirectory));
            builder.Services.AddHttpClient<IPrayerTimesClient, AladhanPrayerTimesClient>();
            builder.Services.AddSingleton<PrayerTimesService>();
            builder.Services.AddSingleton<PrayerDataService>();
            builder.Services.AddSingleton<NotificationBootstrapper>();
            builder.Services.AddSingleton<INotificationBootstrapper>(sp => sp.GetRequiredService<NotificationBootstrapper>());
            builder.Services.AddSingleton(new MauiWebberOptions {
                AppId = "prayadfree-app",
                EmbeddedRoot = "web",
                RemoteBaseUrl = new Uri("http://pray.rynex.nl/"),
                ManifestUrl = new Uri("http://pray.rynex.nl/webber-manifest.json"),
                StorageFolderName = "MauiWebber",
                StartupFile = "index.html",
                UpdatePolicy = MauiWebberUpdatePolicy.LocalFirst,
                RollbackEnabled = true,
                IntegrityMode = MauiWebberIntegrityMode.OptionalHash,
                AppendJsLog = true,
                RequiredContractVersion = WebContractExporter.SchemaVersion
            });
            builder.Services.AddSingleton(sp => new MauiWebberUpdater(
                sp.GetRequiredService<MauiWebberOptions>(),
                CreateMauiWebberHttpClient(),
                sp.GetRequiredService<IMauiWebberLogger>()));
            builder.Services.AddTransient<TodayWebRpcHandler>();
            builder.Services.AddTransient<WebAppRpcHandler>();

            builder.Services.AddTransient<HomeViewModel>();
            builder.Services.AddTransient<CalendarViewModel>();
            builder.Services.AddTransient<QiblaViewModel>();
            builder.Services.AddSingleton<SettingsViewModel>();
            builder.Services.AddTransient<AboutViewModel>();
            builder.Services.AddTransient<TasbihViewModel>();
            builder.Services.AddTransient<LanguageSelectionViewModel>();
            builder.Services.AddTransient<AlarmRemindersViewModel>();
            builder.Services.AddTransient<AppPermissionsViewModel>();
            builder.Services.AddTransient<LocationSetupViewModel>();
            builder.Services.AddTransient<OnboardingViewModel>();

            builder.Services.AddTransient<HomePage>();
            builder.Services.AddTransient<TodayWebPage>();
            builder.Services.AddTransient<AdhanSnoozePage>();
            builder.Services.AddTransient<CalendarPage>();
            builder.Services.AddTransient<QiblaPage>();
            builder.Services.AddTransient<TasbihPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<SettingsLocationsPage>();
            builder.Services.AddTransient<SettingsDiagnosticsPage>();
            builder.Services.AddTransient<SettingsAdhanPage>();
            builder.Services.AddTransient<SettingsNotificationsPage>();
            builder.Services.AddTransient<SettingsPermissionsPage>();
            builder.Services.AddTransient<SettingsAlarmRemindersPage>();
            builder.Services.AddTransient<SettingsTasbihPage>();
            builder.Services.AddTransient<AboutPage>();
            builder.Services.AddTransient<LanguageSelectionPage>();
            builder.Services.AddTransient<OnboardingPage>();
            builder.Services.AddTransient<AppShell>();

            builder.Services.AddSingleton<AppPermissionCenterService>();
            builder.Services.AddSingleton<IAppPermissionCenterService>(sp => sp.GetRequiredService<AppPermissionCenterService>());
            builder.Services.AddSingleton<IStartupNavigationService, StartupNavigationService>();

            return builder.Build();
        }

        private static HttpClient CreateMauiWebberHttpClient() {
#if DEBUG
            return new HttpClient(new HttpClientHandler {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
#else
            return new HttpClient();
#endif
        }

        private static string ResolveStopActionTitle() {
            return LocalizationManager.Translate("Stop");
        }

        private static string ResolveSnooze10ActionTitle() {
            return string.Format(LocalizationManager.Translate("SnoozeDelayLabel"), 10);
        }

        private static string ResolveCustomSnoozeActionTitle() {
            return LocalizationManager.Translate("SnoozePageTitle");
        }

#if ANDROID
        private static void ApplyAndroidInputSurface(Android.Views.View view) {
            view.Background = BuildAndroidInputDrawable(view.Context);
            var density = view.Context?.Resources?.DisplayMetrics?.Density ?? 1f;
            var horizontal = (int)(12 * density);
            var vertical = (int)(10 * density);
            view.SetPadding(horizontal, vertical, horizontal, vertical);
        }

        private static Android.Graphics.Drawables.GradientDrawable BuildAndroidInputDrawable(Android.Content.Context? context) {
            var density = context?.Resources?.DisplayMetrics?.Density ?? 1f;
            var fill = ResolveAndroidThemeColor("InputFill", "InputFillDark", "#ECF4F0");
            var stroke = ResolveAndroidThemeColor("InputStroke", "InputStrokeDark", "#BAD4C9");

            var drawable = new Android.Graphics.Drawables.GradientDrawable();
            drawable.SetShape(Android.Graphics.Drawables.ShapeType.Rectangle);
            drawable.SetColor(fill);
            drawable.SetCornerRadius(14f * density);
            drawable.SetStroke(Math.Max(1, (int)Math.Round(density)), stroke);
            return drawable;
        }

        private static void ApplyAndroidSwitchSurface(AndroidX.AppCompat.Widget.SwitchCompat switchCompat) {
            var trackStates = new[] {
                new[] { Android.Resource.Attribute.StateEnabled, Android.Resource.Attribute.StateChecked },
                new[] { Android.Resource.Attribute.StateEnabled, -Android.Resource.Attribute.StateChecked },
                new[] { -Android.Resource.Attribute.StateEnabled, Android.Resource.Attribute.StateChecked },
                new[] { -Android.Resource.Attribute.StateEnabled, -Android.Resource.Attribute.StateChecked }
            };
            var trackColors = new[] {
                ResolveAndroidThemeColor("SwitchTrackOn", "SwitchTrackOnDark", "#7BC9B7"),
                ResolveAndroidThemeColor("SwitchTrackOff", "SwitchTrackOffDark", "#54606C"),
                ResolveAndroidThemeColor("PrimaryDisabled", "PrimaryDisabledDark", "#6A5032"),
                ResolveAndroidThemeColor("InputFillDisabled", "InputFillDisabledDark", "#18222C")
            };
            var thumbColors = new[] {
                ResolveAndroidThemeColor("SwitchThumbOn", "SwitchThumbOnDark", "#FFFFFF"),
                ResolveAndroidThemeColor("SwitchThumbOff", "SwitchThumbOffDark", "#FFFFFF"),
                ResolveAndroidThemeColor("PrimaryDisabledForeground", "PrimaryDisabledForegroundDark", "#FFFFFF"),
                ResolveAndroidThemeColor("InputForegroundDisabled", "InputForegroundDisabledDark", "#FFFFFF")
            };
            switchCompat.TrackTintList = new Android.Content.Res.ColorStateList( trackStates , Array.ConvertAll( trackColors , color => color.ToArgb() ) );

            switchCompat.ThumbTintList = new Android.Content.Res.ColorStateList( trackStates , Array.ConvertAll( thumbColors , color => color.ToArgb() ) );
            //switchCompat.TrackTintList = new Android.Content.Res.ColorStateList(trackStates, trackColors);
            //switchCompat.ThumbTintList = new Android.Content.Res.ColorStateList(trackStates, thumbColors);
        }

        private static Android.Graphics.Color ResolveAndroidThemeColor(string lightKey, string darkKey, string fallbackHex) {
            var resource = Application.Current?.Resources;
            var key = IsDarkThemeActive() ? darkKey : lightKey;

            if (resource != null && resource.TryGetValue(key, out var value)) {
                if (value is Microsoft.Maui.Graphics.Color mauiColor) {
                    return Android.Graphics.Color.Argb(
                        (int)Math.Round(mauiColor.Alpha * 255),
                        (int)Math.Round(mauiColor.Red * 255),
                        (int)Math.Round(mauiColor.Green * 255),
                        (int)Math.Round(mauiColor.Blue * 255));
                }

                if (value is SolidColorBrush brush) {
                    var brushColor = brush.Color;
                    return Android.Graphics.Color.Argb(
                        (int)Math.Round(brushColor.Alpha * 255),
                        (int)Math.Round(brushColor.Red * 255),
                        (int)Math.Round(brushColor.Green * 255),
                        (int)Math.Round(brushColor.Blue * 255));
                }
            }

            return Android.Graphics.Color.ParseColor(fallbackHex);
        }

        private static bool IsDarkThemeActive() {
            var appTheme = Application.Current?.UserAppTheme ?? AppTheme.Unspecified;
            if (appTheme == AppTheme.Unspecified) {
                appTheme = Application.Current?.RequestedTheme ?? AppTheme.Unspecified;
            }

            return appTheme != AppTheme.Light;
        }
#endif
    }
}
