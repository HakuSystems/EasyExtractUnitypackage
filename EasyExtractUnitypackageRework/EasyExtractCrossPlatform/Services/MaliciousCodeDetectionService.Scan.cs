using System.Formats.Tar;
using ICSharpCode.SharpZipLib.GZip;

namespace EasyExtractCrossPlatform.Services;

public sealed partial class MaliciousCodeDetectionService
{
    public Task<MaliciousCodeScanResult> ScanUnityPackageAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        var normalizedPath = NormalizeFullPath(packagePath);
        LoggingService.LogInformation($"MaliciousCodeScan: request | path={normalizedPath}");

        if (!File.Exists(normalizedPath))
        {
            LoggingService.LogWarning($"MaliciousCodeScan: file_missing | path={normalizedPath}");
            throw new FileNotFoundException("Unitypackage file was not found.", normalizedPath);
        }

        if (_scanCache.TryGetValue(normalizedPath, out var cached))
        {
            var age = DateTimeOffset.UtcNow - cached.Timestamp;
            if (age < CacheExpiration)
            {
                LoggingService.LogInformation(
                    $"MaliciousCodeScan: cache_hit | path={normalizedPath} | ageMs={age.TotalMilliseconds:F0}");
                return Task.FromResult(cached.Result);
            }

            LoggingService.LogInformation(
                $"MaliciousCodeScan: cache_expired | path={normalizedPath} | ageMs={age.TotalMilliseconds:F0}");
        }

        var scanTask = _inFlightScans.GetOrAdd(
            normalizedPath,
            _ => RunScanAsync(normalizedPath, cancellationToken));

        LoggingService.LogInformation($"MaliciousCodeScan: awaiting_result | path={normalizedPath}");
        return scanTask;
    }

    private async Task<MaliciousCodeScanResult> RunScanAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        var correlationId = Path.GetFileName(packagePath);
        if (string.IsNullOrWhiteSpace(correlationId))
            correlationId = packagePath;

        var stopwatch = Stopwatch.StartNew();
        LoggingService.LogInformation($"MaliciousCodeScan.Run: start | path={packagePath}");
        using var performanceScope = LoggingService.BeginPerformanceScope(
            "MaliciousCodeScan",
            "Security",
            correlationId);
        var stats = new ScanStatistics();

        try
        {
            var result = await Task.Run(() => ScanUnityPackageCore(packagePath, cancellationToken, stats),
                    cancellationToken)
                .ConfigureAwait(false);
            stopwatch.Stop();
            stats.ThreatCount = result.Threats.Count;
            _scanCache[packagePath] = new CachedScanResult(result, DateTimeOffset.UtcNow);
            LoggingService.LogInformation(
                $"MaliciousCodeScan.Run: success | path={packagePath} | threats={result.Threats.Count} | durationMs={stopwatch.Elapsed.TotalMilliseconds:F0}");
            LoggingService.LogPerformance(
                "MaliciousCodeDetectionService.ScanUnityPackage",
                stopwatch.Elapsed,
                "Security",
                stats.ToPerformanceDetails(),
                stats.TotalBytesAnalyzed);
            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            LoggingService.LogInformation(
                $"MaliciousCodeScan.Run: cancelled | path={packagePath} | durationMs={stopwatch.Elapsed.TotalMilliseconds:F0}");
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LoggingService.LogError(
                $"MaliciousCodeScan.Run: failed | path={packagePath} | durationMs={stopwatch.Elapsed.TotalMilliseconds:F0}",
                ex);
            throw;
        }
        finally
        {
            _inFlightScans.TryRemove(packagePath, out _);
        }
    }

    private static MaliciousCodeScanResult ScanUnityPackageCore(
        string packagePath,
        CancellationToken cancellationToken,
        ScanStatistics stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        var collector = new MaliciousThreatCollector();
        var assetStates = new Dictionary<string, AssetSecurityState>(StringComparer.OrdinalIgnoreCase);

        using var packageStream = File.OpenRead(packagePath);
        using var gzipStream = new GZipInputStream(packageStream);
        using var tarReader = new TarReader(gzipStream);

        TarEntry? entry;
        while ((entry = tarReader.GetNextEntry()) is not null)
        {
            stats.TarEntriesRead++;
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.EntryType == TarEntryType.Directory)
                continue;

            var entryName = entry.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(entryName))
                continue;

            var (assetKey, componentName) = SplitEntryName(entryName);
            if (string.IsNullOrWhiteSpace(assetKey) || string.IsNullOrWhiteSpace(componentName))
                continue;

            if (!assetStates.TryGetValue(assetKey, out var state))
            {
                state = new AssetSecurityState();
                assetStates[assetKey] = state;
            }

            switch (componentName)
            {
                case "pathname" when entry.DataStream is not null:
                {
                    stats.PathComponentsSeen++;
                    var relativePath = ReadPath(entry);
                    state.RelativePath = NormalizeAssetPath(relativePath);
                    if (state.PendingAssetData is { Length: > 0 } pendingData &&
                        !string.IsNullOrWhiteSpace(state.RelativePath))
                    {
                        ProcessAssetData(state.RelativePath, pendingData, collector, stats);
                        state.PendingAssetData = null;
                        assetStates.Remove(assetKey);
                    }

                    break;
                }
                case "asset" when entry.DataStream is not null:
                {
                    stats.AssetComponentsSeen++;
                    var data = ReadAssetData(entry, cancellationToken, stats);
                    if (data is null || data.Length == 0)
                        break;

                    if (!string.IsNullOrWhiteSpace(state.RelativePath))
                    {
                        ProcessAssetData(state.RelativePath, data, collector, stats);
                        assetStates.Remove(assetKey);
                    }
                    else
                    {
                        state.PendingAssetData = data;
                    }

                    break;
                }
            }
        }

        var unresolvedPendingAssets = assetStates.Values.Count(s =>
            s.PendingAssetData is { Length: > 0 } && string.IsNullOrWhiteSpace(s.RelativePath));

        if (unresolvedPendingAssets > 0)
        {
            stats.AssetsSkippedMissingPath += unresolvedPendingAssets;
            LoggingService.LogWarning(
                $"MaliciousCodeScan: skipped assets without pathname | path={packagePath} | count={unresolvedPendingAssets}");
        }

        var threats = collector.BuildResults();
        stats.ThreatCount = threats.Count;
        return new MaliciousCodeScanResult(
            packagePath,
            threats.Count > 0,
            threats,
            DateTimeOffset.UtcNow);
    }
}