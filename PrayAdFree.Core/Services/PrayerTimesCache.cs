using System.Text.Json;
using PrayAdFree.Core.Models;
using System.Collections.Concurrent;

namespace PrayAdFree.Core.Services;

public sealed class PrayerTimesCache {
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> CacheKeyGates = new(StringComparer.Ordinal);
    private static readonly TimeSpan[] RetryDelays = {
        TimeSpan.FromMilliseconds(40),
        TimeSpan.FromMilliseconds(90),
        TimeSpan.FromMilliseconds(180)
    };

    private readonly string _cacheDirectory;
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions {
        WriteIndented = true
    };

    public PrayerTimesCache(string cacheDirectory) {
        _cacheDirectory = cacheDirectory;
        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<PrayerMonth?> TryReadAsync(string cacheKey, CancellationToken cancellationToken) {
        var path = GetPath(cacheKey);
        try {
            return await ExecuteWithRetryAsync(async () => {
                if (!File.Exists(path)) {
                    return null;
                }

                await using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: 4096,
                    options: FileOptions.Asynchronous);
                return await JsonSerializer.DeserializeAsync<PrayerMonth>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        } catch (FileNotFoundException) {
            return null;
        } catch (DirectoryNotFoundException) {
            return null;
        } catch (JsonException) {
            return null;
        }
    }

    public async Task WriteAsync(string cacheKey, PrayerMonth month, CancellationToken cancellationToken) {
        var path = GetPath(cacheKey);
        var gate = CacheKeyGates.GetOrAdd(cacheKey, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            await ExecuteWithRetryAsync(async () => {
                var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try {
                    await using (var stream = new FileStream(
                        tempPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 4096,
                        options: FileOptions.Asynchronous)) {
                        await JsonSerializer.SerializeAsync(stream, month, _jsonOptions, cancellationToken).ConfigureAwait(false);
                        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }

                    if (File.Exists(path)) {
                        File.Replace(tempPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
                    } else {
                        File.Move(tempPath, path);
                    }

                    return true;
                } finally {
                    if (File.Exists(tempPath)) {
                        try {
                            File.Delete(tempPath);
                        } catch {
                        }
                    }
                }
            }, cancellationToken).ConfigureAwait(false);
        } finally {
            gate.Release();
        }
    }

    private string GetPath(string cacheKey) {
        return Path.Combine(_cacheDirectory, $"{cacheKey}.json");
    }

    private static async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken) {
        for (var attempt = 0; ; attempt++) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                return await action().ConfigureAwait(false);
            } catch (IOException) when (attempt < RetryDelays.Length) {
                await Task.Delay(RetryDelays[attempt], cancellationToken).ConfigureAwait(false);
            } catch (UnauthorizedAccessException) when (attempt < RetryDelays.Length) {
                await Task.Delay(RetryDelays[attempt], cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
