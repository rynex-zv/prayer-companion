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
            AppNotificationManager.Default.NotificationInvoked += (_, _) => { };
            AppNotificationManager.Default.Register();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args) {
            var backgroundLaunch = WindowsBackgroundModeService.IsBackgroundLaunch(args.Arguments);
            RegisterNotifications();
            base.OnLaunched(args);
            HookBackgroundBehavior(backgroundLaunch);
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
            if (!_backgroundModeService.IsEnabled()) {
                return;
            }

            args.Cancel = true;
            if (_windowHandle != IntPtr.Zero) {
                ShowWindow(_windowHandle, SwHide);
            }
        }
    }

}
