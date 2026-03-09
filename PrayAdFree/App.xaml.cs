using Microsoft.Extensions.DependencyInjection;
using Pray_Ad_Free.Services;

namespace Pray_Ad_Free {
    public partial class App : Application {
        public static IServiceProvider? Services { get; private set; }
        private readonly IServiceProvider _services;
        private readonly IAppLogger _logger;

        public App(IServiceProvider services, IAppLogger logger) {
            InitializeComponent();
            _services = services;
            Services = services;
            _logger = logger;
            RegisterExceptionHandlers();
            try {
                _services.GetRequiredService<IAdhanPlaybackService>().Initialize();
            } catch (Exception ex) {
                _logger.LogException(ex, "App.InitializeAdhanPlaybackService");
            }
            TryScheduleNotifications("AppCtor");
        }

        protected override Window CreateWindow( IActivationState? activationState ) {
            return new Window( _services.GetRequiredService<AppShell>() );
        }

        protected override void OnStart() {
            base.OnStart();
            TryScheduleNotifications("OnStart");
        }

        protected override void OnResume() {
            base.OnResume();
            TryScheduleNotifications("OnResume");
        }

        private void TryScheduleNotifications(string reason) {
            try {
                var bootstrapper = _services.GetRequiredService<NotificationBootstrapper>();
                _ = bootstrapper.EnsureScheduledAsync(reason, requestPermissions: true);
            } catch (Exception ex) {
                _logger.LogException(ex, "App.TryScheduleNotifications");
            }
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
    }
}
