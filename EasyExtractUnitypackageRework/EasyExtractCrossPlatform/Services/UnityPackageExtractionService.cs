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
using EasyExtractCrossPlatform.Models;
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
    string? TemporaryDirectory,
    UnityPackageExtractionLimits? Limits = null);

public sealed record UnityPackageExtractionProgress(string? AssetPath, int AssetsExtracted);

public sealed record UnityPackageExtractionResult(
    string PackagePath,
    string OutputDirectory,
    int AssetsExtracted,
    IReadOnlyList<string> ExtractedFiles);

public sealed class UnityPackageExtractionService : IUnityPackageExtractionService
{
    private const int StreamCopyBufferSize = 128 * 1024;
    private const int MaxPathEntryCharacters = 4096;

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

        var correlationId = Guid.NewGuid().ToString("N")[..8];
        var limits = UnityPackageExtractionLimits.Normalize(options.Limits);

        LoggingService.LogInformation(
            $"ExtractAsync: package='{packagePath}' | output='{outputDirectory}' | organize={options.OrganizeByCategories} | temp='{options.TemporaryDirectory}' | limits=[maxAssets={limits.MaxAssets}, maxAssetBytes={limits.MaxAssetBytes:N0}, maxPackageBytes={limits.MaxPackageBytes:N0}] | correlationId={correlationId}");

        if (!File.Exists(packagePath))
        {
            LoggingService.LogError(
                $"ExtractAsync aborted: File not found | path='{packagePath}' | correlationId={correlationId}");
            throw new FileNotFoundException("Unitypackage file was not found.", packagePath);
        }

        try
        {
            Directory.CreateDirectory(outputDirectory);
            LoggingService.LogInformation(
                $"Created output directory | path='{outputDirectory}' | correlationId={correlationId}");
        }
        catch (Exception ex)
        {
            LoggingService.LogError(
                $"Failed to create output directory | path='{outputDirectory}' | correlationId={correlationId}", ex);
            throw;
        }

        if (!string.IsNullOrWhiteSpace(options.TemporaryDirectory))
        {
            try
            {
                Directory.CreateDirectory(options.TemporaryDirectory!);
                LoggingService.LogInformation(
                    $"Created temporary directory | path='{options.TemporaryDirectory}' | correlationId={correlationId}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError(
                    $"Failed to create temporary directory | path='{options.TemporaryDirectory}' | correlationId={correlationId}",
                    ex);
                throw;
            }
        }

        using (LoggingService.BeginPerformanceScope("UnityPackageExtraction", "Extraction",
                   correlationId))
        {
            try
            {
                LoggingService.LogMemoryUsage($"Before extraction | correlationId={correlationId}");

                var result = await Task.Run(() =>
                        ExtractInternal(packagePath, outputDirectory, options, progress, cancellationToken,
                            correlationId),
                    cancellationToken).ConfigureAwait(false);

                LoggingService.LogMemoryUsage($"After extraction | correlationId={correlationId}",
                    true);
                LoggingService.LogInformation(
                    $"ExtractAsync completed: assets={result.AssetsExtracted} | files={result.ExtractedFiles.Count} | output='{result.OutputDirectory}' | correlationId={correlationId}");
                return result;
            }
            catch (OperationCanceledException)
            {
                LoggingService.LogInformation(
                    $"ExtractAsync cancelled | package='{packagePath}' | correlationId={correlationId}");
                throw;
            }
            catch (Exception ex)
            {
                LoggingService.LogError(
                    $"ExtractAsync failed | package='{packagePath}' | correlationId={correlationId}", ex);
                throw;
            }
        }
    }

    private static UnityPackageExtractionResult ExtractInternal(
        string packagePath,
        string outputDirectory,
        UnityPackageExtractionOptions options,
        IProgress<UnityPackageExtractionProgress>? progress,
        CancellationToken cancellationToken,
        string correlationId)
    {
        LoggingService.LogInformation(
            $"ExtractInternal started | package='{packagePath}' | correlationId={correlationId}");

        using var packageStream = File.OpenRead(packagePath);
        using var gzipStream = new GZipStream(packageStream, CompressionMode.Decompress);
        using var tarReader = new TarReader(gzipStream, false);

        var normalizedOutputDirectory = NormalizeOutputDirectory(outputDirectory);
        var limits = UnityPackageExtractionLimits.Normalize(options.Limits);
        var limiter = new ExtractionLimiter(limits);
        var extractedFiles = new List<string>();
        var assetStates = new Dictionary<string, UnityPackageAssetState>(StringComparer.OrdinalIgnoreCase);
        var pendingWrites = new List<PendingAssetWrite>();
        var queuedStates = new HashSet<UnityPackageAssetState>();
        var directoriesToCleanup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var generatedRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicateSuffixCounters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        using var temporaryDirectory = CreateTemporaryDirectory(options.TemporaryDirectory, correlationId);
        var extractionRequired = false;
        var extractedCount = 0;
        var tarEntriesProcessed = 0;
        var tarEntriesSkipped = 0;
        var assetsSkippedNoPath = 0;
        var assetsSkippedNoContent = 0;
        var assetsDuplicated = 0;

        void WriteAndTrack(
            UnityPackageAssetState state,
            string targetPath,
            string? metaPath,
            string? previewPath,
            AssetWritePlan plan)
        {
            using (LoggingService.BeginPerformanceScope("WriteAssetToDisk", "Extraction",
                       correlationId))
            {
                var writtenFiles = WriteAssetToDisk(
                    state,
                    targetPath,
                    metaPath,
                    previewPath,
                    plan,
                    cancellationToken,
                    correlationId);
                if (writtenFiles.Count > 0)
                {
                    extractedCount++;
                    extractedFiles.AddRange(writtenFiles);
                    progress?.Report(new UnityPackageExtractionProgress(state.RelativePath, extractedCount));
                }
                else
                {
                    LoggingService.LogInformation(
                        $"Asset write produced no files | path='{state.RelativePath}' | correlationId={correlationId}");
                }
            }

            state.MarkAsCompleted();
            queuedStates.Remove(state);
        }

        void FlushPendingWrites()
        {
            if (pendingWrites.Count == 0)
                return;

            LoggingService.LogInformation(
                $"FlushPendingWrites: flushing pending assets | count={pendingWrites.Count} | correlationId={correlationId}");

            foreach (var pending in pendingWrites)
                WriteAndTrack(
                    pending.State,
                    pending.TargetPath,
                    pending.MetaPath,
                    pending.PreviewPath,
                    pending.Plan);

            pendingWrites.Clear();
            queuedStates.Clear();

            LoggingService.LogInformation($"FlushPendingWrites completed | correlationId={correlationId}");
        }

        void HandleReadyState(UnityPackageAssetState state)
        {
            if (!TryGetAssetPaths(
                    state,
                    outputDirectory,
                    normalizedOutputDirectory,
                    options.OrganizeByCategories,
                    generatedRelativePaths,
                    duplicateSuffixCounters,
                    directoriesToCleanup,
                    correlationId,
                    out var targetPath,
                    out var metaPath,
                    out var previewPath))
            {
                if (state.RelativePath is null)
                    assetsSkippedNoPath++;
                else if (state.Asset is not { HasContent: true })
                    assetsSkippedNoContent++;

                LoggingService.LogInformation(
                    $"Asset skipped (no valid paths) | relativePath='{state.RelativePath}' | hasAsset={state.Asset?.HasContent} | correlationId={correlationId}");
                return;
            }

            limiter.RegisterAsset();
            var plan = CreateAssetWritePlan(state, targetPath, metaPath, previewPath);

            if (!plan.RequiresWrite)
            {
                LoggingService.LogInformation(
                    $"Asset skipped (already up-to-date) | path='{state.RelativePath}' | target='{targetPath}' | correlationId={correlationId}");
                state.MarkAsCompleted();
                queuedStates.Remove(state);
                return;
            }

            if (!extractionRequired)
            {
                extractionRequired = true;
                LoggingService.LogInformation(
                    $"First asset queued for extraction | path='{state.RelativePath}' | correlationId={correlationId}");
                pendingWrites.Add(new PendingAssetWrite(state, targetPath, metaPath, previewPath, plan));
                queuedStates.Add(state);
                FlushPendingWrites();
            }
            else
            {
                WriteAndTrack(state, targetPath, metaPath, previewPath, plan);
            }
        }

        LoggingService.LogInformation($"Starting TAR entry processing loop | correlationId={correlationId}");
        var lastBatchLog = Stopwatch.StartNew();
        const int batchLogInterval = 100;

        TarEntry? entry;
        using (LoggingService.BeginPerformanceScope("ProcessTarEntries", "Extraction",
                   correlationId))
        {
            while ((entry = tarReader.GetNextEntry()) is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                tarEntriesProcessed++;

                if (entry.EntryType == TarEntryType.Directory)
                {
                    tarEntriesSkipped++;
                    continue;
                }

                var entryName = entry.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(entryName))
                {
                    tarEntriesSkipped++;
                    continue;
                }

                var (assetKey, componentName) = SplitEntryName(entryName);
                if (string.IsNullOrWhiteSpace(assetKey) || string.IsNullOrWhiteSpace(componentName))
                {
                    tarEntriesSkipped++;
                    continue;
                }

                if (!assetStates.TryGetValue(assetKey, out var state))
                {
                    state = new UnityPackageAssetState();
                    assetStates[assetKey] = state;
                }

                if (entry.DataStream is null)
                {
                    tarEntriesSkipped++;
                    continue;
                }

                switch (componentName)
                {
                    case "pathname":
                    {
                        var normalization = NormalizeRelativePath(
                            ReadEntryAsUtf8String(entry.DataStream, cancellationToken, correlationId),
                            correlationId);
                        state.RelativePath = normalization.NormalizedPath;
                        state.OriginalRelativePath = normalization.OriginalPath;
                        state.PathNormalizations = normalization.Segments;
                        break;
                    }
                    case "asset":
                    {
                        var component = CreateAssetComponent(
                            entry,
                            temporaryDirectory.DirectoryPath,
                            limits,
                            limiter,
                            cancellationToken,
                            correlationId);
                        state.SetAssetComponent(component);
                        break;
                    }
                    case "asset.meta":
                    {
                        var component = CreateAssetComponent(
                            entry,
                            temporaryDirectory.DirectoryPath,
                            limits,
                            limiter,
                            cancellationToken,
                            correlationId);
                        state.SetMetaComponent(component);
                        break;
                    }
                    case "preview.png":
                    {
                        var component = CreateAssetComponent(
                            entry,
                            temporaryDirectory.DirectoryPath,
                            limits,
                            limiter,
                            cancellationToken,
                            correlationId);
                        state.SetPreviewComponent(component);
                        break;
                    }
                    default:
                        tarEntriesSkipped++;
                        continue;
                }

                if (state.CanWriteToDisk && !queuedStates.Contains(state))
                    HandleReadyState(state);

                // Log batch progress every N entries
                if (tarEntriesProcessed % batchLogInterval == 0 && lastBatchLog.Elapsed.TotalSeconds >= 2)
                {
                    LoggingService.LogInformation(
                        $"TAR processing progress: entries={tarEntriesProcessed} | assets={assetStates.Count} | extracted={extractedCount} | skipped={tarEntriesSkipped} | correlationId={correlationId}");
                    lastBatchLog.Restart();
                }
            }
        }

        LoggingService.LogInformation(
            $"TAR entry processing completed | totalEntries={tarEntriesProcessed} | skipped={tarEntriesSkipped} | uniqueAssets={assetStates.Count} | correlationId={correlationId}");

        // Emit remaining entries that might have been waiting for path or asset information.
        LoggingService.LogInformation(
            $"Processing remaining asset states | remaining={assetStates.Count} | queued={queuedStates.Count} | correlationId={correlationId}");
        var remainingProcessed = 0;
        foreach (var (_, state) in assetStates)
        {
            if (!state.CanWriteToDisk || queuedStates.Contains(state))
                continue;

            HandleReadyState(state);
            remainingProcessed++;
        }

        if (remainingProcessed > 0)
            LoggingService.LogInformation(
                $"Processed remaining assets | count={remainingProcessed} | correlationId={correlationId}");

        if (!extractionRequired)
        {
            foreach (var pending in pendingWrites)
                pending.State.MarkAsCompleted();

            pendingWrites.Clear();
            queuedStates.Clear();
            CleanupCorruptedDirectories(directoriesToCleanup, correlationId);

            LoggingService.LogInformation(
                $"No writable assets detected | package='{packagePath}' | totalAssets={assetStates.Count} | skippedNoPath={assetsSkippedNoPath} | skippedNoContent={assetsSkippedNoContent} | correlationId={correlationId}");

            return new UnityPackageExtractionResult(
                packagePath,
                outputDirectory,
                0,
                extractedFiles.ToImmutableArray());
        }

        if (pendingWrites.Count > 0)
            FlushPendingWrites();

        CleanupCorruptedDirectories(directoriesToCleanup, correlationId);

        LoggingService.LogInformation(
            $"ExtractInternal completed | package='{packagePath}' | assetsExtracted={extractedCount} | filesWritten={extractedFiles.Count} | tarEntries={tarEntriesProcessed} | skippedNoPath={assetsSkippedNoPath} | skippedNoContent={assetsSkippedNoContent} | duplicated={assetsDuplicated} | correlationId={correlationId}");

        return new UnityPackageExtractionResult(
            packagePath,
            outputDirectory,
            extractedCount,
            extractedFiles.ToImmutableArray());
    }

    private static string NormalizeOutputDirectory(string directory)
    {
        var full = Path.GetFullPath(directory);
        if (!Path.EndsInDirectorySeparator(full))
            full += Path.DirectorySeparatorChar;
        return full;
    }

    private static void EnsurePathIsUnderRoot(string normalizedRoot, string candidatePath, string correlationId)
    {
        var candidate = Path.GetFullPath(candidatePath);
        if (!candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            LoggingService.LogError(
                $"Path traversal detected | candidate='{candidate}' | root='{normalizedRoot}' | correlationId={correlationId}");
            throw new InvalidDataException(
                $"Extraction aborted. Asset path '{candidate}' points outside of '{normalizedRoot}'.");
        }
    }

    private static bool TryGetAssetPaths(
        UnityPackageAssetState state,
        string outputDirectory,
        string normalizedOutputDirectory,
        bool organizeByCategories,
        HashSet<string> generatedRelativePaths,
        Dictionary<string, int> duplicateSuffixCounters,
        HashSet<string> directoriesToCleanup,
        string correlationId,
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

        var originalPath = relativeOutputPath;
        relativeOutputPath = EnsureUniqueRelativePath(
            relativeOutputPath,
            generatedRelativePaths,
            duplicateSuffixCounters,
            organizeByCategories,
            correlationId);

        if (!string.Equals(originalPath, relativeOutputPath, StringComparison.Ordinal))
            LoggingService.LogInformation(
                $"Path renamed for uniqueness | original='{originalPath}' | unique='{relativeOutputPath}' | correlationId={correlationId}");

        targetPath = Path.Combine(outputDirectory, relativeOutputPath);
        metaPath = state.Meta is { HasContent: true } ? $"{targetPath}.meta" : null;
        previewPath = state.Preview is { HasContent: true } ? $"{targetPath}.preview.png" : null;

        try
        {
            EnsurePathIsUnderRoot(normalizedOutputDirectory, targetPath, correlationId);
            if (metaPath is not null)
                EnsurePathIsUnderRoot(normalizedOutputDirectory, metaPath, correlationId);
            if (previewPath is not null)
                EnsurePathIsUnderRoot(normalizedOutputDirectory, previewPath, correlationId);
        }
        catch (InvalidDataException ex)
        {
            LoggingService.LogError(
                $"Path validation failed for asset | relativePath='{state.RelativePath}' | correlationId={correlationId}",
                ex);
            throw;
        }

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
        bool allowSuffixes,
        string correlationId)
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
                LoggingService.LogInformation(
                    $"Duplicate path resolved | original='{relativePath}' | unique='{candidatePath}' | suffix={counter} | correlationId={correlationId}");
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

    private static void CleanupCorruptedDirectories(HashSet<string> directoriesToCleanup, string correlationId)
    {
        if (directoriesToCleanup.Count == 0)
            return;

        LoggingService.LogInformation(
            $"CleanupCorruptedDirectories started | count={directoriesToCleanup.Count} | correlationId={correlationId}");
        var deletedCount = 0;

        foreach (var directory in directoriesToCleanup
                     .OrderByDescending(path => path.Length))
            try
            {
                if (!Directory.Exists(directory))
                    continue;

                if (Directory.EnumerateFileSystemEntries(directory).Any())
                    continue;

                Directory.Delete(directory);
                deletedCount++;
            }
            catch (IOException ex)
            {
                LoggingService.LogWarning(
                    $"Failed to delete empty directory | path='{directory}' | correlationId={correlationId}", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                LoggingService.LogWarning(
                    $"Access denied when deleting directory | path='{directory}' | correlationId={correlationId}", ex);
            }

        if (deletedCount > 0)
            LoggingService.LogInformation(
                $"CleanupCorruptedDirectories completed | deleted={deletedCount}/{directoriesToCleanup.Count} | correlationId={correlationId}");
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
        CancellationToken cancellationToken,
        string correlationId)
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
            {
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (Exception ex)
                {
                    LoggingService.LogError(
                        $"Failed to create asset directory | path='{directory}' | correlationId={correlationId}", ex);
                    throw;
                }
            }

            directoryEnsured = true;
        }

        if (plan.WriteAsset && state.Asset is { HasContent: true } assetComponent)
        {
            EnsureDirectory();
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                assetComponent.CopyTo(targetPath, cancellationToken);
                writtenFiles.Add(targetPath);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(
                    $"Failed to write asset | path='{targetPath}' | size={assetComponent.Length} | correlationId={correlationId}",
                    ex);
                throw;
            }
        }

        if (plan.WriteMeta && metaPath is not null && state.Meta is { HasContent: true } metaComponent)
        {
            EnsureDirectory();
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                metaComponent.CopyTo(metaPath, cancellationToken);
                writtenFiles.Add(metaPath);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(
                    $"Failed to write meta file | path='{metaPath}' | correlationId={correlationId}", ex);
                throw;
            }
        }

        if (plan.WritePreview && previewPath is not null && state.Preview is { HasContent: true } previewComponent)
        {
            EnsureDirectory();
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                previewComponent.CopyTo(previewPath, cancellationToken);
                writtenFiles.Add(previewPath);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(
                    $"Failed to write preview | path='{previewPath}' | correlationId={correlationId}", ex);
                throw;
            }
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
        catch (IOException ex)
        {
            LoggingService.LogWarning($"IOException during content comparison | path='{path}'", ex);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            LoggingService.LogWarning($"Access denied during content comparison | path='{path}'", ex);
            return false;
        }
        catch (CryptographicException ex)
        {
            LoggingService.LogWarning($"Cryptographic error during content comparison | path='{path}'", ex);
            return false;
        }
    }

    private static string ReadEntryAsUtf8String(Stream dataStream, CancellationToken cancellationToken,
        string correlationId)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var reader = new StreamReader(
            dataStream,
            Encoding.UTF8,
            true,
            1024,
            true);
        var buffer = new char[512];
        var builder = new StringBuilder();
        var totalRead = 0;

        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            totalRead += read;
            if (totalRead > MaxPathEntryCharacters)
            {
                LoggingService.LogError(
                    $"Path entry exceeded max length | length={totalRead} | max={MaxPathEntryCharacters} | correlationId={correlationId}");
                throw new InvalidDataException(
                    $"Path entry exceeded the maximum supported length of {MaxPathEntryCharacters:N0} characters.");
            }

            builder.Append(buffer, 0, read);
            cancellationToken.ThrowIfCancellationRequested();
        }

        return builder.ToString();
    }

    private static AssetComponent? CreateAssetComponent(
        TarEntry entry,
        string temporaryDirectory,
        UnityPackageExtractionLimits limits,
        ExtractionLimiter limiter,
        CancellationToken cancellationToken,
        string correlationId)
    {
        if (entry.DataStream is null)
            return null;

        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(temporaryDirectory);
        var entryName = string.IsNullOrWhiteSpace(entry.Name) ? "asset" : entry.Name;
        limiter.ValidateDeclaredSize(entry.Length, entryName);

        var tempPath = Path.Combine(temporaryDirectory, $"{Guid.NewGuid():N}.tmp");
        FileStream? output = null;

        try
        {
            using (LoggingService.BeginPerformanceScope("CreateAssetComponent", "Extraction",
                       correlationId))
            {
                output = File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = ArrayPool<byte>.Shared.Rent(StreamCopyBufferSize);
                long totalWritten = 0;

                try
                {
                    int read;
                    while ((read = entry.DataStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        output.Write(buffer, 0, read);
                        hasher.AppendData(buffer, 0, read);
                        totalWritten += read;

                        if (limits.MaxAssetBytes > 0 && totalWritten > limits.MaxAssetBytes)
                        {
                            LoggingService.LogError(
                                $"Asset exceeded per-file limit | entry='{entryName}' | size={totalWritten} | limit={limits.MaxAssetBytes} | correlationId={correlationId}");
                            throw new InvalidDataException(
                                $"Asset '{entryName}' exceeded the configured per-file limit of {limits.MaxAssetBytes:N0} bytes.");
                        }
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
                    LoggingService.LogInformation(
                        $"Asset component empty, skipping | entry='{entryName}' | correlationId={correlationId}");
                    return null;
                }

                limiter.TrackAssetBytes(totalWritten);
                return new AssetComponent(tempPath, totalWritten, hash);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not InvalidDataException)
        {
            output?.Dispose();
            TryDeleteFile(tempPath);
            LoggingService.LogError(
                $"Failed to create asset component | entry='{entryName}' | correlationId={correlationId}", ex);
            throw;
        }
        catch
        {
            output?.Dispose();
            TryDeleteFile(tempPath);
            throw;
        }
    }

    private static TemporaryDirectoryScope CreateTemporaryDirectory(string? baseDirectory, string correlationId)
    {
        var root = string.IsNullOrWhiteSpace(baseDirectory)
            ? Path.Combine(Path.GetTempPath(), "EasyExtractCrossPlatform")
            : baseDirectory!;

        try
        {
            Directory.CreateDirectory(root);
        }
        catch (Exception ex)
        {
            LoggingService.LogError(
                $"Failed to create temp root directory | path='{root}' | correlationId={correlationId}", ex);
            throw;
        }

        var scopedDirectory = Path.Combine(root, Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(scopedDirectory);
            LoggingService.LogInformation(
                $"Created temporary directory | path='{scopedDirectory}' | correlationId={correlationId}");
            return new TemporaryDirectoryScope(scopedDirectory, correlationId);
        }
        catch (Exception ex)
        {
            LoggingService.LogError(
                $"Failed to create scoped temp directory | path='{scopedDirectory}' | correlationId={correlationId}",
                ex);
            throw;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException ex)
        {
            LoggingService.LogWarning($"Failed to delete temporary file | path='{path}'", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            LoggingService.LogWarning($"Access denied when deleting temporary file | path='{path}'", ex);
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

    private static PathNormalizationResult NormalizeRelativePath(string? input, string correlationId)
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
        var hadDotDotSegments = false;
        var filteredSegments = 0;

        foreach (var segment in segments)
        {
            if (segment is "." or "..")
            {
                hadDotDotSegments = true;
                continue;
            }

            var trimmed = segment.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            var filtered = new string(trimmed.Where(c => !InvalidFileNameCharacters.Contains(c)).ToArray());
            filtered = filtered.Trim();
            if (string.IsNullOrWhiteSpace(filtered))
            {
                filteredSegments++;
                continue;
            }

            originalSegments.Add(filtered);
            var normalizedSegment = FileExtensionNormalizer.Normalize(filtered);
            normalizedSegments.Add(normalizedSegment);
        }

        if (originalSegments.Count == 0)
        {
            LoggingService.LogWarning(
                $"Path normalization resulted in empty path | original='{input}' | hadDotDot={hadDotDotSegments} | filteredCount={filteredSegments} | correlationId={correlationId}");
            return new PathNormalizationResult(string.Empty, string.Empty, EmptySegmentNormalizations);
        }

        var segmentPairs = new PathSegmentNormalization[originalSegments.Count];
        for (var i = 0; i < segmentPairs.Length; i++)
            segmentPairs[i] = new PathSegmentNormalization(originalSegments[i], normalizedSegments[i]);

        var originalPath = Path.Combine(originalSegments.ToArray());
        var normalizedPath = Path.Combine(normalizedSegments.ToArray());

        if (!string.Equals(originalPath, normalizedPath, StringComparison.Ordinal))
            LoggingService.LogInformation(
                $"Path normalized | original='{originalPath}' | normalized='{normalizedPath}' | correlationId={correlationId}");

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

    private sealed class ExtractionLimiter
    {
        private int _assetCount;
        private long _totalBytes;

        public ExtractionLimiter(UnityPackageExtractionLimits limits)
        {
            Limits = limits;
        }

        public UnityPackageExtractionLimits Limits { get; }

        public void ValidateDeclaredSize(long declaredLength, string entryName)
        {
            if (declaredLength <= 0 || Limits.MaxAssetBytes <= 0)
                return;

            if (declaredLength > Limits.MaxAssetBytes)
                throw new InvalidDataException(
                    $"Entry '{entryName}' declares {declaredLength:N0} bytes which exceeds the per-file limit of {Limits.MaxAssetBytes:N0} bytes.");
        }

        public void TrackAssetBytes(long bytes)
        {
            if (bytes <= 0)
                return;

            if (Limits.MaxAssetBytes > 0 && bytes > Limits.MaxAssetBytes)
                throw new InvalidDataException(
                    $"Asset exceeded the per-file limit of {Limits.MaxAssetBytes:N0} bytes.");

            if (Limits.MaxPackageBytes <= 0)
                return;

            if (long.MaxValue - _totalBytes < bytes)
                throw new InvalidDataException("Extraction aborted due to overflow while tracking package size.");

            var next = _totalBytes + bytes;
            if (next > Limits.MaxPackageBytes)
                throw new InvalidDataException(
                    $"Extraction aborted. Total extracted bytes {next:N0} exceeded the configured limit of {Limits.MaxPackageBytes:N0} bytes.");

            _totalBytes = next;
        }

        public void RegisterAsset()
        {
            if (Limits.MaxAssets <= 0)
                return;

            if (_assetCount + 1 > Limits.MaxAssets)
                throw new InvalidDataException(
                    $"Extraction aborted. Asset count exceeded the configured limit of {Limits.MaxAssets:N0} entries.");

            _assetCount++;
        }
    }

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
        public TemporaryDirectoryScope(string directoryPath, string correlationId)
        {
            DirectoryPath = directoryPath;
            CorrelationId = correlationId;
        }

        public string DirectoryPath { get; }

        public string CorrelationId { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(DirectoryPath))
                    Directory.Delete(DirectoryPath, true);
            }
            catch (IOException ex)
            {
                LoggingService.LogWarning(
                    $"Failed to delete empty directory | path='{DirectoryPath}' | correlationId={CorrelationId}",
                    ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                LoggingService.LogWarning(
                    $"Access denied when deleting directory | path='{DirectoryPath}' | correlationId={CorrelationId}",
                    ex);
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