using System.Formats.Tar;
using System.IO.Compression;
using EasyExtract.Core.Models;

namespace EasyExtract.Core.Services;

public sealed partial class UnityPackageExtractionService
{
    public async Task<UnityPackageExtractionResult> ExtractInfoAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        var correlationId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation($"ExtractInfoAsync started | package='{packagePath}' | correlationId={correlationId}");

        if (!File.Exists(packagePath))
        {
            _logger.LogError(
                $"ExtractInfoAsync aborted: File not found | path='{packagePath}' | correlationId={correlationId}");
            throw new FileNotFoundException("Unitypackage file was not found.", packagePath);
        }

        await using var packageStream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            4096, FileOptions.Asynchronous);
        var format = UnityPackageFormatDetector.Detect(packageStream);

        Stream inputStream = packageStream;
        GZipStream? gzipStream = null;

        switch (format)
        {
            case UnityPackageFormat.GzipTar:
                gzipStream = new GZipStream(packageStream, CompressionMode.Decompress);
                inputStream = gzipStream;
                break;
            case UnityPackageFormat.Tar:
                inputStream = packageStream;
                break;
            case UnityPackageFormat.TooSmall:
                throw new InvalidDataException("The selected file is too small to be a valid .unitypackage.");
            default:
                throw CreateInvalidFormatException(format, packagePath, correlationId);
        }

        try
        {
            using var tarReader = new TarReader(inputStream, true);
            var result = await ScanPackageAsync(tarReader, packagePath, cancellationToken, correlationId);
            _logger.LogInformation(
                $"ExtractInfoAsync completed | files={result.ExtractedFiles.Count} | size={result.TotalSize:N0} | correlationId={correlationId}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError($"ExtractInfoAsync failed | package='{packagePath}' | correlationId={correlationId}", ex);
            throw;
        }
        finally
        {
            if (gzipStream != null) await gzipStream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<UnityPackageExtractionResult> ScanPackageAsync(
        TarReader tarReader,
        string packagePath,
        CancellationToken cancellationToken,
        string correlationId)
    {
        var assetPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var assetSizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var extractedFiles = new List<string>();
        long totalSize = 0;
        var entriesProcessed = 0;

        TarEntry? entry;
        while ((entry = await tarReader.GetNextEntryAsync(false, cancellationToken)) != null)
        {
            entriesProcessed++;
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                continue;

            var entryName = entry.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(entryName)) continue;

            var (assetKey, componentName) = SplitEntryName(entryName);
            if (string.IsNullOrWhiteSpace(assetKey)) continue;

            switch (componentName)
            {
                case "pathname":
                    var rawPath =
                        await ReadEntryAsUtf8StringAsync(entry.DataStream ?? Stream.Null, cancellationToken,
                            correlationId, _logger).ConfigureAwait(false);
                    // Normalize just to be safe and clean, but keep structure
                    // We reuse NormalizeRelativePath but without caring about strict safety for Info, 
                    // though it cleans up slashes etc.
                    var normalization = NormalizeRelativePath(rawPath, correlationId, _logger);
                    if (!string.IsNullOrWhiteSpace(normalization.NormalizedPath))
                        assetPaths[assetKey] = normalization.NormalizedPath;
                    break;

                case "asset":
                    var size = entry.Length;
                    assetSizes[assetKey] = size;
                    totalSize += size;
                    break;
            }
        }

        // Compile file list
        // Only include assets that have a path. 
        // We do NOT check for content existence (size > 0), because even 0-byte files are files.
        // But usually we respect pairs.
        foreach (var kvp in assetPaths)
        {
            var guid = kvp.Key;
            var path = kvp.Value;

            // If we have a path, we consider it a file in the package.
            extractedFiles.Add(path);
        }

        return new UnityPackageExtractionResult(
            packagePath,
            string.Empty, // No output directory
            0, // No assets extracted to disk
            extractedFiles)
        {
            TotalSize = totalSize
        };
    }
}