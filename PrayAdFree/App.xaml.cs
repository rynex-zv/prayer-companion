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
        }

        protected override Window CreateWindow( IActivationState? activationState ) {
            return new Window( _services.GetRequiredService<AppShell>() );
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
