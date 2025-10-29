using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EasyExtractCrossPlatform.Utilities;

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
        var pendingWrites = new List<PendingAssetWrite>();
        var queuedStates = new HashSet<UnityPackageAssetState>();
        var extractionRequired = false;
        var extractedCount = 0;

        void WriteAndTrack(
            UnityPackageAssetState state,
            string targetPath,
            string? metaPath,
            string? previewPath,
            AssetWritePlan plan)
        {
            var writtenFiles = WriteAssetToDisk(
                state,
                targetPath,
                metaPath,
                previewPath,
                plan,
                cancellationToken);
            if (writtenFiles.Count > 0)
            {
                extractedCount++;
                extractedFiles.AddRange(writtenFiles);
                progress?.Report(new UnityPackageExtractionProgress(state.RelativePath, extractedCount));
            }

            state.MarkAsCompleted();
            queuedStates.Remove(state);
        }

        void FlushPendingWrites()
        {
            foreach (var pending in pendingWrites)
                WriteAndTrack(
                    pending.State,
                    pending.TargetPath,
                    pending.MetaPath,
                    pending.PreviewPath,
                    pending.Plan);

            pendingWrites.Clear();
            queuedStates.Clear();
        }

        void HandleReadyState(UnityPackageAssetState state)
        {
            if (!TryGetAssetPaths(
                    state,
                    outputDirectory,
                    options.OrganizeByCategories,
                    out var targetPath,
                    out var metaPath,
                    out var previewPath))
                return;

            var plan = CreateAssetWritePlan(state, targetPath, metaPath, previewPath);

            if (!plan.RequiresWrite)
            {
                state.MarkAsCompleted();
                queuedStates.Remove(state);
                return;
            }

            if (!extractionRequired)
            {
                extractionRequired = true;
                pendingWrites.Add(new PendingAssetWrite(state, targetPath, metaPath, previewPath, plan));
                queuedStates.Add(state);
                FlushPendingWrites();
            }
            else
            {
                WriteAndTrack(state, targetPath, metaPath, previewPath, plan);
            }
        }

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

            if (state.CanWriteToDisk && !queuedStates.Contains(state))
                HandleReadyState(state);
        }

        // Emit remaining entries that might have been waiting for path or asset information.
        foreach (var (_, state) in assetStates)
        {
            if (!state.CanWriteToDisk || queuedStates.Contains(state))
                continue;

            HandleReadyState(state);
        }

        if (!extractionRequired)
        {
            foreach (var pending in pendingWrites)
                pending.State.MarkAsCompleted();

            pendingWrites.Clear();
            queuedStates.Clear();

            return new UnityPackageExtractionResult(
                packagePath,
                outputDirectory,
                0,
                extractedFiles.ToImmutableArray());
        }

        if (pendingWrites.Count > 0)
            FlushPendingWrites();

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
        if (!TryGetAssetPaths(
                state,
                outputDirectory,
                organizeByCategories,
                out var targetPath,
                out var metaPath,
                out var previewPath))
            return Array.Empty<string>();

        var plan = CreateAssetWritePlan(state, targetPath, metaPath, previewPath);
        if (!plan.RequiresWrite)
            return Array.Empty<string>();

        return WriteAssetToDisk(state, targetPath, metaPath, previewPath, plan, cancellationToken);
    }

    private static bool TryGetAssetPaths(
        UnityPackageAssetState state,
        string outputDirectory,
        bool organizeByCategories,
        out string targetPath,
        out string? metaPath,
        out string? previewPath)
    {
        targetPath = string.Empty;
        metaPath = null;
        previewPath = null;

        if (state.RelativePath is null || state.AssetData is not { Length: > 0 })
            return false;

        var sanitizedPath = organizeByCategories
            ? state.RelativePath
            : Path.GetFileName(state.RelativePath);

        if (string.IsNullOrWhiteSpace(sanitizedPath))
            return false;

        sanitizedPath = NormalizeRelativePath(sanitizedPath);
        if (string.IsNullOrWhiteSpace(sanitizedPath))
            return false;

        targetPath = Path.Combine(outputDirectory, sanitizedPath);
        metaPath = state.MetaData is { Length: > 0 } ? $"{targetPath}.meta" : null;
        previewPath = state.PreviewData is { Length: > 0 } ? $"{targetPath}.preview.png" : null;
        return true;
    }

    private static AssetWritePlan CreateAssetWritePlan(
        UnityPackageAssetState state,
        string targetPath,
        string? metaPath,
        string? previewPath)
    {
        var writeAsset = state.AssetData is { Length: > 0 } && NeedsWrite(targetPath, state.AssetData);
        var writeMeta = metaPath is not null &&
                        state.MetaData is { Length: > 0 } &&
                        NeedsWrite(metaPath, state.MetaData);
        var writePreview = previewPath is not null &&
                           state.PreviewData is { Length: > 0 } &&
                           NeedsWrite(previewPath, state.PreviewData);

        return new AssetWritePlan(writeAsset, writeMeta, writePreview);
    }

    private static IReadOnlyList<string> WriteAssetToDisk(
        UnityPackageAssetState state,
        string targetPath,
        string? metaPath,
        string? previewPath,
        AssetWritePlan plan,
        CancellationToken cancellationToken)
    {
        if (!plan.RequiresWrite)
            return Array.Empty<string>();

        var writtenFiles = new List<string>();
        var directory = Path.GetDirectoryName(targetPath);
        var directoryEnsured = false;

        void EnsureDirectory()
        {
            if (directoryEnsured)
                return;

            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            directoryEnsured = true;
        }

        if (plan.WriteAsset && state.AssetData is { Length: > 0 })
        {
            EnsureDirectory();
            cancellationToken.ThrowIfCancellationRequested();
            File.WriteAllBytes(targetPath, state.AssetData);
            writtenFiles.Add(targetPath);
        }

        if (plan.WriteMeta && metaPath is not null && state.MetaData is { Length: > 0 })
        {
            EnsureDirectory();
            cancellationToken.ThrowIfCancellationRequested();
            File.WriteAllBytes(metaPath, state.MetaData);
            writtenFiles.Add(metaPath);
        }

        if (plan.WritePreview && previewPath is not null && state.PreviewData is { Length: > 0 })
        {
            EnsureDirectory();
            cancellationToken.ThrowIfCancellationRequested();
            File.WriteAllBytes(previewPath, state.PreviewData);
            writtenFiles.Add(previewPath);
        }

        return writtenFiles;
    }

    private static bool NeedsWrite(string path, byte[] data)
    {
        if (!File.Exists(path))
            return true;

        return !ContentMatches(path, data);
    }

    private static bool ContentMatches(string path, byte[] data)
    {
        try
        {
            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists || fileInfo.Length != data.Length)
                return false;

            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var fileHash = SHA256.HashData(stream);
            var dataHash = SHA256.HashData(data);
            return CryptographicOperations.FixedTimeEquals(fileHash, dataHash);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
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

            var normalizedSegment = FileExtensionNormalizer.Normalize(filtered);
            cleanedSegments.Add(normalizedSegment);
        }

        return cleanedSegments.Count == 0
            ? string.Empty
            : Path.Combine(cleanedSegments.ToArray());
    }

    private readonly record struct AssetWritePlan(bool WriteAsset, bool WriteMeta, bool WritePreview)
    {
        public bool RequiresWrite => WriteAsset || WriteMeta || WritePreview;
    }

    private sealed record PendingAssetWrite(
        UnityPackageAssetState State,
        string TargetPath,
        string? MetaPath,
        string? PreviewPath,
        AssetWritePlan Plan);

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