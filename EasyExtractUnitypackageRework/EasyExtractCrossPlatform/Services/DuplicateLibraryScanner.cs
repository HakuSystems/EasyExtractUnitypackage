using EasyExtract.Core.Models;
using EasyExtract.Core.Services;

namespace EasyExtractCrossPlatform.Services;

public interface IDuplicateLibraryScanner
{
    Task<DuplicateScanReport> ScanAsync(
        IReadOnlyList<string> packagePaths,
        IProgress<DuplicateScanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    IReadOnlyList<string> FindPackages(string directory, bool includeSubdirectories = true);
}

/// <summary>
///     Scans a whole library of .unitypackage files for duplicated assets, reusing cached
///     fingerprints for files that have not changed since the last run.
/// </summary>
public sealed class DuplicateLibraryScanner : IDuplicateLibraryScanner
{
    public const string PackageSearchPattern = "*.unitypackage";

    private readonly string? _cachePathOverride;
    private readonly IDuplicateAssetScanner _scanner;

    public DuplicateLibraryScanner(IDuplicateAssetScanner scanner, string? cachePathOverride = null)
    {
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        _cachePathOverride = cachePathOverride;
    }

    public async Task<DuplicateScanReport> ScanAsync(
        IReadOnlyList<string> packagePaths,
        IProgress<DuplicateScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packagePaths);

        var targets = Normalize(packagePaths);
        if (targets.Count == 0)
            return DuplicateScanReport.Empty;

        var cache = DuplicateScanCache.Load(_cachePathOverride);
        var results = new List<PackageFingerprintResult>(targets.Count);
        var completed = 0;
        var cacheChanged = false;

        LoggingService.LogInformation($"Duplicate scan started over {targets.Count} package(s).");

        foreach (var packagePath in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new DuplicateScanProgress(completed, targets.Count, Path.GetFileName(packagePath)));

            results.Add(await FingerprintAsync(packagePath, cache, changed => cacheChanged |= changed,
                cancellationToken).ConfigureAwait(false));

            completed++;
        }

        progress?.Report(new DuplicateScanProgress(completed, targets.Count, string.Empty));

        if (cacheChanged)
        {
            cache.PruneMissing();
            cache.Save(_cachePathOverride);
        }

        var report = DuplicateAssetScanner.BuildReport(results);
        LoggingService.LogInformation(
            $"Duplicate scan finished | scanned={report.PackagesScanned} | failed={report.PackagesFailed} | " +
            $"groups={report.Groups.Count} | wasted={report.TotalWastedBytes:N0} bytes");

        return report;
    }

    public IReadOnlyList<string> FindPackages(string directory, bool includeSubdirectories = true)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return Array.Empty<string>();

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = includeSubdirectories,
            // A library folder routinely contains junctions and unreadable system folders; neither
            // should abort the enumeration.
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        try
        {
            return Directory.EnumerateFiles(directory, PackageSearchPattern, options)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            LoggingService.LogWarning($"Could not enumerate packages under '{directory}'.", ex);
            return Array.Empty<string>();
        }
    }

    internal static IReadOnlyList<string> Normalize(IReadOnlyList<string> packagePaths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new List<string>(packagePaths.Count);

        foreach (var path in packagePaths)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                continue;
            }

            if (seen.Add(full))
                normalized.Add(full);
        }

        return normalized;
    }

    private async Task<PackageFingerprintResult> FingerprintAsync(
        string packagePath,
        DuplicateScanCache cache,
        Action<bool> reportCacheChange,
        CancellationToken cancellationToken)
    {
        FileInfo info;
        try
        {
            info = new FileInfo(packagePath);
            if (!info.Exists)
                return new PackageFingerprintResult(packagePath, [], "The package no longer exists.");

            if (cache.TryGet(packagePath, info, out var cached))
                return cached;
        }
        catch (Exception ex)
        {
            return new PackageFingerprintResult(packagePath, [], ex.Message);
        }

        var result = await _scanner.FingerprintPackageAsync(packagePath, cancellationToken).ConfigureAwait(false);
        cache.Store(packagePath, info, result);
        reportCacheChange(true);
        return result;
    }
}
