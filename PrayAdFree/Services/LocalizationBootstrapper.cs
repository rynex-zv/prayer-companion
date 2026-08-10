using System.Threading;
using PrayAdFree.Core.Services;

namespace Pray_Ad_Free.Services;

public static class LocalizationBootstrapper {
    private static readonly object SyncLock = new();
    private static int _filesSynced;

    public static void EnsureInitialized(string? preferredLanguage = null) {
        EnsureFilesSynced();
        LocalizationManager.EnsureInitialized(ResolvePreferredLanguage(preferredLanguage));
    }

    private static void EnsureFilesSynced() {
        if (Volatile.Read(ref _filesSynced) == 1) {
            return;
        }

        lock (SyncLock) {
            if (_filesSynced == 1) {
                return;
            }

            try {
                new LocalizationFileSync().SyncIfNeeded();
                _filesSynced = 1;
            } catch {
            }
        }
    }

    private static string? ResolvePreferredLanguage(string? preferredLanguage) {
        if (!string.IsNullOrWhiteSpace(preferredLanguage)) {
            return preferredLanguage;
        }

        try {
            var settings = new SettingsService(new FileSettingsStore(AutomationRuntime.SettingsPath)).Load();
            return settings.Language;
        } catch {
            return preferredLanguage;
        }
    }
}
