using System.Globalization;
using System.Text.Json;
using MauiWebber;
using PrayAdFree.Core.Services;
using PrayAdFree.Core.Models;

namespace Pray_Ad_Free.Services;

public sealed class TodayWebRpcHandler : IMauiWebberRpcHandler {
    private const int SnapshotCacheSchemaVersion = 2;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        WriteIndented = true
    };

    private readonly ITodayProjectionSource _source;
    private readonly IAppLogger _logger;
    private readonly ISettingsRepository _settings;
    private readonly object _sync = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly string _snapshotPath;
    private Task? _preloadTask;
    private TodayWebSnapshot? _lastSnapshot;
    private bool _backgroundRefreshRunning;
    private bool _cacheWarmupStarted;

    public TodayWebRpcHandler(ITodayProjectionSource source, ISettingsRepository settings, IAppLogger logger) {
        _source = source;
        _settings = settings;
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
            if (!string.IsNullOrWhiteSpace(_source.LocationTitle)) {
                _source.UpdateCountdown(DateTime.Now);
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
        await _refreshGate.WaitAsync().ConfigureAwait(false);
        try {
            if (force || string.IsNullOrWhiteSpace(_source.LocationTitle)) {
                await _source.RefreshAsync().ConfigureAwait(false);
            }

            _source.UpdateCountdown(DateTime.Now);
            var snapshot = BuildSnapshot();
            _lastSnapshot = snapshot;
            SaveSnapshot(snapshot);
            return snapshot;
        } finally {
            _refreshGate.Release();
        }
    }

    private TodayWebSnapshot BuildSnapshot() {
        var isRtl = string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "ar", StringComparison.OrdinalIgnoreCase);

        return new TodayWebSnapshot(
            LocationTitle: _source.LocationTitle,
            HijriDate: LocalizeHijriDate(_source.HijriDate),
            GregorianDate: _source.GregorianDate,
            CurrentTime: FormatLiveClock(DateTime.Now, _source.CurrentClockFormat),
            NextPrayerId: _source.NextPrayerId.ToString(),
            NextPrayerClock: _source.NextPrayerClock,
            NextPrayerBaseClock: _source.NextPrayerBaseClock,
            ShowNextPrayerBaseClock: _source.ShowNextPrayerBaseClock,
            NextPrayerDayId: _source.NextPrayerDayId,
            Countdown: _source.Countdown,
            StatusMessage: BuildStatusMessage(_source.StatusMessage),
            ImsakTime: _source.ImsakTime,
            IftarTime: _source.IftarTime,
            IsImsakNext: _source.IsImsakNext,
            IsIftarNext: _source.IsIftarNext,
            NextFastingCountdown: _source.NextFastingCountdown,
            IsRtl: isRtl,
            Labels: WebCatalog.Labels(LocalizationManager.CurrentLanguage),
            TodayTimings: _source.TodayTimings.Select(row => new TodayWebTiming(
                Id: row.Id.ToString(),
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

            var envelope = JsonSerializer.Deserialize<TodaySnapshotCacheEnvelope>(File.ReadAllText(_snapshotPath), JsonOptions);
            if (envelope?.SchemaVersion != SnapshotCacheSchemaVersion ||
                !string.Equals(envelope.InputKey, BuildSnapshotInputKey(), StringComparison.Ordinal)) {
                TryDeleteSnapshotCache();
                return null;
            }
            return envelope.Snapshot;
        } catch (Exception ex) {
            _logger.LogException(ex, "TodayWebRpcHandler.LoadCachedSnapshot");
            TryDeleteSnapshotCache();
            return null;
        }
    }

    private void SaveSnapshot(TodayWebSnapshot snapshot) {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(_snapshotPath)!);
            var envelope = new TodaySnapshotCacheEnvelope(SnapshotCacheSchemaVersion, BuildSnapshotInputKey(), snapshot);
            var tempPath = _snapshotPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(envelope, JsonOptions));
            File.Move(tempPath, _snapshotPath, overwrite: true);
        } catch (Exception ex) {
            _logger.LogException(ex, "TodayWebRpcHandler.SaveSnapshot");
        }
    }

    private string BuildSnapshotInputKey() {
        var settings = _settings.Load();
        var prayerInput = PrayerTimesService.BuildCacheKey(settings, DateTime.Today.Year, DateTime.Today.Month);
        return string.Join('|',
            $"date:{DateOnly.FromDateTime(DateTime.Today):yyyy-MM-dd}",
            $"prayer:{prayerInput}",
            $"language:{LocalizationManager.CurrentLanguage}",
            $"clock:{settings.ClockFormat}");
    }

    private void TryDeleteSnapshotCache() {
        try { if (File.Exists(_snapshotPath)) File.Delete(_snapshotPath); } catch { }
    }
}

public sealed record TodaySnapshotCacheEnvelope(int SchemaVersion, string InputKey, TodayWebSnapshot Snapshot);

public sealed record TodayWebSnapshot(
    string LocationTitle,
    string HijriDate,
    string GregorianDate,
    string CurrentTime,
    string NextPrayerId,
    string NextPrayerClock,
    string NextPrayerBaseClock,
    bool ShowNextPrayerBaseClock,
    string NextPrayerDayId,
    string Countdown,
    string StatusMessage,
    string ImsakTime,
    string IftarTime,
    bool IsImsakNext,
    bool IsIftarNext,
    string NextFastingCountdown,
    bool IsRtl,
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyList<TodayWebTiming> TodayTimings);

public sealed record TodayWebTiming(
    string Id,
    string Time,
    string BaseTime,
    bool ShowBaseTime,
    bool IsNext);
