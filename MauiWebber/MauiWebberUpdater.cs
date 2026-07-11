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
    private const string UseRemoteSlotPreferencePrefix = "MauiWebber.UseRemoteSlot.";
    private const string RemoteBaseUrlPreferencePrefix = "MauiWebber.RemoteBaseUrl.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        WriteIndented = true
    };

    private readonly MauiWebberOptions _options;
    private readonly HttpClient _httpClient;
    private readonly IMauiWebberLogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _embeddedVersion;

    public MauiWebberUpdater(MauiWebberOptions options, HttpClient? httpClient = null, IMauiWebberLogger? logger = null) {
        _options = options;
        _httpClient = httpClient ?? new HttpClient();
        _logger = logger ?? NullMauiWebberLogger.Instance;
    }

    public MauiWebberOptions Options => _options;

    public Uri RemoteBaseUrl => LoadRemoteBaseUrl();

    public Uri ManifestUrl => new(RemoteBaseUrl, ManifestFileName);

    public Uri SetRemoteBaseUrl(string? url) {
        var normalized = NormalizeRemoteBaseUrl(url);
        Preferences.Set(RemoteBaseUrlPreferenceKey(), normalized.AbsoluteUri);
        _logger.Log("RemoteBaseUrl.Set", normalized.AbsoluteUri);
        return normalized;
    }

    public void ResetRemoteBaseUrl() {
        Preferences.Remove(RemoteBaseUrlPreferenceKey());
        _logger.Log("RemoteBaseUrl.Reset", _options.RemoteBaseUrl.AbsoluteUri);
    }

    public async Task<string> ResolveStartupFileAsync(CancellationToken cancellationToken = default) {
        var started = DateTime.UtcNow;
        _logger.Log("ResolveStartupFile.Start", _options.AppId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var embeddedManifest = await LoadEmbeddedManifestAsync(cancellationToken).ConfigureAwait(false);
#if DEBUG
            if (Preferences.Get(UseRemoteSlotPreferenceKey(), false)) {
                var active = SlotPath(ActiveSlot);
                if (IsHealthy(active) && IsSlotAtLeastVersion(active, embeddedManifest.Version)) {
                    var entry = EntryPath(active);
                    LogResolve("active-debug", entry, started);
                    return entry;
                }
            }

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

    public async Task<MauiWebberUpdateResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default) {
        if (_options.UpdatePolicy != MauiWebberUpdatePolicy.LocalFirst) {
            return MauiWebberUpdateResult.Skipped(CurrentInstalledVersion());
        }

        if (!await _gate.WaitAsync(_options.UpdateGateWaitTimeout, cancellationToken).ConfigureAwait(false)) {
            _logger.Log("UpdateCheck.Skip", "busy");
            return MauiWebberUpdateResult.Failed("Another web update check is already running. Try again in a few seconds.", CurrentInstalledVersion());
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.UpdateCheckTimeout);
        var token = timeout.Token;
        var started = DateTime.UtcNow;
        var manifestUrl = ManifestUrl;
        _logger.Log("UpdateCheck.Start", manifestUrl.ToString());
        try {
            var remoteManifest = await LoadRemoteManifestAsync(manifestUrl, token).ConfigureAwait(false);
            if (remoteManifest == null || string.IsNullOrWhiteSpace(remoteManifest.Version)) {
                _logger.Log("UpdateCheck.Skip", "remote_manifest_missing");
                return MauiWebberUpdateResult.Failed("Remote web manifest is missing or invalid.", CurrentInstalledVersion());
            }

            if (_options.RequiredContractVersion > 0 &&
                remoteManifest.ContractVersion != _options.RequiredContractVersion) {
                _logger.Log("UpdateCheck.Skip", $"contract_mismatch:required={_options.RequiredContractVersion};remote={remoteManifest.ContractVersion}");
                return MauiWebberUpdateResult.Failed(
                    $"Web update contract is incompatible (required {_options.RequiredContractVersion}, received {remoteManifest.ContractVersion}).",
                    CurrentInstalledVersion());
            }

            var embeddedManifest = await LoadEmbeddedManifestAsync(token).ConfigureAwait(false);
            var activeManifest = LoadManifest(SlotPath(ActiveSlot));
            var installedManifest = activeManifest != null &&
                                    !string.IsNullOrWhiteSpace(activeManifest.Version) &&
                                    CompareVersions(activeManifest.Version, embeddedManifest.Version) >= 0
                ? activeManifest
                : embeddedManifest;
            if (string.Equals(installedManifest.Version, remoteManifest.Version, StringComparison.Ordinal)) {
                _logger.Log("UpdateCheck.Skip", $"same_version:{remoteManifest.Version}");
                return MauiWebberUpdateResult.SameVersion(remoteManifest.Version);
            }

            if (CompareVersions(remoteManifest.Version, installedManifest.Version) < 0) {
                _logger.Log("UpdateCheck.Skip", $"remote_older:remote={remoteManifest.Version};installed={installedManifest.Version}");
                return MauiWebberUpdateResult.Failed(
                    $"Remote web version {remoteManifest.Version} is older than installed version {installedManifest.Version}.",
                    installedManifest.Version);
            }

            var staging = SlotPath(StagingSlot);
            DeleteDirectory(staging);
            Directory.CreateDirectory(staging);
            await WriteManifestAsync(staging, remoteManifest, token).ConfigureAwait(false);

            foreach (var file in remoteManifest.Files.Where(item => !string.IsNullOrWhiteSpace(item.Path))) {
                token.ThrowIfCancellationRequested();
                var relativePath = NormalizeRelativePath(file.Path);
                var targetPath = Path.Combine(staging, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

                var sourceUri = new Uri(RemoteBaseUrl, relativePath.Replace('\\', '/'));
                byte[] bytes;
                try {
                    bytes = await _httpClient.GetByteArrayAsync(sourceUri, token).ConfigureAwait(false);
                } catch (HttpRequestException) when (IsOptionalRemoteFile(relativePath)) {
                    _logger.Log("UpdateCheck.OptionalFileMissing", relativePath);
                    continue;
                }

                if (!IsHashValid(bytes, file.Sha256)) {
                    DeleteDirectory(staging);
                    _logger.Log("UpdateCheck.Fail", $"hash:{relativePath}");
                    return MauiWebberUpdateResult.Failed($"Downloaded web asset failed validation: {relativePath}", CurrentInstalledVersion());
                }

                await File.WriteAllBytesAsync(targetPath, bytes, token).ConfigureAwait(false);
            }

            if (!IsHealthy(staging)) {
                DeleteDirectory(staging);
                _logger.Log("UpdateCheck.Fail", "staging_unhealthy");
                return MauiWebberUpdateResult.Failed("Downloaded web bundle failed health checks.", CurrentInstalledVersion());
            }

            RewriteHtmlForLocalSlot(staging, remoteManifest);

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
            return MauiWebberUpdateResult.Updated(remoteManifest.Version);
        } catch (Exception ex) {
            _logger.LogException(ex, "MauiWebber.CheckForUpdates");
            DeleteDirectory(SlotPath(StagingSlot));
            return MauiWebberUpdateResult.Failed(
                ex is OperationCanceledException && !cancellationToken.IsCancellationRequested
                    ? "Web update timed out. Check the network and try again."
                    : CleanError(ex),
                CurrentInstalledVersion());
        } finally {
            _gate.Release();
        }
    }

    public async Task<MauiWebberUpdateResult> PullRemoteAndActivateAsync(CancellationToken cancellationToken = default) {
        var result = await CheckForUpdatesAsync(cancellationToken).ConfigureAwait(false);
        if (string.Equals(result.Status, "error", StringComparison.Ordinal)) {
            return result;
        }

        var active = SlotPath(ActiveSlot);
        if (!IsHealthy(active)) {
            if (string.Equals(result.Status, "same", StringComparison.Ordinal)) {
                var embedded = SlotPath(EmbeddedSlot);
                if (!IsHealthy(embedded)) {
                    var embeddedManifest = await LoadEmbeddedManifestAsync(cancellationToken).ConfigureAwait(false);
                    await EnsureEmbeddedAsync(embeddedManifest, cancellationToken).ConfigureAwait(false);
                }

                if (IsHealthy(embedded) && IsSlotSameVersion(embedded, result.Version)) {
                    ReplaceDirectory(active, embedded);
                    Preferences.Set(UseRemoteSlotPreferenceKey(), true);
                    var activeEntry = EntryPath(active);
                    _logger.Log("RemoteSlot.ActivatedFromEmbedded", activeEntry);
                    return result with {
                        StartupFile = activeEntry
                    };
                }

                return result with {
                    Error = "Same web version is installed, but no healthy active web slot is available."
                };
            }

            var fallbackVersion = result.Version ?? CurrentInstalledVersion();
            return MauiWebberUpdateResult.Failed(result.Error ?? "Remote web bundle is not available or failed health checks.", fallbackVersion);
        }

        Preferences.Set(UseRemoteSlotPreferenceKey(), true);
        var entry = EntryPath(active);
        _logger.Log("RemoteSlot.Activated", entry);
        return result with {
            Version = LoadManifest(active)?.Version ?? result.Version,
            StartupFile = entry
        };
    }

    public void UseEmbeddedOnNextStartup() {
        Preferences.Set(UseRemoteSlotPreferenceKey(), false);
        _logger.Log("RemoteSlot.Disabled", _options.AppId);
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

            var active = SlotPath(ActiveSlot);
            var previous = SlotPath(PreviousSlot);
            if (_options.RollbackEnabled && IsHealthy(previous)) {
                ReplaceDirectory(active, previous);
                Preferences.Set(UseRemoteSlotPreferenceKey(), true);
                var previousEntry = EntryPath(active);
                _logger.Log("NavigationFailure.PreviousFallback", previousEntry);
                return previousEntry;
            }

            Preferences.Set(UseRemoteSlotPreferenceKey(), false);
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
        var manifest = await JsonSerializer.DeserializeAsync<MauiWebberManifest>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? new MauiWebberManifest { Version = "embedded", Entry = _options.StartupFile };
        _embeddedVersion = manifest.Version;
        return manifest;
    }

    private async Task<MauiWebberManifest?> LoadRemoteManifestAsync(Uri manifestUrl, CancellationToken cancellationToken) {
        try {
            return await _httpClient.GetFromJsonAsync<MauiWebberManifest>(manifestUrl, JsonOptions, cancellationToken).ConfigureAwait(false);
        } catch (Exception ex) {
            _logger.LogException(ex, "MauiWebber.LoadRemoteManifest");
            return null;
        }
    }

    private Uri LoadRemoteBaseUrl() {
        var saved = Preferences.Get(RemoteBaseUrlPreferenceKey(), string.Empty);
        if (Uri.TryCreate(saved, UriKind.Absolute, out var uri)) {
            return NormalizeRemoteBaseUrl(uri.AbsoluteUri);
        }

        return NormalizeRemoteBaseUrl(_options.RemoteBaseUrl.AbsoluteUri);
    }

    private Uri NormalizeRemoteBaseUrl(string? url) {
        if (string.IsNullOrWhiteSpace(url)) {
            throw new InvalidOperationException("Remote web URL is empty.");
        }

        var trimmed = url.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) {
            throw new InvalidOperationException("Remote web URL must start with http:// or https://.");
        }

        var builder = new UriBuilder(uri);
        if (builder.Path.EndsWith(ManifestFileName, StringComparison.OrdinalIgnoreCase)) {
            builder.Path = builder.Path[..^ManifestFileName.Length];
        }

        if (!builder.Path.EndsWith("/", StringComparison.Ordinal)) {
            builder.Path += "/";
        }

        builder.Query = string.Empty;
        builder.Fragment = string.Empty;
        return builder.Uri;
    }

    private string? CurrentInstalledVersion() {
        string? installed = null;
        foreach (var candidate in new[] {
                     LoadManifest(SlotPath(ActiveSlot))?.Version,
                     LoadManifest(SlotPath(EmbeddedSlot))?.Version,
                     _embeddedVersion
                 }) {
            if (!string.IsNullOrWhiteSpace(candidate) &&
                (installed == null || CompareVersions(candidate, installed) > 0)) {
                installed = candidate;
            }
        }

        return installed;
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
        if (_options.RequiredContractVersion > 0 &&
            manifest?.ContractVersion != _options.RequiredContractVersion) {
            return false;
        }

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

        return CompareVersions(version, minimumVersion) >= 0;
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

    private string RemoteBaseUrlPreferenceKey() => $"{RemoteBaseUrlPreferencePrefix}{_options.AppId}";

    private string UseRemoteSlotPreferenceKey() {
        return $"{UseRemoteSlotPreferencePrefix}{Sanitize(_options.AppId)}";
    }

    private static bool IsHashValid(byte[] bytes, string? expectedHash) {
        if (string.IsNullOrWhiteSpace(expectedHash)) {
            return true;
        }

        var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return string.Equals(actual, expectedHash.Trim().ToLowerInvariant(), StringComparison.Ordinal);
    }

    private static bool IsOptionalRemoteFile(string relativePath) {
        return string.Equals(
            relativePath.Replace('\\', '/'),
            "version.web.info",
            StringComparison.OrdinalIgnoreCase);
    }

    private void RewriteHtmlForLocalSlot(string slotPath, MauiWebberManifest manifest) {
        var entry = string.IsNullOrWhiteSpace(manifest.Entry) ? _options.StartupFile : manifest.Entry;
        var entryPath = Path.Combine(slotPath, NormalizeRelativePath(entry));
        if (!File.Exists(entryPath)) {
            return;
        }

        var html = File.ReadAllText(entryPath);
        var rewritten = html
            .Replace("src=\"/", "src=\"", StringComparison.Ordinal)
            .Replace("href=\"/", "href=\"", StringComparison.Ordinal)
            .Replace("src='/", "src='", StringComparison.Ordinal)
            .Replace("href='/", "href='", StringComparison.Ordinal);

        if (string.Equals(html, rewritten, StringComparison.Ordinal)) {
            return;
        }

        File.WriteAllText(entryPath, rewritten);
        _logger.Log("UpdateCheck.RewriteHtmlForLocalSlot", entry);
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

    private static int CompareVersions(string left, string right) {
        return long.TryParse(left, out var leftNumber) && long.TryParse(right, out var rightNumber)
            ? leftNumber.CompareTo(rightNumber)
            : string.CompareOrdinal(left, right);
    }

    private static string CleanError(Exception ex) {
        return ex switch {
            OperationCanceledException => "Web update was cancelled.",
            HttpRequestException => "Could not download the web update. Check the network and try again.",
            JsonException => "Remote web manifest is not valid JSON.",
            IOException => "Could not write the downloaded web update.",
            _ => string.IsNullOrWhiteSpace(ex.Message) ? "Web update failed." : ex.Message
        };
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

public sealed record MauiWebberUpdateResult(
    string Status,
    string? Version,
    string? StartupFile,
    string? Error) {
    public static MauiWebberUpdateResult Updated(string version) {
        return new("updated", version, null, null);
    }

    public static MauiWebberUpdateResult SameVersion(string version) {
        return new("same", version, null, null);
    }

    public static MauiWebberUpdateResult Skipped(string? version) {
        return new("skipped", version, null, null);
    }

    public static MauiWebberUpdateResult Failed(string error, string? version) {
        return new("error", version, null, error);
    }
}
