using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;
using Pray_Ad_Free.Services;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Pray_Ad_Free.WinUI {
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication {
        private IntPtr _windowHandle;
        private AppWindow? _appWindow;
        private readonly WindowsBackgroundModeService _backgroundModeService = new();
        private bool _isBackgroundWorkerLaunch;

        /// <summary>
        /// Initializes the singleton application object. This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App() {
            this.InitializeComponent();
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        private static void RegisterNotifications() {
            SetCurrentProcessExplicitAppUserModelID("com.rynex.prayadfree");
            AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
            AppNotificationManager.Default.Register();
        }

        private static void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args) {
            try {
                if (!IsControlNotificationInvocation(args)) {
                    return;
                }

                if (global::Pray_Ad_Free.App.Services?.GetService(typeof(IAdhanPlaybackService)) is IAdhanPlaybackService playbackService) {
                    _ = playbackService.StopAsync();
                }
            } catch {
            }
        }

        private static bool IsControlNotificationInvocation(AppNotificationActivatedEventArgs args) {
            var argument = args.Argument ?? string.Empty;
            if (string.Equals(argument, AdhanPlaybackService.WindowsControlNotificationSourceToken, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument, $"source={AdhanPlaybackService.WindowsControlNotificationSourceToken}", StringComparison.OrdinalIgnoreCase) ||
                argument.Contains($"source={AdhanPlaybackService.WindowsControlNotificationSourceToken}", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            if (string.Equals(argument, AdhanPlaybackService.WindowsStopActionToken, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument, $"action={AdhanPlaybackService.WindowsStopActionToken}", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            if (args.Arguments is System.Collections.IDictionary values &&
                values.Contains("source") &&
                values["source"] is string sourceValue &&
                string.Equals(sourceValue, AdhanPlaybackService.WindowsControlNotificationSourceToken, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            if (args.Arguments is System.Collections.IDictionary actionValues &&
                actionValues.Contains("action") &&
                actionValues["action"] is string actionValue &&
                string.Equals(actionValue, AdhanPlaybackService.WindowsStopActionToken, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            return argument.Contains($"action={AdhanPlaybackService.WindowsStopActionToken}", StringComparison.OrdinalIgnoreCase);
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args) {
            _isBackgroundWorkerLaunch = WindowsBackgroundModeService.IsBackgroundLaunch(args.Arguments);
            if (_isBackgroundWorkerLaunch) {
                WindowsBackgroundModeService.RegisterCurrentBackgroundProcess();
            }

            RegisterNotifications();
            base.OnLaunched(args);
            HookBackgroundBehavior(_isBackgroundWorkerLaunch);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SwHide = 0;

        private void HookBackgroundBehavior(bool hideOnLaunch) {
            var mauiWindow = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
            if (mauiWindow?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window nativeWindow) {
                return;
            }

            _windowHandle = WindowNative.GetWindowHandle(nativeWindow);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_windowHandle);
            _appWindow = AppWindow.GetFromWindowId(windowId);
            _appWindow.Closing -= OnAppWindowClosing;
            _appWindow.Closing += OnAppWindowClosing;

            if (hideOnLaunch && _backgroundModeService.IsEnabled()) {
                ShowWindow(_windowHandle, SwHide);
            }
        }

        private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args) {
            if (!_isBackgroundWorkerLaunch) {
                return;
            }

            if (!_backgroundModeService.IsEnabled()) {
                WindowsBackgroundModeService.UnregisterCurrentBackgroundProcess();
                return;
            }

            args.Cancel = true;
            if (_windowHandle != IntPtr.Zero) {
                ShowWindow(_windowHandle, SwHide);
            }
        }
    }

}
