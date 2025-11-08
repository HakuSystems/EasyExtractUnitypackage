using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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
    private const int StreamCopyBufferSize = 128 * 1024;

    private static readonly HashSet<char> InvalidFileNameCharacters =
        Path.GetInvalidFileNameChars().ToHashSet();

    private static readonly PathSegmentNormalization[] EmptySegmentNormalizations =
        Array.Empty<PathSegmentNormalization>();

    public async Task<UnityPackageExtractionResult> ExtractAsync(
        string packagePath,
        string outputDirectory,
        UnityPackageExtractionOptions options,
        IProgress<UnityPackageExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        LoggingService.LogInformation(
            $"Unity package extraction requested. package='{packagePath}', output='{outputDirectory}', organize={options.OrganizeByCategories}, temp='{options.TemporaryDirectory}'.");

        if (!File.Exists(packagePath))
        {
            LoggingService.LogError($"Unity package extraction aborted. File not found: '{packagePath}'.");
            throw new FileNotFoundException("Unitypackage file was not found.", packagePath);
        }

        Directory.CreateDirectory(outputDirectory);
        LoggingService.LogInformation($"Ensured output directory '{outputDirectory}' exists.");

        if (!string.IsNullOrWhiteSpace(options.TemporaryDirectory))
        {
            Directory.CreateDirectory(options.TemporaryDirectory!);
            LoggingService.LogInformation($"Ensured temporary directory '{options.TemporaryDirectory}' exists.");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await Task.Run(() =>
                    ExtractInternal(packagePath, outputDirectory, options, progress, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            LoggingService.LogInformation(
                $"Unity package extraction completed in {stopwatch.Elapsed.TotalMilliseconds:F0} ms. Extracted {result.AssetsExtracted} assets to '{result.OutputDirectory}'.");
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            LoggingService.LogError($"Unity package extraction failed for '{packagePath}'.", ex);
            throw;
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    private static UnityPackageExtractionResult ExtractInternal(
        string packagePath,
        string outputDirectory,
        UnityPackageExtractionOptions options,
        IProgress<UnityPackageExtractionProgress>? progress,
        CancellationToken cancellationToken)
    {
        LoggingService.LogInformation($"Beginning low-level extraction flow for '{packagePath}'.");

        using var packageStream = File.OpenRead(packagePath);
        using var gzipStream = new GZipStream(packageStream, CompressionMode.Decompress);
        using var tarReader = new TarReader(gzipStream, false);

        var extractedFiles = new List<string>();
        var assetStates = new Dictionary<string, UnityPackageAssetState>(StringComparer.OrdinalIgnoreCase);
        var pendingWrites = new List<PendingAssetWrite>();
        var queuedStates = new HashSet<UnityPackageAssetState>();
        var directoriesToCleanup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var generatedRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicateSuffixCounters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        using var temporaryDirectory = CreateTemporaryDirectory(options.TemporaryDirectory);
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
                    generatedRelativePaths,
                    duplicateSuffixCounters,
                    directoriesToCleanup,
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

            switch (componentName)
            {
                case "pathname":
                {
                    var normalization =
                        NormalizeRelativePath(ReadEntryAsUtf8String(entry.DataStream, cancellationToken));
                    state.RelativePath = normalization.NormalizedPath;
                    state.OriginalRelativePath = normalization.OriginalPath;
                    state.PathNormalizations = normalization.Segments;
                    break;
                }
                case "asset":
                {
                    var component = CreateAssetComponent(entry.DataStream, temporaryDirectory.DirectoryPath,
                        cancellationToken);
                    state.SetAssetComponent(component);
                    break;
                }
                case "asset.meta":
                {
                    var component = CreateAssetComponent(entry.DataStream, temporaryDirectory.DirectoryPath,
                        cancellationToken);
                    state.SetMetaComponent(component);
                    break;
                }
                case "preview.png":
                {
                    var component = CreateAssetComponent(entry.DataStream, temporaryDirectory.DirectoryPath,
                        cancellationToken);
                    state.SetPreviewComponent(component);
                    break;
                }
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
            CleanupCorruptedDirectories(directoriesToCleanup);

            LoggingService.LogInformation($"No writable assets detected in '{packagePath}'.");

            return new UnityPackageExtractionResult(
                packagePath,
                outputDirectory,
                0,
                extractedFiles.ToImmutableArray());
        }

        if (pendingWrites.Count > 0)
            FlushPendingWrites();

        CleanupCorruptedDirectories(directoriesToCleanup);

        LoggingService.LogInformation(
            $"Extraction flow completed for '{packagePath}'. AssetsExtracted={extractedCount}, filesWritten={extractedFiles.Count}.");

        return new UnityPackageExtractionResult(
            packagePath,
            outputDirectory,
            extractedCount,
            extractedFiles.ToImmutableArray());
    }

    private static bool TryGetAssetPaths(
        UnityPackageAssetState state,
        string outputDirectory,
        bool organizeByCategories,
        HashSet<string> generatedRelativePaths,
        Dictionary<string, int> duplicateSuffixCounters,
        HashSet<string> directoriesToCleanup,
        out string targetPath,
        out string? metaPath,
        out string? previewPath)
    {
        targetPath = string.Empty;
        metaPath = null;
        previewPath = null;

        if (state.RelativePath is null || state.Asset is not { HasContent: true })
            return false;

        var relativeOutputPath = ResolveOutputRelativePath(state, organizeByCategories);

        if (string.IsNullOrWhiteSpace(relativeOutputPath))
            return false;

        relativeOutputPath = EnsureUniqueRelativePath(
            relativeOutputPath,
            generatedRelativePaths,
            duplicateSuffixCounters,
            organizeByCategories);

        targetPath = Path.Combine(outputDirectory, relativeOutputPath);
        metaPath = state.Meta is { HasContent: true } ? $"{targetPath}.meta" : null;
        previewPath = state.Preview is { HasContent: true } ? $"{targetPath}.preview.png" : null;
        TrackCorruptedDirectories(outputDirectory, state, directoriesToCleanup);
        return true;
    }

    private static string? ResolveOutputRelativePath(
        UnityPackageAssetState state,
        bool organizeByCategories)
    {
        var relativePath = state.RelativePath;
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        if (!organizeByCategories)
        {
            var normalizedSegments = state.PathNormalizations?
                .Select(segment => segment.Normalized)
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .ToArray();

            if (normalizedSegments is { Length: > 0 })
                return Path.Combine(normalizedSegments);

            return relativePath;
        }

        var assetSize = state.Asset?.Length ?? 0L;
        var category = UnityAssetClassification.ResolveCategory(
            state.OriginalRelativePath ?? relativePath,
            assetSize,
            state.Asset?.HasContent ?? false);
        var categorySegment = SanitizePathSegment(category);

        var fileName = Path.GetFileName(relativePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            var fallbackSegment = state.PathNormalizations?
                .Select(segment => segment.Normalized)
                .LastOrDefault(segment => !string.IsNullOrWhiteSpace(segment));
            if (!string.IsNullOrWhiteSpace(fallbackSegment))
            {
                fileName = fallbackSegment;
            }
            else
            {
                var originalFileName = Path.GetFileName(state.OriginalRelativePath ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(originalFileName))
                    fileName = SanitizePathSegment(originalFileName);
            }
        }

        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "Asset";

        return Path.Combine(categorySegment, fileName);
    }

    private static string EnsureUniqueRelativePath(
        string relativePath,
        HashSet<string> usedRelativePaths,
        Dictionary<string, int> duplicateSuffixCounters,
        bool allowSuffixes)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return relativePath;

        if (usedRelativePaths.Add(relativePath) || !allowSuffixes)
            return relativePath;

        var directory = Path.GetDirectoryName(relativePath);
        var fileName = Path.GetFileName(relativePath);

        if (string.IsNullOrWhiteSpace(fileName)) fileName = "Asset";

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = fileName;
            extension = string.Empty;
        }

        var duplicateKey = string.IsNullOrWhiteSpace(directory)
            ? fileName
            : $"{directory}/{fileName}";

        if (!duplicateSuffixCounters.TryGetValue(duplicateKey, out var counter))
            counter = 1;

        while (true)
        {
            var suffixedName = $"{baseName} ({counter}){extension}";
            var candidatePath = string.IsNullOrWhiteSpace(directory)
                ? suffixedName
                : Path.Combine(directory, suffixedName);

            if (usedRelativePaths.Add(candidatePath))
            {
                duplicateSuffixCounters[duplicateKey] = counter + 1;
                return candidatePath;
            }

            counter++;
        }
    }

    private static string SanitizePathSegment(string? segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return "Other";

        var filtered = segment
            .Where(static c =>
                c != Path.DirectorySeparatorChar &&
                c != Path.AltDirectorySeparatorChar)
            .Where(c => !InvalidFileNameCharacters.Contains(c))
            .ToArray();

        var sanitized = new string(filtered).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "Other" : sanitized;
    }

    private static void TrackCorruptedDirectories(
        string outputDirectory,
        UnityPackageAssetState state,
        HashSet<string> directoriesToCleanup)
    {
        if (directoriesToCleanup is null)
            return;

        var normalizations = state.PathNormalizations;
        if (normalizations is null || normalizations.Count <= 1)
            return;

        string? originalAccumulated = null;
        for (var i = 0; i < normalizations.Count - 1; i++)
        {
            var normalization = normalizations[i];
            var originalSegment = normalization.Original;

            if (string.IsNullOrWhiteSpace(originalSegment))
                continue;

            originalAccumulated = originalAccumulated is null
                ? originalSegment
                : Path.Combine(originalAccumulated, originalSegment);

            if (string.Equals(
                    normalization.Original,
                    normalization.Normalized,
                    StringComparison.Ordinal))
                continue;

            if (string.IsNullOrWhiteSpace(originalAccumulated))
                continue;

            var candidate = Path.Combine(outputDirectory, originalAccumulated);
            directoriesToCleanup.Add(candidate);
        }
    }

    private static void CleanupCorruptedDirectories(HashSet<string> directoriesToCleanup)
    {
        if (directoriesToCleanup.Count == 0)
            return;

        foreach (var directory in directoriesToCleanup
                     .OrderByDescending(path => path.Length))
            try
            {
                if (!Directory.Exists(directory))
                    continue;

                if (Directory.EnumerateFileSystemEntries(directory).Any())
                    continue;

                Directory.Delete(directory);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
    }

    private static AssetWritePlan CreateAssetWritePlan(
        UnityPackageAssetState state,
        string targetPath,
        string? metaPath,
        string? previewPath)
    {
        var writeAsset = state.Asset is { HasContent: true } && NeedsWrite(targetPath, state.Asset);
        var writeMeta = metaPath is not null &&
                        state.Meta is { HasContent: true } &&
                        NeedsWrite(metaPath, state.Meta);
        var writePreview = previewPath is not null &&
                           state.Preview is { HasContent: true } &&
                           NeedsWrite(previewPath, state.Preview);

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

        if (plan.WriteAsset && state.Asset is { HasContent: true } assetComponent)
        {
            EnsureDirectory();
            cancellationToken.ThrowIfCancellationRequested();
            assetComponent.CopyTo(targetPath, cancellationToken);
            writtenFiles.Add(targetPath);
        }

        if (plan.WriteMeta && metaPath is not null && state.Meta is { HasContent: true } metaComponent)
        {
            EnsureDirectory();
            cancellationToken.ThrowIfCancellationRequested();
            metaComponent.CopyTo(metaPath, cancellationToken);
            writtenFiles.Add(metaPath);
        }

        if (plan.WritePreview && previewPath is not null && state.Preview is { HasContent: true } previewComponent)
        {
            EnsureDirectory();
            cancellationToken.ThrowIfCancellationRequested();
            previewComponent.CopyTo(previewPath, cancellationToken);
            writtenFiles.Add(previewPath);
        }

        return writtenFiles;
    }

    private static bool NeedsWrite(string path, AssetComponent component)
    {
        if (!File.Exists(path))
            return true;

        return !ContentMatches(path, component);
    }

    private static bool ContentMatches(string path, AssetComponent component)
    {
        try
        {
            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists || fileInfo.Length != component.Length)
                return false;

            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var fileHash = SHA256.HashData(stream);
            return CryptographicOperations.FixedTimeEquals(fileHash, component.ContentHash.Span);
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

    private static string ReadEntryAsUtf8String(Stream dataStream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            dataStream,
            Encoding.UTF8,
            true,
            StreamCopyBufferSize,
            true);
        var text = reader.ReadToEnd();
        cancellationToken.ThrowIfCancellationRequested();
        return text;
    }

    private static AssetComponent? CreateAssetComponent(
        Stream dataStream,
        string temporaryDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(temporaryDirectory);

        var tempPath = Path.Combine(temporaryDirectory, $"{Guid.NewGuid():N}.tmp");
        using var output = File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(StreamCopyBufferSize);
        long totalWritten = 0;

        try
        {
            int read;
            while ((read = dataStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                output.Write(buffer, 0, read);
                hasher.AppendData(buffer, 0, read);
                totalWritten += read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        output.Flush();
        var hash = hasher.GetHashAndReset();
        output.Dispose();

        if (totalWritten == 0)
        {
            TryDeleteFile(tempPath);
            return null;
        }

        return new AssetComponent(tempPath, totalWritten, hash);
    }

    private static TemporaryDirectoryScope CreateTemporaryDirectory(string? baseDirectory)
    {
        var root = string.IsNullOrWhiteSpace(baseDirectory)
            ? Path.Combine(Path.GetTempPath(), "EasyExtractCrossPlatform")
            : baseDirectory!;

        Directory.CreateDirectory(root);

        var scopedDirectory = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scopedDirectory);
        return new TemporaryDirectoryScope(scopedDirectory);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
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

    private readonly record struct PathSegmentNormalization(string Original, string Normalized);

    private readonly record struct PathNormalizationResult(
        string NormalizedPath,
        string OriginalPath,
        IReadOnlyList<PathSegmentNormalization> Segments);

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
        private AssetComponent? _asset;
        private AssetComponent? _meta;
        private AssetComponent? _preview;
        public string? RelativePath { get; set; }
        public string? OriginalRelativePath { get; set; }
        public IReadOnlyList<PathSegmentNormalization>? PathNormalizations { get; set; }

        public AssetComponent? Asset => _asset;
        public AssetComponent? Meta => _meta;
        public AssetComponent? Preview => _preview;
        private bool Completed { get; set; }

        public bool CanWriteToDisk =>
            !Completed &&
            !string.IsNullOrWhiteSpace(RelativePath) &&
            Asset is { HasContent: true };

        public void SetAssetComponent(AssetComponent? component)
        {
            ReplaceComponent(ref _asset, component);
        }

        public void SetMetaComponent(AssetComponent? component)
        {
            ReplaceComponent(ref _meta, component);
        }

        public void SetPreviewComponent(AssetComponent? component)
        {
            ReplaceComponent(ref _preview, component);
        }

        public void MarkAsCompleted()
        {
            RelativePath = null;
            OriginalRelativePath = null;
            PathNormalizations = null;
            ReplaceComponent(ref _asset, null);
            ReplaceComponent(ref _meta, null);
            ReplaceComponent(ref _preview, null);
            Completed = true;
        }

        private void ReplaceComponent(ref AssetComponent? target, AssetComponent? value)
        {
            if (ReferenceEquals(target, value))
                return;

            if (Completed)
            {
                value?.Dispose();
                return;
            }

            target?.Dispose();
            target = value;
        }
    }

    private sealed class TemporaryDirectoryScope : IDisposable
    {
        public TemporaryDirectoryScope(string directoryPath)
        {
            DirectoryPath = directoryPath;
        }

        public string DirectoryPath { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(DirectoryPath))
                    Directory.Delete(DirectoryPath, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed class AssetComponent : IDisposable
    {
        private bool _disposed;

        public AssetComponent(string tempPath, long length, byte[] contentHash)
        {
            TempPath = tempPath;
            Length = length;
            ContentHash = contentHash;
        }

        public string TempPath { get; }
        public long Length { get; }
        public ReadOnlyMemory<byte> ContentHash { get; }
        public bool HasContent => Length > 0;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            TryDeleteFile(TempPath);
        }

        public void CopyTo(string destinationPath, CancellationToken cancellationToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AssetComponent));

            cancellationToken.ThrowIfCancellationRequested();

            using var source = File.Open(TempPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var destination = File.Open(destinationPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            var buffer = ArrayPool<byte>.Shared.Rent(StreamCopyBufferSize);

            try
            {
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    destination.Write(buffer, 0, read);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}