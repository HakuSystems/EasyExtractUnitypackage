using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EasyExtractCrossPlatform.Services;

public interface IUnityPackageExtractionService
{
    Task<UnityPackageExtractionResult> ExtractAsync(
        string packagePath,
        string outputDirectory,
        UnityPackageExtractionOptions options,
        IProgress<UnityPackageExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed record UnityPackageExtractionOptions(
    bool OrganizeByCategories,
    string? TemporaryDirectory);

public sealed record UnityPackageExtractionProgress(string? AssetPath, int AssetsExtracted);

public sealed record UnityPackageExtractionResult(
    string PackagePath,
    string OutputDirectory,
    int AssetsExtracted,
    IReadOnlyList<string> ExtractedFiles);

public sealed class UnityPackageExtractionService : IUnityPackageExtractionService
{
    private static readonly HashSet<char> InvalidFileNameCharacters =
        Path.GetInvalidFileNameChars().ToHashSet();

    public async Task<UnityPackageExtractionResult> ExtractAsync(
        string packagePath,
        string outputDirectory,
        UnityPackageExtractionOptions options,
        IProgress<UnityPackageExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        if (!File.Exists(packagePath))
            throw new FileNotFoundException("Unitypackage file was not found.", packagePath);

        Directory.CreateDirectory(outputDirectory);

        if (!string.IsNullOrWhiteSpace(options.TemporaryDirectory))
            Directory.CreateDirectory(options.TemporaryDirectory!);

        return await Task.Run(() =>
                ExtractInternal(packagePath, outputDirectory, options, progress, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private static UnityPackageExtractionResult ExtractInternal(
        string packagePath,
        string outputDirectory,
        UnityPackageExtractionOptions options,
        IProgress<UnityPackageExtractionProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var packageStream = File.OpenRead(packagePath);
        using var gzipStream = new GZipStream(packageStream, CompressionMode.Decompress);
        using var tarReader = new TarReader(gzipStream, false);

        var extractedFiles = new List<string>();
        var assetStates = new Dictionary<string, UnityPackageAssetState>(StringComparer.OrdinalIgnoreCase);
        var extractedCount = 0;

        TarEntry? entry;
        while ((entry = tarReader.GetNextEntry()) is not null)
        {
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
                state = new UnityPackageAssetState();
                assetStates[assetKey] = state;
            }

            if (entry.DataStream is null)
                continue;

            using var memoryStream = new MemoryStream();
            entry.DataStream.CopyTo(memoryStream);
            var data = memoryStream.ToArray();

            switch (componentName)
            {
                case "pathname":
                    state.RelativePath = NormalizeRelativePath(Encoding.UTF8.GetString(data));
                    break;
                case "asset":
                    state.AssetData = data;
                    break;
                case "asset.meta":
                    state.MetaData = data;
                    break;
                case "preview.png":
                    state.PreviewData = data;
                    break;
                default:
                    continue;
            }

            if (state.CanWriteToDisk)
            {
                var writtenFiles = WriteAssetToDisk(
                    state,
                    outputDirectory,
                    options.OrganizeByCategories,
                    cancellationToken);

                if (writtenFiles.Count > 0)
                {
                    extractedCount++;
                    extractedFiles.AddRange(writtenFiles);
                    progress?.Report(new UnityPackageExtractionProgress(state.RelativePath, extractedCount));
                }

                state.MarkAsCompleted();
            }
        }

        // Emit remaining entries that might have been waiting for path or asset information.
        foreach (var (_, state) in assetStates)
        {
            if (!state.CanWriteToDisk)
                continue;

            var writtenFiles = WriteAssetToDisk(
                state,
                outputDirectory,
                options.OrganizeByCategories,
                cancellationToken);

            if (writtenFiles.Count > 0)
            {
                extractedCount++;
                extractedFiles.AddRange(writtenFiles);
                progress?.Report(new UnityPackageExtractionProgress(state.RelativePath, extractedCount));
            }

            state.MarkAsCompleted();
        }

        return new UnityPackageExtractionResult(
            packagePath,
            outputDirectory,
            extractedCount,
            extractedFiles.ToImmutableArray());
    }

    private static IReadOnlyList<string> WriteAssetToDisk(
        UnityPackageAssetState state,
        string outputDirectory,
        bool organizeByCategories,
        CancellationToken cancellationToken)
    {
        if (!state.CanWriteToDisk || state.RelativePath is null)
            return Array.Empty<string>();

        var sanitizedPath = organizeByCategories
            ? state.RelativePath
            : Path.GetFileName(state.RelativePath);

        if (string.IsNullOrWhiteSpace(sanitizedPath) || state.AssetData is null)
            return Array.Empty<string>();

        sanitizedPath = NormalizeRelativePath(sanitizedPath);
        if (string.IsNullOrWhiteSpace(sanitizedPath))
            return Array.Empty<string>();

        var targetPath = Path.Combine(outputDirectory, sanitizedPath);
        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        cancellationToken.ThrowIfCancellationRequested();
        File.WriteAllBytes(targetPath, state.AssetData);

        var writtenFiles = new List<string> { targetPath };

        if (state.MetaData is { Length: > 0 })
        {
            var metaPath = $"{targetPath}.meta";
            File.WriteAllBytes(metaPath, state.MetaData);
            writtenFiles.Add(metaPath);
        }

        if (state.PreviewData is { Length: > 0 })
        {
            var previewPath = $"{targetPath}.preview.png";
            File.WriteAllBytes(previewPath, state.PreviewData);
            writtenFiles.Add(previewPath);
        }

        return writtenFiles;
    }

    private static (string AssetKey, string ComponentName) SplitEntryName(string entryName)
    {
        var normalized = entryName.Replace('\\', '/');
        var firstSlash = normalized.IndexOf('/');
        if (firstSlash < 0)
            return (string.Empty, string.Empty);

        var key = normalized[..firstSlash].Trim();
        var remainder = normalized[(firstSlash + 1)..].Trim();
        return (key, remainder.ToLowerInvariant());
    }

    private static string NormalizeRelativePath(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var sanitized = input.Replace('\\', '/')
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (string.IsNullOrWhiteSpace(sanitized))
            return string.Empty;

        var segments = sanitized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var cleanedSegments = new List<string>();

        foreach (var segment in segments)
        {
            if (segment is "." or "..")
                continue;

            var trimmed = segment.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            var filtered = new string(trimmed.Where(c => !InvalidFileNameCharacters.Contains(c)).ToArray());
            filtered = filtered.Trim();
            if (string.IsNullOrWhiteSpace(filtered))
                continue;

            cleanedSegments.Add(filtered);
        }

        return cleanedSegments.Count == 0
            ? string.Empty
            : Path.Combine(cleanedSegments.ToArray());
    }

    private sealed class UnityPackageAssetState
    {
        public string? RelativePath { get; set; }
        public byte[]? AssetData { get; set; }
        public byte[]? MetaData { get; set; }
        public byte[]? PreviewData { get; set; }
        private bool Completed { get; set; }

        public bool CanWriteToDisk =>
            !Completed &&
            !string.IsNullOrWhiteSpace(RelativePath) &&
            AssetData is { Length: > 0 };

        public void MarkAsCompleted()
        {
            AssetData = null;
            MetaData = null;
            PreviewData = null;
            Completed = true;
        }
    }
}