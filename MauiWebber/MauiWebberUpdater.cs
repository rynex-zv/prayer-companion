using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace MauiWebber;

public sealed class MauiWebberUpdater {
    private const string ActiveSlot = "active";
    private const string PreviousSlot = "previous";
    private const string EmbeddedSlot = "embedded";
    private const string StagingSlot = "staging";
    private const string ManifestFileName = "webber-manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        WriteIndented = true
    };

    private readonly MauiWebberOptions _options;
    private readonly HttpClient _httpClient;
    private readonly IMauiWebberLogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public MauiWebberUpdater(MauiWebberOptions options, HttpClient? httpClient = null, IMauiWebberLogger? logger = null) {
        _options = options;
        _httpClient = httpClient ?? new HttpClient();
        _logger = logger ?? NullMauiWebberLogger.Instance;
    }

    public async Task<string> ResolveStartupFileAsync(CancellationToken cancellationToken = default) {
        var started = DateTime.UtcNow;
        _logger.Log("ResolveStartupFile.Start", _options.AppId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var embeddedManifest = await LoadEmbeddedManifestAsync(cancellationToken).ConfigureAwait(false);
#if DEBUG
            var debugEmbedded = await ResolveEmbeddedStartupUrlAsync(embeddedManifest, cancellationToken).ConfigureAwait(false);
            LogResolve("embedded-debug", debugEmbedded, started);
            return debugEmbedded;
#else
            var active = SlotPath(ActiveSlot);
            if (IsHealthy(active) && IsSlotAtLeastVersion(active, embeddedManifest.Version)) {
                var entry = EntryPath(active);
                LogResolve("active", entry, started);
                return entry;
            }

            var previous = SlotPath(PreviousSlot);
            if (_options.RollbackEnabled && IsHealthy(previous) && IsSlotAtLeastVersion(previous, embeddedManifest.Version)) {
                ReplaceDirectory(active, previous);
                var entry = EntryPath(active);
                LogResolve("previous", entry, started);
                return entry;
            }

            var embedded = await ResolveEmbeddedStartupUrlAsync(embeddedManifest, cancellationToken).ConfigureAwait(false);
            LogResolve("embedded", embedded, started);
            return embedded;
#endif
        } catch (Exception ex) {
            _logger.LogException(ex, "MauiWebber.ResolveStartupFile");
            throw;
        } finally {
            _gate.Release();
        }
    }

    public async Task CheckForUpdatesAsync(CancellationToken cancellationToken = default) {
        if (_options.UpdatePolicy != MauiWebberUpdatePolicy.LocalFirst) {
            return;
        }

        var started = DateTime.UtcNow;
        _logger.Log("UpdateCheck.Start", _options.ManifestUrl.ToString());
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var remoteManifest = await LoadRemoteManifestAsync(cancellationToken).ConfigureAwait(false);
            if (remoteManifest == null || string.IsNullOrWhiteSpace(remoteManifest.Version)) {
                _logger.Log("UpdateCheck.Skip", "remote_manifest_missing");
                return;
            }

            var activeManifest = LoadManifest(SlotPath(ActiveSlot)) ?? await LoadEmbeddedManifestAsync(cancellationToken).ConfigureAwait(false);
            if (string.Equals(activeManifest?.Version, remoteManifest.Version, StringComparison.Ordinal)) {
                _logger.Log("UpdateCheck.Skip", $"same_version:{remoteManifest.Version}");
                return;
            }

            var staging = SlotPath(StagingSlot);
            DeleteDirectory(staging);
            Directory.CreateDirectory(staging);
            await WriteManifestAsync(staging, remoteManifest, cancellationToken).ConfigureAwait(false);

            foreach (var file in remoteManifest.Files.Where(item => !string.IsNullOrWhiteSpace(item.Path))) {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = NormalizeRelativePath(file.Path);
                var targetPath = Path.Combine(staging, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

                var sourceUri = new Uri(_options.RemoteBaseUrl, relativePath.Replace('\\', '/'));
                var bytes = await _httpClient.GetByteArrayAsync(sourceUri, cancellationToken).ConfigureAwait(false);
                if (!IsHashValid(bytes, file.Sha256)) {
                    DeleteDirectory(staging);
                    _logger.Log("UpdateCheck.Fail", $"hash:{relativePath}");
                    return;
                }

                await File.WriteAllBytesAsync(targetPath, bytes, cancellationToken).ConfigureAwait(false);
            }

            if (!IsHealthy(staging)) {
                DeleteDirectory(staging);
                _logger.Log("UpdateCheck.Fail", "staging_unhealthy");
                return;
            }

            var active = SlotPath(ActiveSlot);
            var previous = SlotPath(PreviousSlot);
            if (_options.RollbackEnabled && Directory.Exists(active)) {
                DeleteDirectory(previous);
                Directory.Move(active, previous);
            } else {
                DeleteDirectory(active);
            }

            Directory.Move(staging, active);
            _logger.Log("UpdateCheck.Success", $"version={remoteManifest.Version};ms={(DateTime.UtcNow - started).TotalMilliseconds:F0}");
        } catch (Exception ex) {
            _logger.LogException(ex, "MauiWebber.CheckForUpdates");
            DeleteDirectory(SlotPath(StagingSlot));
        } finally {
            _gate.Release();
        }
    }

    public async Task<string?> ResolveAfterNavigationFailureAsync(string failedUrl, CancellationToken cancellationToken = default) {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var removedSlot = false;
            foreach (var slot in new[] { ActiveSlot, PreviousSlot }) {
                var slotPath = SlotPath(slot);
                if (!Directory.Exists(slotPath)) {
                    continue;
                }

                var entryPath = EntryPath(slotPath);
                var entryUrl = new Uri(entryPath).AbsoluteUri;
                if (!string.Equals(failedUrl, entryUrl, StringComparison.OrdinalIgnoreCase) &&
                    !failedUrl.Contains(slotPath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                DeleteDirectory(slotPath);
                removedSlot = true;
                _logger.Log("NavigationFailure.Rollback", $"slot={slot};url={failedUrl}");
            }

            if (!removedSlot) {
                return null;
            }

            var embedded = await ResolveEmbeddedStartupUrlAsync(cancellationToken).ConfigureAwait(false);
            _logger.Log("NavigationFailure.Fallback", embedded);
            return embedded;
        } catch (Exception ex) {
            _logger.LogException(ex, "MauiWebber.ResolveAfterNavigationFailure");
            return null;
        } finally {
            _gate.Release();
        }
    }

    private void LogResolve(string source, string entry, DateTime started) {
        _logger.Log("ResolveStartupFile.End", $"source={source};ms={(DateTime.UtcNow - started).TotalMilliseconds:F0};entry={entry}");
    }

    private async Task<string> ResolveEmbeddedStartupUrlAsync(CancellationToken cancellationToken) {
        var manifest = await LoadEmbeddedManifestAsync(cancellationToken).ConfigureAwait(false);
        return await ResolveEmbeddedStartupUrlAsync(manifest, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> ResolveEmbeddedStartupUrlAsync(MauiWebberManifest manifest, CancellationToken cancellationToken) {
        var entry = string.IsNullOrWhiteSpace(manifest.Entry) ? _options.StartupFile : manifest.Entry;
        var relativePath = $"{_options.EmbeddedRoot.TrimEnd('/', '\\')}/{entry.TrimStart('/', '\\')}".Replace('\\', '/');
#if ANDROID
        return $"file:///android_asset/{relativePath}";
#else
        await EnsureEmbeddedAsync(manifest, cancellationToken).ConfigureAwait(false);
        return EntryPath(SlotPath(EmbeddedSlot));
#endif
    }

    private async Task EnsureEmbeddedAsync(CancellationToken cancellationToken) {
        var manifest = await LoadEmbeddedManifestAsync(cancellationToken).ConfigureAwait(false);
        await EnsureEmbeddedAsync(manifest, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureEmbeddedAsync(MauiWebberManifest manifest, CancellationToken cancellationToken) {
        var embedded = SlotPath(EmbeddedSlot);
        if (IsHealthy(embedded) && IsSlotSameVersion(embedded, manifest.Version)) {
            return;
        }

        DeleteDirectory(embedded);
        Directory.CreateDirectory(embedded);

        await WriteManifestAsync(embedded, manifest, cancellationToken).ConfigureAwait(false);

        foreach (var file in manifest.Files.Where(item => !string.IsNullOrWhiteSpace(item.Path))) {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = NormalizeRelativePath(file.Path);
            var packagePath = $"{_options.EmbeddedRoot.TrimEnd('/', '\\')}/{relativePath.Replace('\\', '/')}";
            var targetPath = Path.Combine(embedded, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            await using var source = await FileSystem.OpenAppPackageFileAsync(packagePath).ConfigureAwait(false);
            await using var target = File.Create(targetPath);
            await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<MauiWebberManifest> LoadEmbeddedManifestAsync(CancellationToken cancellationToken) {
        var path = $"{_options.EmbeddedRoot.TrimEnd('/', '\\')}/{ManifestFileName}";
        await using var stream = await FileSystem.OpenAppPackageFileAsync(path).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<MauiWebberManifest>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? new MauiWebberManifest { Version = "embedded", Entry = _options.StartupFile };
    }

    private async Task<MauiWebberManifest?> LoadRemoteManifestAsync(CancellationToken cancellationToken) {
        try {
            return await _httpClient.GetFromJsonAsync<MauiWebberManifest>(_options.ManifestUrl, JsonOptions, cancellationToken).ConfigureAwait(false);
        } catch (Exception ex) {
            _logger.LogException(ex, "MauiWebber.LoadRemoteManifest");
            return null;
        }
    }

    private MauiWebberManifest? LoadManifest(string slotPath) {
        try {
            var path = Path.Combine(slotPath, ManifestFileName);
            return File.Exists(path)
                ? JsonSerializer.Deserialize<MauiWebberManifest>(File.ReadAllText(path), JsonOptions)
                : null;
        } catch {
            return null;
        }
    }

    private bool IsHealthy(string slotPath) {
        var manifest = LoadManifest(slotPath);
        var entry = manifest?.Entry;
        if (string.IsNullOrWhiteSpace(entry)) {
            entry = _options.StartupFile;
        }

        return File.Exists(Path.Combine(slotPath, NormalizeRelativePath(entry)));
    }

    private bool IsSlotSameVersion(string slotPath, string? expectedVersion) {
        var manifest = LoadManifest(slotPath);
        return !string.IsNullOrWhiteSpace(manifest?.Version) &&
               string.Equals(manifest.Version, expectedVersion, StringComparison.Ordinal);
    }

    private bool IsSlotAtLeastVersion(string slotPath, string? minimumVersion) {
        if (string.IsNullOrWhiteSpace(minimumVersion)) {
            return true;
        }

        var version = LoadManifest(slotPath)?.Version;
        if (string.IsNullOrWhiteSpace(version)) {
            return false;
        }

        return string.CompareOrdinal(version, minimumVersion) >= 0;
    }

    private string EntryPath(string slotPath) {
        var manifest = LoadManifest(slotPath);
        var entry = string.IsNullOrWhiteSpace(manifest?.Entry) ? _options.StartupFile : manifest!.Entry;
        return Path.Combine(slotPath, NormalizeRelativePath(entry));
    }

    private async Task WriteManifestAsync(string slotPath, MauiWebberManifest manifest, CancellationToken cancellationToken) {
        var path = Path.Combine(slotPath, ManifestFileName);
        Directory.CreateDirectory(slotPath);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken).ConfigureAwait(false);
    }

    private string SlotPath(string slot) {
        return Path.Combine(FileSystem.AppDataDirectory, _options.StorageFolderName, Sanitize(_options.AppId), slot);
    }

    private static bool IsHashValid(byte[] bytes, string? expectedHash) {
        if (string.IsNullOrWhiteSpace(expectedHash)) {
            return true;
        }

        var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return string.Equals(actual, expectedHash.Trim().ToLowerInvariant(), StringComparison.Ordinal);
    }

    private static string NormalizeRelativePath(string path) {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        if (normalized.Contains("..", StringComparison.Ordinal)) {
            throw new InvalidOperationException($"Unsafe web asset path: {path}");
        }

        return normalized.Replace('/', Path.DirectorySeparatorChar);
    }

    private static string Sanitize(string value) {
        return string.Concat(value.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));
    }

    private static void ReplaceDirectory(string target, string source) {
        DeleteDirectory(target);
        CopyDirectory(source, target);
    }

    private static void CopyDirectory(string source, string target) {
        Directory.CreateDirectory(target);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories)) {
            Directory.CreateDirectory(directory.Replace(source, target));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)) {
            File.Copy(file, file.Replace(source, target), overwrite: true);
        }
    }

    private static void DeleteDirectory(string path) {
        if (Directory.Exists(path)) {
            Directory.Delete(path, recursive: true);
        }
    }
}
