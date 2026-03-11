using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;
using PrayAdFree.Core.Services;
using Pray_Ad_Free.Services;
using Pray_Ad_Free.ViewModels;
using WinRT.Interop;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace Pray_Ad_Free.WinUI {
    public partial class App : MauiWinUIApplication {
        private static readonly object TraceLock = new();
        private static readonly string StartupTracePath = BuildTracePath();

        private IntPtr _windowHandle;
        private AppWindow? _appWindow;
        private readonly WindowsBackgroundModeService _backgroundModeService = new();
        private WinForms.NotifyIcon? _trayIcon;
        private WinForms.ContextMenuStrip? _trayMenu;
        private WinForms.ToolStripMenuItem? _trayExitItem;
        private WinForms.ToolStripMenuItem? _trayTestAdhanItem;
        private WinForms.ToolStripMenuItem? _trayStartWithWindowsItem;
        private Drawing.Icon? _trayIconImage;
        private bool _trayIconFromFile;
        private bool _hideOnLaunch;
        private bool _isExitRequested;

        public App() {
            Trace("WinUI.App.ctor:start");
            InitializeComponent();
            Trace("WinUI.App.ctor:end");
        }

        protected override MauiApp CreateMauiApp() {
            Trace("WinUI.CreateMauiApp:start");
            var app = MauiProgram.CreateMauiApp();
            Trace("WinUI.CreateMauiApp:end");
            return app;
        }

        private static void RegisterNotifications() {
            Trace("WinUI.RegisterNotifications:start");
            SetCurrentProcessExplicitAppUserModelID("com.rynex.prayadfree");
            AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
            AppNotificationManager.Default.Register();
            Trace("WinUI.RegisterNotifications:end");
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
            Dictionary<string, string?>? values = null;
            if (args.Arguments is System.Collections.IDictionary dictionary && dictionary.Count > 0) {
                values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (System.Collections.DictionaryEntry entry in dictionary) {
                    var key = entry.Key?.ToString();
                    if (string.IsNullOrWhiteSpace(key)) {
                        continue;
                    }

                    values[key] = entry.Value?.ToString();
                }
            }

            return WindowsNotificationActionParser.ShouldStopAdhan(args.Argument, values);
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args) {
            Trace($"WinUI.OnLaunched:start args='{args.Arguments ?? string.Empty}'");
            _hideOnLaunch = WindowsBackgroundModeService.IsBackgroundLaunch(args.Arguments) && _backgroundModeService.IsEnabled();
            Trace($"WinUI.OnLaunched:hideOnLaunch={_hideOnLaunch}");

            try {
                RegisterNotifications();
            } catch (Exception ex) {
                Trace($"WinUI.OnLaunched:RegisterNotifications:exception {ex}");
            }

            Trace("WinUI.OnLaunched:beforeBase");
            base.OnLaunched(args);
            Trace("WinUI.OnLaunched:afterBase");

            try {
                HookBackgroundBehavior();
                Trace("WinUI.OnLaunched:afterHookBackgroundBehavior");
            } catch (Exception ex) {
                Trace($"WinUI.OnLaunched:HookBackgroundBehavior:exception {ex}");
            }
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
            Trace("WinUI.HookBackgroundBehavior:start");
            var mauiWindow = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
            if (mauiWindow?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window nativeWindow) {
                Trace("WinUI.HookBackgroundBehavior:noNativeWindow");
                return;
            }

            _windowHandle = WindowNative.GetWindowHandle(nativeWindow);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_windowHandle);
            _appWindow = AppWindow.GetFromWindowId(windowId);
            Trace($"WinUI.HookBackgroundBehavior:windowHandle={_windowHandle}");
            _appWindow.Closing -= OnAppWindowClosing;
            _appWindow.Closing += OnAppWindowClosing;

            InitializeTrayIcon();

            if (_hideOnLaunch) {
                Trace("WinUI.HookBackgroundBehavior:HideWindow");
                HideWindow();
            }

            Trace("WinUI.HookBackgroundBehavior:end");
        }

        private void InitializeTrayIcon() {
            Trace("WinUI.InitializeTrayIcon:start");
            if (_trayIcon != null) {
                Trace("WinUI.InitializeTrayIcon:alreadyInitialized");
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

            var trayIconPath = Path.Combine(AppContext.BaseDirectory, "tray.ico");
            if (File.Exists(trayIconPath)) {
                _trayIconImage = new Drawing.Icon(trayIconPath);
                _trayIconFromFile = true;
            } else {
                _trayIconImage = Drawing.SystemIcons.Application;
                _trayIconFromFile = false;
            }

            _trayIcon = new WinForms.NotifyIcon {
                Icon = _trayIconImage,
                Text = "Pray Ad Free",
                Visible = true,
                ContextMenuStrip = _trayMenu
            };
            _trayIcon.MouseDoubleClick += OnTrayIconMouseDoubleClick;
            Trace("WinUI.InitializeTrayIcon:end");
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
            Trace("WinUI.OnTrayExitClicked");
            _isExitRequested = true;
            DisposeTrayIcon();
            Exit();
        }

        private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args) {
            Trace("WinUI.OnAppWindowClosing:start");
            if (_isExitRequested) {
                DisposeTrayIcon();
                Trace("WinUI.OnAppWindowClosing:exitRequested");
                return;
            }

            if (!ShouldHideOnClose()) {
                DisposeTrayIcon();
                Trace("WinUI.OnAppWindowClosing:allowClose");
                return;
            }

            args.Cancel = true;
            HideWindow();
            Trace("WinUI.OnAppWindowClosing:hidden");
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
                Trace("WinUI.HideWindow:applied");
            }
        }

        private void RestoreWindow() {
            if (_windowHandle == IntPtr.Zero) {
                Trace("WinUI.RestoreWindow:noHandle");
                return;
            }

            ShowWindow(_windowHandle, SwShow);
            ShowWindow(_windowHandle, SwRestore);
            SetForegroundWindow(_windowHandle);
            Trace("WinUI.RestoreWindow:applied");
        }

        private void DisposeTrayIcon() {
            Trace("WinUI.DisposeTrayIcon:start");
            if (_trayIcon != null) {
                _trayIcon.Visible = false;
                _trayIcon.MouseDoubleClick -= OnTrayIconMouseDoubleClick;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            if (_trayIconImage != null && _trayIconFromFile) {
                _trayIconImage.Dispose();
                _trayIconImage = null;
            }
            _trayIconFromFile = false;

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
            Trace("WinUI.DisposeTrayIcon:end");
        }

        private static string BuildTracePath() {
            try {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var dir = Path.Combine(desktop, "PrayAdFreeLogs");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "startup-trace.log");
            } catch {
                return Path.Combine(Path.GetTempPath(), "PrayAdFree-startup-trace.log");
            }
        }

        private static void Trace(string message) {
            try {
                var line = $"{DateTime.UtcNow:O} | T{Environment.CurrentManagedThreadId} | {message}";
                lock (TraceLock) {
                    File.AppendAllText(StartupTracePath, line + Environment.NewLine, Encoding.UTF8);
                }
            } catch {
            }
        }
    }
}
