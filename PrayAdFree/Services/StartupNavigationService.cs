using Microsoft.Extensions.DependencyInjection;
using PrayAdFree.Core.Models;
using PrayAdFree.Core.Services;

namespace Pray_Ad_Free.Services;

public sealed class StartupNavigationService : IStartupNavigationService {
    private readonly IServiceProvider _services;
    private readonly SettingsService _settingsService;
    private readonly IAppLogger _logger;
    private readonly object _sync = new();
    private Shell? _preparedShell;
    private string _preparedKey = string.Empty;

    public StartupNavigationService(IServiceProvider services, SettingsService settingsService, IAppLogger logger) {
        _services = services;
        _settingsService = settingsService;
        _logger = logger;
    }

    public StartupTarget ResolveTarget(AppSettings settings) {
        return HasCompletedSetup(settings)
            ? StartupTarget.Shell
            : StartupTarget.Onboarding;
    }

    public Page CreateStartupPage() {
        var settings = _settingsService.Load();
        return ResolveTarget(settings) == StartupTarget.Onboarding
            ? _services.GetRequiredService<Pages.OnboardingPage>()
            : GetOrCreateShell(settings);
    }

    public async Task PrepareShellAsync(AppSettings? settings = null) {
        settings ??= _settingsService.Load();
        var shellKey = BuildShellKey(settings);

        if (TryGetPreparedShell(shellKey, out _)) {
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(() => {
            lock (_sync) {
                if (_preparedShell != null && string.Equals(_preparedKey, shellKey, StringComparison.Ordinal)) {
                    return;
                }

                _preparedShell = CreateShell();
                _preparedKey = shellKey;
                _logger.LogEvent("StartupNavigation", $"prepared_shell:{shellKey}");
            }
        });
    }

    public async Task ActivateShellAsync(AppSettings? settings = null) {
        settings ??= _settingsService.Load();
        await PrepareShellAsync(settings).ConfigureAwait(false);

        await MainThread.InvokeOnMainThreadAsync(() => {
            var shell = GetOrCreateShell(settings);
            var window = Application.Current?.Windows.FirstOrDefault();
            if (window == null) {
                _logger.LogEvent("StartupNavigation", "activate_shell_skipped:no_window");
                return;
            }

            window.Page = shell;
            lock (_sync) {
                _preparedShell = null;
                _preparedKey = string.Empty;
            }
            _logger.LogEvent("StartupNavigation", "activate_shell:applied");
        });
    }

    private Shell GetOrCreateShell(AppSettings settings) {
        var shellKey = BuildShellKey(settings);
        if (TryGetPreparedShell(shellKey, out var shell)) {
            return shell!;
        }

        shell = CreateShell();
        lock (_sync) {
            _preparedShell = null;
            _preparedKey = string.Empty;
        }
        return shell;
    }

    private bool TryGetPreparedShell(string shellKey, out Shell? shell) {
        lock (_sync) {
            if (_preparedShell != null && string.Equals(_preparedKey, shellKey, StringComparison.Ordinal)) {
                shell = _preparedShell;
                return true;
            }
        }

        shell = null;
        return false;
    }

    private Shell CreateShell() {
        return _services.GetRequiredService<AppShell>();
    }

    private static string BuildShellKey(AppSettings settings) {
        return settings.Language;
    }

    private static bool HasCompletedSetup(AppSettings settings) {
        return settings.OnboardingCompleted;
    }
}
