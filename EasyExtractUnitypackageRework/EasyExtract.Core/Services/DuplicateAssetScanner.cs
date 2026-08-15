using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using EasyExtract.Core;
using EasyExtract.Core.Models;
using EasyExtract.Core.Utilities;

namespace EasyExtract.Core.Services;

public interface IDuplicateAssetScanner
{
    Task<PackageFingerprintResult> FingerprintPackageAsync(
        string packagePath,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Fingerprints the assets inside .unitypackage files so the same asset can be recognised across
///     packages. Assets are hashed by content, not by name, because the same shader or texture is
///     routinely shipped under different paths in different bundles.
/// </summary>
public sealed class DuplicateAssetScanner : IDuplicateAssetScanner
{
    /// <summary>Anything smaller carries no meaningful storage cost and only adds noise.</summary>
    internal const long MinimumTrackedAssetBytes = 1;

    /// <summary>A guard against a crafted archive with millions of entries.</summary>
    internal const int MaxEntriesPerPackage = 400_000;

    private readonly IEasyExtractLogger _logger;

    public DuplicateAssetScanner(IEasyExtractLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PackageFingerprintResult> FingerprintPackageAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        try
        {
            if (!File.Exists(packagePath))
                return Failed(packagePath, "The package no longer exists.");

            var assets = await ReadFingerprintsAsync(packagePath, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                $"Duplicate scan fingerprinted '{Path.GetFileName(packagePath)}' | assets={assets.Count}");
            return new PackageFingerprintResult(packagePath, assets);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A single unreadable package must not abort a scan over the whole library.
            _logger.LogWarning($"Duplicate scan could not read '{packagePath}'.", ex);
            return Failed(packagePath, ex.Message);
        }
    }

    /// <summary>
    ///     Groups fingerprints by content. Pure function so the grouping rules can be tested without
    ///     touching the filesystem.
    /// </summary>
    public static DuplicateScanReport BuildReport(IEnumerable<PackageFingerprintResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var materialized = results.ToList();
        var byHash = new Dictionary<string, List<(DuplicateAssetOccurrence Occurrence, long SizeBytes)>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var result in materialized.Where(result => result.Succeeded))
        foreach (var asset in result.Assets)
        {
            if (!byHash.TryGetValue(asset.ContentHash, out var occurrences))
            {
                occurrences = [];
                byHash[asset.ContentHash] = occurrences;
            }

            occurrences.Add((new DuplicateAssetOccurrence(result.PackagePath, asset.AssetPath), asset.SizeBytes));
        }

        var groups = byHash
            .Where(pair => pair.Value.Count > 1)
            .Select(pair => new DuplicateAssetGroup(
                pair.Key,
                pair.Value[0].SizeBytes,
                pair.Value
                    .Select(entry => entry.Occurrence)
                    .OrderBy(occurrence => occurrence.PackageName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(occurrence => occurrence.AssetPath, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .OrderByDescending(group => group.WastedBytes)
            .ThenBy(group => group.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var failed = materialized
            .Where(result => !result.Succeeded)
            .Select(result => result.PackagePath)
            .ToList();

        return new DuplicateScanReport(
            groups,
            materialized.Count - failed.Count,
            failed.Count,
            failed);
    }

    private static PackageFingerprintResult Failed(string packagePath, string error)
    {
        return new PackageFingerprintResult(packagePath, Array.Empty<PackageAssetFingerprint>(), error);
    }

    private async Task<IReadOnlyList<PackageAssetFingerprint>> ReadFingerprintsAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        await using var packageStream = new FileStream(
            packagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);

        var format = UnityPackageFormatDetector.Detect(packageStream);
        GZipStream? gzipStream = null;
        Stream inputStream;

        switch (format)
        {
            case UnityPackageFormat.GzipTar:
                gzipStream = new GZipStream(packageStream, CompressionMode.Decompress);
                inputStream = gzipStream;
                break;
            case UnityPackageFormat.Tar:
                inputStream = packageStream;
                break;
            default:
                throw new InvalidDataException(
                    $"Not a readable .unitypackage ({UnityPackageFormatDetector.Describe(format)}).");
        }

        try
        {
            // The pathname entry can appear before or after its asset entry, so hashes and names are
            // collected independently and joined once the archive has been walked.
            var hashes = new Dictionary<string, (string Hash, long Size)>(StringComparer.Ordinal);
            var paths = new Dictionary<string, string>(StringComparer.Ordinal);

            using var tarReader = new TarReader(inputStream, true);
            var entriesSeen = 0;

            while (await tarReader.GetNextEntryAsync(false, cancellationToken).ConfigureAwait(false) is { } entry)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (++entriesSeen > MaxEntriesPerPackage)
                    throw new InvalidDataException(
                        $"The package declares more than {MaxEntriesPerPackage:N0} entries.");

                if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                    continue;

                var (assetKey, component) = SplitEntry(entry.Name);
                if (assetKey.Length == 0 || entry.DataStream is not { } data)
                    continue;

                switch (component)
                {
                    case "asset":
                        if (entry.Length < MinimumTrackedAssetBytes)
                            continue;

                        var hash = await SHA256.HashDataAsync(data, cancellationToken).ConfigureAwait(false);
                        hashes[assetKey] = (Convert.ToHexString(hash), entry.Length);
                        break;

                    case "pathname":
                        paths[assetKey] = await ReadPathnameAsync(data, cancellationToken).ConfigureAwait(false);
                        break;
                }
            }

            var fingerprints = new List<PackageAssetFingerprint>(hashes.Count);
            foreach (var (assetKey, entry) in hashes)
            {
                if (!paths.TryGetValue(assetKey, out var assetPath) || string.IsNullOrWhiteSpace(assetPath))
                    continue;

                fingerprints.Add(new PackageAssetFingerprint(assetPath, entry.Hash, entry.Size));
            }

            return fingerprints;
        }
        finally
        {
            if (gzipStream is not null)
                await gzipStream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task<string> ReadPathnameAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var raw = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        // Unity writes the asset path on the first line; later lines carry unrelated metadata.
        var firstLine = raw.Split('\n', 2)[0];
        return firstLine.Replace('\\', '/').Trim();
    }

    internal static (string AssetKey, string Component) SplitEntry(string? entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
            return (string.Empty, string.Empty);

        var normalized = TarEntryNames.StripArchiveRootPrefix(entryName.Replace('\\', '/'));
        var slash = normalized.IndexOf('/');
        if (slash < 0)
            return (string.Empty, string.Empty);

        return (normalized[..slash].Trim(), normalized[(slash + 1)..].Trim().ToLowerInvariant());
    }
}
