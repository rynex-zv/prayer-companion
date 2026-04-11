using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;
#if ANDROID
using Pray_Ad_Free.Platforms.Android;
#endif
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free {
    public partial class App : Application {
        public static IServiceProvider? Services { get; private set; }
        private readonly IServiceProvider _services;
        private readonly IAppLogger _logger;
        private readonly IStartupNavigationService _startupNavigationService;
        private ThemeVariant _activeThemeVariant = ThemeVariant.B;
        private Window? _mainWindow;
        private bool _startupNotificationBootstrapQueued;

        public App(IServiceProvider services, IAppLogger logger) {
            InitializeComponent();
            _services = services;
            Services = services;
            _logger = logger;
            _startupNavigationService = services.GetRequiredService<IStartupNavigationService>();
            _logger.LogEvent("AppCtor", "start");
            RegisterExceptionHandlers();

            var startupSettings = LoadSettingsSafe();
            LocalizationBootstrapper.EnsureInitialized(startupSettings.Language);
            _activeThemeVariant = startupSettings.ThemeVariant;
            _logger.LogEvent("AppCtor", $"Theme.Apply.start:{_activeThemeVariant}");
            try {
                ThemeManager.ApplyTheme(startupSettings);
                _logger.LogEvent("AppCtor", "Theme.Apply.ok");
            } catch (Exception ex) {
                _logger.LogException(ex, "App.ThemeManager.ApplyTheme");
                _logger.LogEvent("AppCtor", "Theme.Apply.failed");
            }

            try {
                _logger.LogEvent("AppCtor", "AdhanPlayback.Resolve.start");
                var playbackService = _services.GetRequiredService<IAdhanPlaybackService>();
                _logger.LogEvent("AppCtor", "AdhanPlayback.Resolve.ok");
                _logger.LogEvent("AppCtor", "AdhanPlayback.Initialize.start");
                playbackService.Initialize();
                _logger.LogEvent("AppCtor", "AdhanPlayback.Initialize.ok");
            } catch (Exception ex) {
                _logger.LogException(ex, "App.InitializeAdhanPlaybackService");
            }
            TryProcessPendingAlarmUi("AppCtor");
            _logger.LogEvent("AppCtor", "end");
        }

        protected override Window CreateWindow( IActivationState? activationState ) {
            try {
                _logger.LogEvent("CreateWindow", "beforeResolveRoot");
                var rootPage = _startupNavigationService.CreateStartupPage();
                _mainWindow = new Window(rootPage);
                QueueStartupNotificationBootstrap("CreateWindow");
                TryProcessPendingAlarmUi("CreateWindow");
#if ANDROID
                WidgetUpdateCoordinator.RequestImmediateRefresh("CreateWindow");
#endif
                return _mainWindow;
            } catch (Exception ex) {
                _logger.LogException(ex, "App.CreateWindow");
                _logger.LogEvent("CreateWindow", "fallbackPage");
                var fallbackPage = new ContentPage {
                    BackgroundColor = Color.FromArgb("#08111D"),
                    Content = new ScrollView {
                        Content = new VerticalStackLayout {
                            Padding = new Thickness(20, 36, 20, 20),
                            Spacing = 12,
                            Children = {
                                new Label {
                                    Text = "Failed to load UI.",
                                    TextColor = Colors.White,
                                    FontAttributes = FontAttributes.Bold,
                                    FontSize = 22
                                },
                                new Label {
                                    Text = ex.Message,
                                    TextColor = Color.FromArgb("#D3E2F5"),
                                    FontSize = 14
                                }
                            }
                        }
                    }
                };
                return new Window(fallbackPage);
            }
        }

        private void QueueStartupNotificationBootstrap(string reason) {
            if (_startupNotificationBootstrapQueued) {
                return;
            }

            _startupNotificationBootstrapQueued = true;
            _ = MainThread.InvokeOnMainThreadAsync(() => {
                TryScheduleNotifications(reason);
                return Task.CompletedTask;
            });
        }

        public static Task ReloadShellForThemeVariantAsync(ThemeVariant variant) {
            if (Current is not App app) {
                return Task.CompletedTask;
            }

            return app.ReloadShellInternalAsync(variant);
        }

        protected override void OnStart() {
            base.OnStart();
            TryScheduleNotifications("OnStart");
            TryProcessPendingAlarmUi("OnStart");
#if ANDROID
            WidgetUpdateCoordinator.RequestImmediateRefresh("OnStart");
#endif
        }

        protected override void OnResume() {
            base.OnResume();
            TryScheduleNotifications("OnResume");
            TryProcessPendingAlarmUi("OnResume");
#if ANDROID
            WidgetUpdateCoordinator.RequestImmediateRefresh("OnResume");
#endif
        }

        private void TryScheduleNotifications(string reason) {
            try {
                var settings = LoadSettingsSafe();
                if (!settings.OnboardingCompleted) {
                    _logger.LogEvent("TryScheduleNotifications", $"skip_onboarding:{reason}");
                    return;
                }

                _logger.LogEvent("TryScheduleNotifications", $"start:{reason}");
                var bootstrapper = _services.GetRequiredService<NotificationBootstrapper>();
                _ = bootstrapper.EnsureScheduledAsync(reason, requestPermissions: false);
                _logger.LogEvent("TryScheduleNotifications", $"queued:{reason}");
            } catch (Exception ex) {
                _logger.LogException(ex, "App.TryScheduleNotifications");
            }
        }

        internal static void NotifyUiActivated(string reason) {
            if (Current is App app) {
                app.TryProcessPendingAlarmUi(reason);
            }
        }

        private void TryProcessPendingAlarmUi(string reason) {
#if ANDROID
            try {
                AndroidAlarmLaunchCoordinator.TryDispatchPending(reason);
                if (Services?.GetService(typeof(AdhanPlaybackService)) is AdhanPlaybackService playbackService) {
                    _ = playbackService.TryPresentPendingAlarmScreenAsync(reason);
                }
            } catch (Exception ex) {
                _logger.LogException(ex, $"App.TryProcessPendingAlarmUi:{reason}");
            }
#endif
        }

        private void RegisterExceptionHandlers() {
            AppDomain.CurrentDomain.UnhandledException += (_, args) => {
                if (args.ExceptionObject is Exception exception) {
                    _logger.LogException(exception, "AppDomain.UnhandledException");
                } else {
                    _logger.LogException(new Exception("Unknown unhandled exception"), "AppDomain.UnhandledException");
                }
            };

            TaskScheduler.UnobservedTaskException += (_, args) => {
                _logger.LogException(args.Exception, "TaskScheduler.UnobservedTaskException");
                args.SetObserved();
            };
        }

        private AppSettings LoadSettingsSafe() {
            try {
                return _services.GetRequiredService<SettingsService>().Load();
            } catch (Exception ex) {
                _logger.LogException(ex, "App.LoadSettingsSafe");
                return new AppSettings();
            }
        }

        private Shell CreateShellForVariant(ThemeVariant variant) {
            _logger.LogEvent("CreateShell", $"variant:{variant}");
            return variant == ThemeVariant.A
                ? _services.GetRequiredService<AppShellA>()
                : _services.GetRequiredService<AppShell>();
        }

        private async Task ReloadShellInternalAsync(ThemeVariant variant) {
            await MainThread.InvokeOnMainThreadAsync(() => {
                try {
                    var currentWindow = _mainWindow ?? Current?.Windows.FirstOrDefault();
                    if (currentWindow == null) {
                        _logger.LogEvent("ReloadShell", "skip:noWindow");
                        return;
                    }

                    _activeThemeVariant = variant;
                    currentWindow.Page = CreateShellForVariant(variant);
                    _mainWindow = currentWindow;
                    _logger.LogEvent("ReloadShell", $"applied:{variant}");
                } catch (Exception ex) {
                    _logger.LogException(ex, "App.ReloadShellInternalAsync");
                }
            }).ConfigureAwait(false);
        }
    }
}
