using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Services;

public enum StartupTarget {
    Onboarding,
    Shell
}

public interface IStartupNavigationService {
    StartupTarget ResolveTarget(AppSettings settings);
    Page CreateStartupPage();
    Task PrepareShellAsync(AppSettings? settings = null);
    Task ActivateShellAsync(AppSettings? settings = null);
}
