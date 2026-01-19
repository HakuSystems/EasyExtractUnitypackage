using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using EasyExtract.Core.Models;

namespace EasyExtract.Core.Services;

public sealed partial class UnityPackageExtractionService
{
    private sealed class ExtractionSession
    {
        private readonly Dictionary<string, UnityPackageAssetState> _assetStates =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly CancellationToken _cancellationToken;
        private readonly string _correlationId;
        private readonly HashSet<string> _directoriesToCleanup = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _duplicateSuffixCounters = new(StringComparer.OrdinalIgnoreCase);

        private readonly List<string> _extractedFiles = new();
        private readonly HashSet<string> _generatedRelativePaths = new(StringComparer.OrdinalIgnoreCase);
        private readonly ExtractionLimiter _limiter;
        private readonly UnityPackageExtractionLimits _limits;
        private readonly string _normalizedOutputDirectory;
        private readonly bool _organizeByCategories;
        private readonly string _outputDirectory;
        private readonly string _packagePath;
        private readonly List<PendingAssetWrite> _pendingWrites = new();
        private readonly IProgress<UnityPackageExtractionProgress>? _progress;
        private readonly HashSet<UnityPackageAssetState> _queuedStates = new();

        private readonly TarReader _tarReader;
        private readonly string _temporaryDirectoryPath;
        private readonly IEasyExtractLogger _logger; // Logger Injected

        private int _assetsDuplicated;
        private int _assetsSkippedNoContent;
        private int _assetsSkippedNoPath;
        private int _extractedCount;
        private bool _extractionRequired;
        private int _tarEntriesProcessed;
        private int _tarEntriesSkipped;
        private long _calculatedTotalSize; // To store accumulated size

        public ExtractionSession(
            string packagePath,
            string outputDirectory,
            string normalizedOutputDirectory,
            bool organizeByCategories,
            UnityPackageExtractionLimits limits,
            string temporaryDirectoryPath,
            TarReader tarReader,
            IProgress<UnityPackageExtractionProgress>? progress,
            CancellationToken cancellationToken,
            string correlationId,
            IEasyExtractLogger logger) // Logger Injected
        {
            _packagePath = packagePath;
            _outputDirectory = outputDirectory;
            _normalizedOutputDirectory = normalizedOutputDirectory;
            _organizeByCategories = organizeByCategories;
            _limits = limits;
            _limiter = new ExtractionLimiter(_limits);
            _temporaryDirectoryPath = temporaryDirectoryPath;
            _tarReader = tarReader;
            _progress = progress;
            _cancellationToken = cancellationToken;
            _correlationId = correlationId;
            _logger = logger;
            _assetsDuplicated = 0;
            _calculatedTotalSize = 0;
        }

        public UnityPackageExtractionResult Execute()
        {
            ProcessTarEntries();
            ProcessRemainingAssetStates();

            if (!_extractionRequired)
                return CompleteWithoutExtraction();

            if (_pendingWrites.Count > 0)
                FlushPendingWrites();

            CleanupCorruptedDirectories(_directoriesToCleanup, _correlationId, _logger);

            _logger.LogInformation(
                $"ExtractInternal completed | package='{_packagePath}' | assetsExtracted={_extractedCount} | filesWritten={_extractedFiles.Count} | size={_calculatedTotalSize} | tarEntries={_tarEntriesProcessed} | skippedNoPath={_assetsSkippedNoPath} | skippedNoContent={_assetsSkippedNoContent} | duplicated={_assetsDuplicated} | correlationId={_correlationId}");

            return new UnityPackageExtractionResult(
                _packagePath,
                _outputDirectory,
                _extractedCount,
                _extractedFiles.ToImmutableArray()) { TotalSize = _calculatedTotalSize };
        }

        private void ProcessTarEntries()
        {
            _logger.LogInformation(
                $"Starting TAR entry processing loop | correlationId={_correlationId}");

            var lastBatchLog = Stopwatch.StartNew();
            const int batchLogInterval = 100;
            TarEntry? entry;

            try
            {
                using (_logger.BeginPerformanceScope("ProcessTarEntries", "Extraction", _correlationId))
                {
                    while ((entry = _tarReader.GetNextEntry()) is not null)
                    {
                        _cancellationToken.ThrowIfCancellationRequested();
                        _tarEntriesProcessed++;

                        if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                        {
                            _tarEntriesSkipped++;
                            continue;
                        }

                        var entryName = entry.Name ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(entryName))
                        {
                            _tarEntriesSkipped++;
                            continue;
                        }

                        var (assetKey, componentName) = SplitEntryName(entryName);
                        if (string.IsNullOrWhiteSpace(assetKey) || string.IsNullOrWhiteSpace(componentName))
                        {
                            _tarEntriesSkipped++;
                            continue;
                        }

                        if (!_assetStates.TryGetValue(assetKey, out var state))
                        {
                            state = new UnityPackageAssetState();
                            _assetStates[assetKey] = state;
                        }

                        switch (componentName)
                        {
                            case "pathname":
                            {
                                var normalization = NormalizeRelativePath(
                                    ReadEntryAsUtf8String(entry.DataStream ?? Stream.Null, _cancellationToken,
                                        _correlationId, _logger),
                                    _correlationId, _logger);
                                state.RelativePath = normalization.NormalizedPath;
                                state.OriginalRelativePath = normalization.OriginalPath;
                                state.PathNormalizations = normalization.Segments;
                                break;
                            }
                            case "asset":
                            {
                                var component = CreateAssetComponent(
                                    entry,
                                    _temporaryDirectoryPath,
                                    _limits,
                                    _limiter,
                                    _cancellationToken,
                                    _correlationId,
                                    _logger);
                                state.SetAssetComponent(component);
                                break;
                            }
                            case "asset.meta":
                            {
                                var component = CreateAssetComponent(
                                    entry,
                                    _temporaryDirectoryPath,
                                    _limits,
                                    _limiter,
                                    _cancellationToken,
                                    _correlationId,
                                    _logger);
                                state.SetMetaComponent(component);
                                break;
                            }
                            case "preview.png":
                            {
                                var component = CreateAssetComponent(
                                    entry,
                                    _temporaryDirectoryPath,
                                    _limits,
                                    _limiter,
                                    _cancellationToken,
                                    _correlationId,
                                    _logger);
                                state.SetPreviewComponent(component);
                                break;
                            }
                            default:
                                _tarEntriesSkipped++;
                                continue;
                        }

                        if (state.CanWriteToDisk && !_queuedStates.Contains(state))
                            HandleReadyState(state);

                        if (_tarEntriesProcessed % batchLogInterval == 0 &&
                            lastBatchLog.Elapsed.TotalSeconds >= 2)
                        {
                            _logger.LogInformation(
                                $"TAR processing progress: entries={_tarEntriesProcessed} | assets={_assetStates.Count} | extracted={_extractedCount} | skipped={_tarEntriesSkipped} | correlationId={_correlationId}");
                            lastBatchLog.Restart();
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException)
            {
                _logger.LogError(
                    $"TAR processing failed | entriesProcessed={_tarEntriesProcessed} | skipped={_tarEntriesSkipped} | correlationId={_correlationId}",
                    ex);

                throw new InvalidDataException(
                    "The selected file is not a valid .unitypackage or is corrupted. Please download the file again.",
                    ex);
            }

            _logger.LogInformation(
                $"TAR entry processing completed | totalEntries={_tarEntriesProcessed} | skipped={_tarEntriesSkipped} | uniqueAssets={_assetStates.Count} | correlationId={_correlationId}");
        }

        private void ProcessRemainingAssetStates()
        {
            _logger.LogInformation(
                $"Processing remaining asset states | remaining={_assetStates.Count} | queued={_queuedStates.Count} | correlationId={_correlationId}");

            var remainingProcessed = 0;
            foreach (var state in _assetStates.Values)
            {
                if (!state.CanWriteToDisk || _queuedStates.Contains(state))
                    continue;

                HandleReadyState(state);
                remainingProcessed++;
            }

            if (remainingProcessed > 0)
                _logger.LogInformation(
                    $"Processed remaining assets | count={remainingProcessed} | correlationId={_correlationId}");
        }

        private UnityPackageExtractionResult CompleteWithoutExtraction()
        {
            foreach (var pending in _pendingWrites)
                pending.State.MarkAsCompleted();

            _pendingWrites.Clear();
            _queuedStates.Clear();
            CleanupCorruptedDirectories(_directoriesToCleanup, _correlationId, _logger);

            _logger.LogInformation(
                $"No writable assets detected | package='{_packagePath}' | totalAssets={_assetStates.Count} | skippedNoPath={_assetsSkippedNoPath} | skippedNoContent={_assetsSkippedNoContent} | correlationId={_correlationId}");

            return new UnityPackageExtractionResult(
                _packagePath,
                _outputDirectory,
                0,
                _extractedFiles.ToImmutableArray()) { TotalSize = 0 };
        }

        private void HandleReadyState(UnityPackageAssetState state)
        {
            if (!TryGetAssetPaths(
                    state,
                    _normalizedOutputDirectory,
                    _normalizedOutputDirectory,
                    _organizeByCategories,
                    _generatedRelativePaths,
                    _duplicateSuffixCounters,
                    _directoriesToCleanup,
                    _correlationId,
                    _logger,
                    out var targetPath,
                    out var metaPath,
                    out var previewPath))
            {
                if (state.RelativePath is null)
                    _assetsSkippedNoPath++;
                else if (state.Asset is not { HasContent: true })
                    _assetsSkippedNoContent++;

                _logger.LogInformation(
                    $"Asset skipped (no valid paths) | relativePath='{state.RelativePath}' | hasAsset={state.Asset?.HasContent} | correlationId={_correlationId}");
                return;
            }

            _limiter.RegisterAsset();
            var plan = CreateAssetWritePlan(state, targetPath, metaPath, previewPath, _logger);

            if (!plan.RequiresWrite)
            {
                _logger.LogInformation(
                    $"Asset skipped (already up-to-date) | path='{state.RelativePath}' | target='{targetPath}' | correlationId={_correlationId}");
                state.MarkAsCompleted();
                _queuedStates.Remove(state);
                return;
            }

            if (!_extractionRequired)
            {
                _extractionRequired = true;
                _logger.LogInformation(
                    $"First asset queued for extraction | path='{state.RelativePath}' | correlationId={_correlationId}");
                _pendingWrites.Add(new PendingAssetWrite(state, targetPath, metaPath, previewPath, plan));
                _queuedStates.Add(state);
                FlushPendingWrites();
            }
            else
            {
                WriteAndTrack(state, targetPath, metaPath, previewPath, plan);
            }
        }

        private void WriteAndTrack(
            UnityPackageAssetState state,
            string targetPath,
            string? metaPath,
            string? previewPath,
            AssetWritePlan plan)
        {
            var relativePath = state.RelativePath;
         
            // Accumulate Size
            if (plan.WriteAsset && state.Asset is { HasContent: true } assetComp)
                _calculatedTotalSize += assetComp.Length;
            if (plan.WriteMeta && state.Meta is { HasContent: true } metaComp)
                _calculatedTotalSize += metaComp.Length;
            if (plan.WritePreview && state.Preview is { HasContent: true } previewComp)
                _calculatedTotalSize += previewComp.Length;

            using (_logger.BeginPerformanceScope("WriteAssetToDisk", "Extraction", _correlationId))
            {
                var writtenFiles = WriteAssetToDisk(
                    state,
                    targetPath,
                    metaPath,
                    previewPath,
                    plan,
                    _cancellationToken,
                    _correlationId,
                    _logger);

                if (writtenFiles.Count > 0)
                {
                    _extractedCount++;
                    _extractedFiles.AddRange(writtenFiles);
                    _progress?.Report(new UnityPackageExtractionProgress(relativePath, _extractedCount));
                }
                else
                {
                    _logger.LogInformation(
                        $"Asset write produced no files | path='{state.RelativePath}' | correlationId={_correlationId}");
                }
            }

            state.MarkAsCompleted();
            _queuedStates.Remove(state);
        }

        private void FlushPendingWrites()
        {
            if (_pendingWrites.Count == 0)
                return;

            _logger.LogInformation(
                $"FlushPendingWrites: flushing pending assets | count={_pendingWrites.Count} | correlationId={_correlationId}");

            foreach (var pending in _pendingWrites)
                WriteAndTrack(
                    pending.State,
                    pending.TargetPath,
                    pending.MetaPath,
                    pending.PreviewPath,
                    pending.Plan);

            _pendingWrites.Clear();
            _queuedStates.Clear();

            _logger.LogInformation(
                $"FlushPendingWrites completed | correlationId={_correlationId}");
        }
    }
}
