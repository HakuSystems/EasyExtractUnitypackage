using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EasyExtractCrossPlatform.Models;
using EasyExtractCrossPlatform.Utilities;

namespace EasyExtractCrossPlatform.Services;

public interface IUnityPackagePreviewService
{
    Task<UnityPackagePreviewResult> LoadPreviewAsync(
        string packagePath,
        CancellationToken cancellationToken = default);
}

public sealed class UnityPackagePreviewService : IUnityPackagePreviewService
{
    private const long MaxEmbeddedAssetBytes = 8 * 1024 * 1024; // 8 MB
    private static readonly HashSet<char> InvalidFileNameCharacters = Path.GetInvalidFileNameChars().ToHashSet();

    private static readonly PathSegmentNormalization[] EmptySegmentNormalizations =
        Array.Empty<PathSegmentNormalization>();

    public async Task<UnityPackagePreviewResult> LoadPreviewAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        if (!File.Exists(packagePath))
            throw new FileNotFoundException("Unitypackage file was not found.", packagePath);

        return await Task.Run(() => LoadPreviewInternal(packagePath, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
    }

    private static UnityPackagePreviewResult LoadPreviewInternal(string packagePath,
        CancellationToken cancellationToken)
    {
        using var packageStream = File.OpenRead(packagePath);
        using var gzipStream = new GZipStream(packageStream, CompressionMode.Decompress);
        using var tarReader = new TarReader(gzipStream, false);

        var assetStates = new Dictionary<string, UnityPackageAssetPreviewState>(StringComparer.OrdinalIgnoreCase);

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
                state = new UnityPackageAssetPreviewState();
                assetStates[assetKey] = state;
            }

            switch (componentName)
            {
                case "pathname" when entry.DataStream is not null:
                    using (var buffer = new MemoryStream())
                    {
                        entry.DataStream.CopyTo(buffer);
                        var path = Encoding.UTF8.GetString(buffer.ToArray());
                        var normalization = NormalizeRelativePath(path);
                        state.RelativePath = normalization.NormalizedPath;
                        state.PathNormalization = normalization;
                    }

                    break;
                case "asset" when entry.DataStream is not null:
                    state.AssetSizeBytes = Math.Max(0, entry.Length);
                    if (entry.Length >= 0 && entry.Length <= MaxEmbeddedAssetBytes)
                    {
                        using var assetBuffer = new MemoryStream();
                        entry.DataStream.CopyTo(assetBuffer);
                        state.AssetData = assetBuffer.ToArray();
                        state.IsAssetDataTruncated = false;
                    }
                    else if (entry.Length < 0)
                    {
                        using var assetBuffer = new MemoryStream();
                        entry.DataStream.CopyTo(assetBuffer);
                        if (assetBuffer.Length <= MaxEmbeddedAssetBytes)
                        {
                            state.AssetData = assetBuffer.ToArray();
                            state.IsAssetDataTruncated = false;
                        }
                        else
                        {
                            state.AssetData = null;
                            state.IsAssetDataTruncated = true;
                        }
                    }
                    else
                    {
                        state.AssetData = null;
                        state.IsAssetDataTruncated = true;
                    }

                    break;
                case "asset.meta":
                    state.HasMetaFile = true;
                    break;
                case "preview.png" when entry.DataStream is not null:
                    using (var memoryStream = new MemoryStream())
                    {
                        entry.DataStream.CopyTo(memoryStream);
                        state.PreviewImageData = memoryStream.ToArray();
                    }

                    break;
            }
        }

        var assets = new List<UnityPackagePreviewAsset>(assetStates.Count);
        var directoriesToPrune = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalAssetSize = 0;

        foreach (var state in assetStates.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(state.RelativePath))
                continue;

            TrackCorruptedDirectories(state, directoriesToPrune);

            var assetSize = state.AssetSizeBytes;
            totalAssetSize += assetSize;

            assets.Add(new UnityPackagePreviewAsset(
                state.RelativePath!,
                assetSize,
                state.HasMetaFile,
                state.PreviewImageData,
                state.AssetData,
                state.IsAssetDataTruncated));
        }

        assets.Sort(static (left, right) =>
            string.Compare(left.RelativePath, right.RelativePath, StringComparison.OrdinalIgnoreCase));

        var fileInfo = new FileInfo(packagePath);
        var packageSizeBytes = fileInfo.Exists ? fileInfo.Length : 0;
        DateTimeOffset? lastModifiedUtc = fileInfo.Exists
            ? new DateTimeOffset(fileInfo.LastWriteTimeUtc)
            : null;

        return new UnityPackagePreviewResult(
            packagePath,
            fileInfo.Name,
            packageSizeBytes,
            lastModifiedUtc,
            totalAssetSize,
            assets,
            directoriesToPrune);
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

    private static PathNormalizationResult NormalizeRelativePath(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new PathNormalizationResult(string.Empty, string.Empty, EmptySegmentNormalizations);

        var sanitized = input.Replace('\\', '/')
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (string.IsNullOrWhiteSpace(sanitized))
            return new PathNormalizationResult(string.Empty, string.Empty, EmptySegmentNormalizations);

        var segments = sanitized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return new PathNormalizationResult(string.Empty, string.Empty, EmptySegmentNormalizations);

        var originalSegments = new List<string>(segments.Length);
        var normalizedSegments = new List<string>(segments.Length);

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

            originalSegments.Add(filtered);
            var normalizedSegment = FileExtensionNormalizer.Normalize(filtered);
            normalizedSegments.Add(normalizedSegment);
        }

        if (originalSegments.Count == 0)
            return new PathNormalizationResult(string.Empty, string.Empty, EmptySegmentNormalizations);

        var segmentPairs = new PathSegmentNormalization[originalSegments.Count];
        for (var i = 0; i < segmentPairs.Length; i++)
            segmentPairs[i] = new PathSegmentNormalization(originalSegments[i], normalizedSegments[i]);

        var originalPath = Path.Combine(originalSegments.ToArray());
        var normalizedPath = Path.Combine(normalizedSegments.ToArray());

        return new PathNormalizationResult(normalizedPath, originalPath, segmentPairs);
    }

    private static void TrackCorruptedDirectories(
        UnityPackageAssetPreviewState state,
        HashSet<string> directoriesToPrune)
    {
        if (state.PathNormalization is null)
            return;

        var normalization = state.PathNormalization.Value;
        var segments = normalization.Segments;
        if (segments.Count == 0)
            return;

        var builder = new StringBuilder();
        for (var i = 0; i < segments.Count - 1; i++)
        {
            if (builder.Length > 0)
                builder.Append('/');

            builder.Append(segments[i].Normalized);

            if (string.Equals(segments[i].Original, segments[i].Normalized, StringComparison.Ordinal))
                continue;

            directoriesToPrune.Add(builder.ToString());
        }
    }

    private readonly record struct PathSegmentNormalization(string Original, string Normalized);

    private readonly record struct PathNormalizationResult(
        string NormalizedPath,
        string OriginalPath,
        IReadOnlyList<PathSegmentNormalization> Segments);

    private sealed class UnityPackageAssetPreviewState
    {
        public string? RelativePath { get; set; }
        public PathNormalizationResult? PathNormalization { get; set; }
        public long AssetSizeBytes { get; set; }
        public bool HasMetaFile { get; set; }
        public byte[]? PreviewImageData { get; set; }
        public byte[]? AssetData { get; set; }
        public bool IsAssetDataTruncated { get; set; }
    }
}