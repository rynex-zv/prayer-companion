using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;
using WinRT.Interop;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

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
        private WinForms.NotifyIcon? _trayIcon;
        private WinForms.ContextMenuStrip? _trayMenu;
        private WinForms.ToolStripMenuItem? _trayExitItem;
        private WinForms.ToolStripMenuItem? _trayTestAdhanItem;
        private WinForms.ToolStripMenuItem? _trayStartWithWindowsItem;
        private bool _hideOnLaunch;
        private bool _isExitRequested;

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
            _hideOnLaunch = WindowsBackgroundModeService.IsBackgroundLaunch(args.Arguments) && _backgroundModeService.IsEnabled();

            RegisterNotifications();
            base.OnLaunched(args);
            HookBackgroundBehavior();
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int SwHide = 0;
        private const int SwShow = 5;
        private const int SwRestore = 9;

        private void HookBackgroundBehavior() {
            var mauiWindow = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
            if (mauiWindow?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window nativeWindow) {
                return;
            }

            _windowHandle = WindowNative.GetWindowHandle(nativeWindow);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_windowHandle);
            _appWindow = AppWindow.GetFromWindowId(windowId);
            _appWindow.Closing -= OnAppWindowClosing;
            _appWindow.Closing += OnAppWindowClosing;

            InitializeTrayIcon();

            if (_hideOnLaunch) {
                HideWindow();
            }
        }

        private void InitializeTrayIcon() {
            if (_trayIcon != null) {
                return;
            }

            _trayMenu = new WinForms.ContextMenuStrip();
            _trayMenu.Opening += OnTrayMenuOpening;
            _trayTestAdhanItem = new WinForms.ToolStripMenuItem();
            _trayTestAdhanItem.Click += OnTrayTestAdhanClicked;

            _trayStartWithWindowsItem = new WinForms.ToolStripMenuItem {
                CheckOnClick = true
            };
            _trayStartWithWindowsItem.Click += OnTrayStartWithWindowsClicked;

            _trayExitItem = new WinForms.ToolStripMenuItem();
            _trayExitItem.Click += OnTrayExitClicked;

            _trayMenu.Items.Add(_trayTestAdhanItem);
            _trayMenu.Items.Add(_trayStartWithWindowsItem);
            _trayMenu.Items.Add(new WinForms.ToolStripSeparator());
            _trayMenu.Items.Add(_trayExitItem);

            _trayIcon = new WinForms.NotifyIcon {
                Icon = Drawing.SystemIcons.Application,
                Text = "Pray Ad Free",
                Visible = true,
                ContextMenuStrip = _trayMenu
            };
            _trayIcon.MouseDoubleClick += OnTrayIconMouseDoubleClick;
        }

        private void OnTrayIconMouseDoubleClick(object? sender, WinForms.MouseEventArgs e) {
            if (e.Button == WinForms.MouseButtons.Left) {
                RestoreWindow();
            }
        }

        private void OnTrayMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e) {
            if (_trayMenu == null || _trayTestAdhanItem == null || _trayStartWithWindowsItem == null || _trayExitItem == null) {
                return;
            }

            _trayTestAdhanItem.Text = LocalizationManager.Translate("TrayTestAdhan");
            _trayStartWithWindowsItem.Text = LocalizationManager.Translate("TrayStartMinimizedWithWindows");
            _trayExitItem.Text = LocalizationManager.Translate("TrayExitApp");
            _trayStartWithWindowsItem.Checked = _backgroundModeService.IsEnabled();
        }

        private async void OnTrayTestAdhanClicked(object? sender, EventArgs e) {
            try {
                if (global::Pray_Ad_Free.App.Services?.GetService(typeof(SettingsService)) is not SettingsService settingsService ||
                    global::Pray_Ad_Free.App.Services?.GetService(typeof(IAdhanPlaybackService)) is not IAdhanPlaybackService playbackService) {
                    return;
                }

                var soundKey = settingsService.Load().Notifications.SoundKey;
                await playbackService.PlayPreviewAsync(soundKey).ConfigureAwait(false);
            } catch {
            }
        }

        private void OnTrayStartWithWindowsClicked(object? sender, EventArgs e) {
            if (_trayStartWithWindowsItem == null) {
                return;
            }

            var requested = _trayStartWithWindowsItem.Checked;
            var applied = _backgroundModeService.SetEnabled(requested);
            _trayStartWithWindowsItem.Checked = applied;
        }

        private void OnTrayExitClicked(object? sender, EventArgs e) {
            _isExitRequested = true;
            DisposeTrayIcon();
            Exit();
        }

        private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args) {
            if (_isExitRequested) {
                DisposeTrayIcon();
                return;
            }

            if (!ShouldHideOnClose()) {
                DisposeTrayIcon();
                return;
            }

            args.Cancel = true;
            HideWindow();
        }

        private bool ShouldHideOnClose() {
            try {
                if (global::Pray_Ad_Free.App.Services?.GetService(typeof(SettingsViewModel)) is SettingsViewModel viewModel) {
                    return viewModel.HideOnCloseEnabled;
                }

                if (global::Pray_Ad_Free.App.Services?.GetService(typeof(SettingsService)) is SettingsService settingsService) {
                    return settingsService.Load().Notifications.HideOnCloseOnWindows;
                }
            } catch {
            }

            return false;
        }

        private void HideWindow() {
            if (_windowHandle != IntPtr.Zero) {
                ShowWindow(_windowHandle, SwHide);
            }
        }

        private void RestoreWindow() {
            if (_windowHandle == IntPtr.Zero) {
                return;
            }

            ShowWindow(_windowHandle, SwShow);
            ShowWindow(_windowHandle, SwRestore);
            SetForegroundWindow(_windowHandle);
        }

        private void DisposeTrayIcon() {
            if (_trayIcon != null) {
                _trayIcon.Visible = false;
                _trayIcon.MouseDoubleClick -= OnTrayIconMouseDoubleClick;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            if (_trayTestAdhanItem != null) {
                _trayTestAdhanItem.Click -= OnTrayTestAdhanClicked;
                _trayTestAdhanItem = null;
            }

            if (_trayStartWithWindowsItem != null) {
                _trayStartWithWindowsItem.Click -= OnTrayStartWithWindowsClicked;
                _trayStartWithWindowsItem = null;
            }

            if (_trayExitItem != null) {
                _trayExitItem.Click -= OnTrayExitClicked;
                _trayExitItem = null;
            }

            if (_trayMenu != null) {
                _trayMenu.Opening -= OnTrayMenuOpening;
                _trayMenu.Dispose();
                _trayMenu = null;
            }
        }
    }
}
