using System.Collections.Immutable;
using System.Diagnostics;
using System.Formats.Tar;
using EasyExtract.Core.Models;

namespace EasyExtract.Core.Services;

public sealed partial class UnityPackageExtractionService
{
    private sealed class ExtractionSession
    {
        private readonly int _assetsDuplicated;

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
        private readonly IEasyExtractLogger _logger; // Logger Injected
        private readonly string _normalizedOutputDirectory;
        private readonly bool _organizeByCategories;
        private readonly string _outputDirectory;
        private readonly string _packagePath;
        private readonly List<PendingAssetWrite> _pendingWrites = new();
        private readonly IProgress<UnityPackageExtractionProgress>? _progress;
        private readonly HashSet<UnityPackageAssetState> _queuedStates = new();

        private readonly TarReader _tarReader;
        private readonly string _temporaryDirectoryPath;
        private int _assetsSkippedNoContent;
        private int _assetsSkippedNoPath;
        private long _calculatedTotalSize; // To store accumulated size
        private int _extractedCount;
        private bool _extractionRequired;
        private int _tarEntriesProcessed;
        private int _tarEntriesSkipped;

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

        public async Task<UnityPackageExtractionResult> ExecuteAsync()
        {
            await ProcessTarEntriesAsync().ConfigureAwait(false);
            await ProcessRemainingAssetStatesAsync().ConfigureAwait(false);

            if (!_extractionRequired)
                return CompleteWithoutExtraction();

            if (_pendingWrites.Count > 0)
                await FlushPendingWritesAsync().ConfigureAwait(false);

            CleanupCorruptedDirectories(_directoriesToCleanup, _correlationId, _logger);

            _logger.LogInformation(
                $"ExtractInternal completed | package='{_packagePath}' | assetsExtracted={_extractedCount} | filesWritten={_extractedFiles.Count} | size={_calculatedTotalSize} | tarEntries={_tarEntriesProcessed} | skippedNoPath={_assetsSkippedNoPath} | skippedNoContent={_assetsSkippedNoContent} | duplicated={_assetsDuplicated} | correlationId={_correlationId}");

            return new UnityPackageExtractionResult(
                _packagePath,
                _outputDirectory,
                _extractedCount,
                _extractedFiles.ToImmutableArray()) { TotalSize = _calculatedTotalSize };
        }

        private async Task ProcessTarEntriesAsync()
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
                    while ((entry = await _tarReader.GetNextEntryAsync(false, _cancellationToken).ConfigureAwait(false))
                           is not null)
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
                                var content = await ReadEntryAsUtf8StringAsync(entry.DataStream ?? Stream.Null,
                                    _cancellationToken,
                                    _correlationId, _logger).ConfigureAwait(false);
                                var normalization = NormalizeRelativePath(content, _correlationId, _logger);
                                state.RelativePath = normalization.NormalizedPath;
                                state.OriginalRelativePath = normalization.OriginalPath;
                                state.PathNormalizations = normalization.Segments;
                                break;
                            }
                            case "asset":
                            {
                                var component = await CreateAssetComponentAsync(
                                    entry,
                                    _temporaryDirectoryPath,
                                    _limits,
                                    _limiter,
                                    _cancellationToken,
                                    _correlationId,
                                    _logger).ConfigureAwait(false);
                                state.SetAssetComponent(component);
                                break;
                            }
                            case "asset.meta":
                            {
                                var component = await CreateAssetComponentAsync(
                                    entry,
                                    _temporaryDirectoryPath,
                                    _limits,
                                    _limiter,
                                    _cancellationToken,
                                    _correlationId,
                                    _logger).ConfigureAwait(false);
                                state.SetMetaComponent(component);
                                break;
                            }
                            case "preview.png":
                            {
                                var component = await CreateAssetComponentAsync(
                                    entry,
                                    _temporaryDirectoryPath,
                                    _limits,
                                    _limiter,
                                    _cancellationToken,
                                    _correlationId,
                                    _logger).ConfigureAwait(false);
                                state.SetPreviewComponent(component);
                                break;
                            }
                            default:
                                _tarEntriesSkipped++;
                                continue;
                        }

                        if (state.CanWriteToDisk && !_queuedStates.Contains(state))
                            await HandleReadyStateAsync(state).ConfigureAwait(false);

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
                if (_tarEntriesProcessed > 0 && ex is InvalidDataException)
                {
                    _logger.LogWarning(
                        $"TAR processing encountered an error after reading {_tarEntriesProcessed} entries. Archiving may have trailing junk data. Proceeding with extracted assets. | correlationId={_correlationId}",
                        ex);
                }
                else
                {
                    _logger.LogError(
                        $"TAR processing failed | entriesProcessed={_tarEntriesProcessed} | skipped={_tarEntriesSkipped} | correlationId={_correlationId}",
                        ex);

                    throw new InvalidDataException(
                        "The selected file is not a valid .unitypackage or is corrupted. Please download the file again.",
                        ex);
                }
            }

            _logger.LogInformation(
                $"TAR entry processing completed | totalEntries={_tarEntriesProcessed} | skipped={_tarEntriesSkipped} | uniqueAssets={_assetStates.Count} | correlationId={_correlationId}");
        }

        private async Task ProcessRemainingAssetStatesAsync()
        {
            _logger.LogInformation(
                $"Processing remaining asset states | remaining={_assetStates.Count} | queued={_queuedStates.Count} | correlationId={_correlationId}");

            var remainingProcessed = 0;
            foreach (var state in _assetStates.Values)
            {
                if (!state.CanWriteToDisk || _queuedStates.Contains(state))
                    continue;

                await HandleReadyStateAsync(state).ConfigureAwait(false);
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

        private async Task HandleReadyStateAsync(UnityPackageAssetState state)
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
            var plan = await CreateAssetWritePlanAsync(state, targetPath, metaPath, previewPath, _logger)
                .ConfigureAwait(false);

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
                await FlushPendingWritesAsync().ConfigureAwait(false);
            }
            else
            {
                await WriteAndTrackAsync(state, targetPath, metaPath, previewPath, plan).ConfigureAwait(false);
            }
        }

        private async Task WriteAndTrackAsync(
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
                var writtenFiles = await WriteAssetToDiskAsync(
                    state,
                    targetPath,
                    metaPath,
                    previewPath,
                    plan,
                    _cancellationToken,
                    _correlationId,
                    _logger).ConfigureAwait(false);

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

        private async Task FlushPendingWritesAsync()
        {
            if (_pendingWrites.Count == 0)
                return;

            _logger.LogInformation(
                $"FlushPendingWrites: flushing pending assets | count={_pendingWrites.Count} | correlationId={_correlationId}");

            foreach (var pending in _pendingWrites)
                await WriteAndTrackAsync(
                    pending.State,
                    pending.TargetPath,
                    pending.MetaPath,
                    pending.PreviewPath,
                    pending.Plan).ConfigureAwait(false);

            _pendingWrites.Clear();
            _queuedStates.Clear();

            _logger.LogInformation(
                $"FlushPendingWrites completed | correlationId={_correlationId}");
        }
    }
}