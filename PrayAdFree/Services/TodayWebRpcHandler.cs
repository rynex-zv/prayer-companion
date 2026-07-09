using System.Globalization;
using System.Text.Json;
using MauiWebber;
using Pray_Ad_Free.ViewModels;

namespace Pray_Ad_Free.Services;

public sealed class TodayWebRpcHandler : IMauiWebberRpcHandler {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        WriteIndented = true
    };

    private readonly HomeViewModel _viewModel;
    private readonly IAppLogger _logger;
    private readonly object _sync = new();
    private readonly string _snapshotPath;
    private Task? _preloadTask;
    private TodayWebSnapshot? _lastSnapshot;
    private bool _backgroundRefreshRunning;
    private bool _cacheWarmupStarted;

    public TodayWebRpcHandler(HomeViewModel viewModel, IAppLogger logger) {
        _viewModel = viewModel;
        _logger = logger;
        _snapshotPath = Path.Combine(FileSystem.AppDataDirectory, "MauiWebber", "today-snapshot.json");
        _lastSnapshot = LoadCachedSnapshot();
    }

    public Task PreloadAsync() {
        lock (_sync) {
            _preloadTask ??= RefreshAndCacheAsync(force: false);
            return _preloadTask;
        }
    }

    public async Task<object?> HandleAsync(string method, JsonElement payload, CancellationToken cancellationToken) {
        return method switch {
            "today.getSnapshot" => await GetSnapshotAsync(refresh: false).ConfigureAwait(false),
            "today.refresh" => await GetSnapshotAsync(refresh: true).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unknown MauiWebber RPC method: {method}")
        };
    }

    private async Task<TodayWebSnapshot> GetSnapshotAsync(bool refresh) {
        if (refresh) {
            return await RefreshAndCacheAsync(force: true).ConfigureAwait(false);
        }

        if (_lastSnapshot != null) {
            StartCacheWarmup();
            if (!string.IsNullOrWhiteSpace(_viewModel.LocationTitle)) {
                await MainThread.InvokeOnMainThreadAsync(() => _viewModel.UpdateCountdown(DateTime.Now));
                _lastSnapshot = BuildSnapshot();
            }

            return _lastSnapshot;
        }

        return await RefreshAndCacheAsync(force: false).ConfigureAwait(false);
    }

    private void StartCacheWarmup() {
        lock (_sync) {
            if (_cacheWarmupStarted || _backgroundRefreshRunning) {
                return;
            }

            _cacheWarmupStarted = true;
            _backgroundRefreshRunning = true;
        }

        _ = Task.Run(async () => {
            try {
                await RefreshAndCacheAsync(force: false).ConfigureAwait(false);
            } catch (Exception ex) {
                lock (_sync) {
                    _cacheWarmupStarted = false;
                }

                _logger.LogException(ex, "TodayWebRpcHandler.BackgroundRefresh");
            } finally {
                lock (_sync) {
                    _backgroundRefreshRunning = false;
                }
            }
        });
    }

    private async Task<TodayWebSnapshot> RefreshAndCacheAsync(bool force) {
        if (force || string.IsNullOrWhiteSpace(_viewModel.LocationTitle)) {
            await _viewModel.RefreshAsync().ConfigureAwait(false);
        }

        await MainThread.InvokeOnMainThreadAsync(() => _viewModel.UpdateCountdown(DateTime.Now));

        var snapshot = BuildSnapshot();
        _lastSnapshot = snapshot;
        SaveSnapshot(snapshot);
        return snapshot;
    }

    private TodayWebSnapshot BuildSnapshot() {
        var isRtl = string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "ar", StringComparison.OrdinalIgnoreCase);

        return new TodayWebSnapshot(
            LocationTitle: _viewModel.LocationTitle,
            HijriDate: LocalizeHijriDate(_viewModel.HijriDate),
            GregorianDate: _viewModel.GregorianDate,
            CurrentTime: FormatLiveClock(DateTime.Now, _viewModel.CurrentClockFormat),
            NextPrayerName: _viewModel.NextPrayerName,
            NextPrayerClock: _viewModel.NextPrayerClock,
            NextPrayerBaseClock: _viewModel.NextPrayerBaseClock,
            ShowNextPrayerBaseClock: _viewModel.ShowNextPrayerBaseClock,
            NextPrayerDayLabel: _viewModel.NextPrayerDayLabel,
            Countdown: _viewModel.Countdown,
            StatusMessage: BuildStatusMessage(_viewModel.StatusMessage),
            ImsakTime: _viewModel.ImsakTime,
            IftarTime: _viewModel.IftarTime,
            IsImsakNext: _viewModel.IsImsakNext,
            IsIftarNext: _viewModel.IsIftarNext,
            NextFastingCountdown: _viewModel.NextFastingCountdown,
            IsRtl: isRtl,
            Labels: new TodayWebLabels(
                NextPrayer: LocalizationManager.Translate("NextPrayer"),
                TimeLeft: LocalizationManager.Translate("TimeLeft"),
                TodayPrayerTimes: LocalizationManager.Translate("TodayPrayTimesLabel"),
                Iftar: LocalizationManager.Translate("Iftar"),
                Imsak: LocalizationManager.Translate("Imsak"),
                Refresh: LocalizationManager.Translate("Refresh"),
                Refreshing: LocalizationManager.Translate("Refreshing"),
                Base: LocalizationManager.Translate("BaseTimeLabel")),
            TodayTimings: _viewModel.TodayTimings.Select(row => new TodayWebTiming(
                Id: row.Id.ToString(),
                Name: row.Name,
                Time: row.Time,
                BaseTime: row.BaseTime,
                ShowBaseTime: row.ShowBaseTime,
                IsNext: row.IsNext)).ToList());
    }

    private static string BuildStatusMessage(string statusMessage) {
        if (string.IsNullOrWhiteSpace(statusMessage)) {
            return string.Empty;
        }

        const string lastUpdatedPrefix = "Last updated ";
        if (statusMessage.StartsWith(lastUpdatedPrefix, StringComparison.Ordinal)) {
            var time = statusMessage[lastUpdatedPrefix.Length..];
            var format = LocalizationManager.Translate("LastUpdatedFormat");
            if (string.IsNullOrWhiteSpace(format) || string.Equals(format, "LastUpdatedFormat", StringComparison.Ordinal)) {
                var label = LocalizationManager.Translate("LastUpdated");
                format = string.IsNullOrWhiteSpace(label) || string.Equals(label, "LastUpdated", StringComparison.Ordinal)
                    ? "Last updated {0}"
                    : $"{label} {{0}}";
            }

            return string.Format(CultureInfo.CurrentUICulture, format, IsolateLeftToRight(time));
        }

        return statusMessage switch {
            "Updating times..." => LocalizationManager.Translate("UpdatingTimes"),
            "Unable to load prayer times." => LocalizationManager.Translate("UnableToLoadPrayerTimes"),
            "Notifications update failed." => LocalizationManager.Translate("NotificationsUpdateFailed"),
            "Update failed." => LocalizationManager.Translate("UpdateFailed"),
            _ => statusMessage
        };
    }

    private static string LocalizeHijriDate(string value) {
        if (!string.Equals(LocalizationManager.CurrentLanguage, "ar", StringComparison.OrdinalIgnoreCase)) {
            return value;
        }

        return value
            .Replace("Muḥarram", "محرم", StringComparison.OrdinalIgnoreCase)
            .Replace("Safar", "صفر", StringComparison.OrdinalIgnoreCase)
            .Replace("Rabīʿ al-awwal", "ربيع الأول", StringComparison.OrdinalIgnoreCase)
            .Replace("Rabi' al-Awwal", "ربيع الأول", StringComparison.OrdinalIgnoreCase)
            .Replace("Rabīʿ al-thānī", "ربيع الآخر", StringComparison.OrdinalIgnoreCase)
            .Replace("Jumādá al-ūlá", "جمادى الأولى", StringComparison.OrdinalIgnoreCase)
            .Replace("Jumādá al-ākhirah", "جمادى الآخرة", StringComparison.OrdinalIgnoreCase)
            .Replace("Rajab", "رجب", StringComparison.OrdinalIgnoreCase)
            .Replace("Shaʿbān", "شعبان", StringComparison.OrdinalIgnoreCase)
            .Replace("Ramaḍān", "رمضان", StringComparison.OrdinalIgnoreCase)
            .Replace("Shawwāl", "شوال", StringComparison.OrdinalIgnoreCase)
            .Replace("Dhū al-Qaʿdah", "ذو القعدة", StringComparison.OrdinalIgnoreCase)
            .Replace("Dhū al-Ḥijjah", "ذو الحجة", StringComparison.OrdinalIgnoreCase);
    }

    private static string IsolateLeftToRight(string value) {
        return $"\u2066{value}\u2069";
    }

    private static string FormatLiveClock(DateTime time, PrayAdFree.Core.Models.ClockFormat format) {
        return format switch {
            PrayAdFree.Core.Models.ClockFormat.TwelveHour => time.ToString("h:mm:ss tt", CultureInfo.CurrentCulture),
            _ => time.ToString("HH:mm:ss", CultureInfo.CurrentCulture)
        };
    }

    private TodayWebSnapshot? LoadCachedSnapshot() {
        try {
            if (!File.Exists(_snapshotPath)) {
                return null;
            }

            return JsonSerializer.Deserialize<TodayWebSnapshot>(File.ReadAllText(_snapshotPath), JsonOptions);
        } catch (Exception ex) {
            _logger.LogException(ex, "TodayWebRpcHandler.LoadCachedSnapshot");
            return null;
        }
    }

    private void SaveSnapshot(TodayWebSnapshot snapshot) {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(_snapshotPath)!);
            File.WriteAllText(_snapshotPath, JsonSerializer.Serialize(snapshot, JsonOptions));
        } catch (Exception ex) {
            _logger.LogException(ex, "TodayWebRpcHandler.SaveSnapshot");
        }
    }
}

public sealed record TodayWebSnapshot(
    string LocationTitle,
    string HijriDate,
    string GregorianDate,
    string CurrentTime,
    string NextPrayerName,
    string NextPrayerClock,
    string NextPrayerBaseClock,
    bool ShowNextPrayerBaseClock,
    string NextPrayerDayLabel,
    string Countdown,
    string StatusMessage,
    string ImsakTime,
    string IftarTime,
    bool IsImsakNext,
    bool IsIftarNext,
    string NextFastingCountdown,
    bool IsRtl,
    TodayWebLabels Labels,
    IReadOnlyList<TodayWebTiming> TodayTimings);

public sealed record TodayWebLabels(
    string NextPrayer,
    string TimeLeft,
    string TodayPrayerTimes,
    string Iftar,
    string Imsak,
    string Refresh,
    string Refreshing,
    string Base);

public sealed record TodayWebTiming(
    string Id,
    string Name,
    string Time,
    string BaseTime,
    bool ShowBaseTime,
    bool IsNext);
