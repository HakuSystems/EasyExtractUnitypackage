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

public sealed partial class UnityPackageExtractionService
{
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

}
