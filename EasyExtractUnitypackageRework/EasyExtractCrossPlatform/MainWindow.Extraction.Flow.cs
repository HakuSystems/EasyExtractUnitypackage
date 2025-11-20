namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private async Task RunSingleExtractionPickerAsync()
    {
        if (!EnsureExtractionIdle())
            return;

        if (StorageProvider is null)
        {
            ShowDropStatusMessage(
                "File picker unavailable",
                "Restart the app and try again.",
                TimeSpan.FromSeconds(4),
                UiSoundEffect.Negative);
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select a Unity package",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Unity package")
                {
                    Patterns = new[] { "*.unitypackage" }
                }
            }
        });

        if (files.Count == 0)
            return;

        var path = TryResolveLocalPath(files[0]);
        if (string.IsNullOrWhiteSpace(path))
        {
            ShowDropStatusMessage(
                "Unsupported location",
                "Only local files can be extracted.",
                TimeSpan.FromSeconds(4),
                UiSoundEffect.Negative);
            return;
        }

        await RunExtractionSequenceAsync(new[] { new ExtractionItem(path, null) });
    }


    private async Task RunBatchExtractionPickerAsync()
    {
        if (!EnsureExtractionIdle())
            return;

        if (StorageProvider is null)
        {
            ShowDropStatusMessage(
                "File picker unavailable",
                "Restart the app and try again.",
                TimeSpan.FromSeconds(4),
                UiSoundEffect.Negative);
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select one or more Unity packages",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Unity packages")
                {
                    Patterns = new[] { "*.unitypackage" }
                }
            }
        });

        if (files.Count == 0)
            return;

        var paths = files
            .Select(TryResolveLocalPath)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(p => new ExtractionItem(p!, null))
            .ToList();

        if (paths.Count == 0)
        {
            ShowDropStatusMessage(
                "No local files selected",
                "Select files stored on this device.",
                TimeSpan.FromSeconds(4),
                UiSoundEffect.Subtle);
            return;
        }

        await RunExtractionSequenceAsync(paths);
    }


    private async Task RunQueueExtractionAsync()
    {
        var queuedPackages = _settings.UnitypackageFiles?
            .Where(p => p is { IsInQueue: true })
            .Select(p => new ExtractionItem(p.FilePath, p))
            .Where(item => !string.IsNullOrWhiteSpace(item.Path))
            .ToList();

        if (queuedPackages is null || queuedPackages.Count == 0)
        {
            ShowDropStatusMessage(
                "Queue is empty",
                "Add packages before starting extraction.",
                TimeSpan.FromSeconds(4),
                UiSoundEffect.Subtle);
            return;
        }

        await RunExtractionSequenceAsync(queuedPackages);
    }


    private async Task RunExtractionSequenceAsync(IReadOnlyList<ExtractionItem> items)
    {
        var validItems = items
            .Where(item => !string.IsNullOrWhiteSpace(item.Path))
            .ToList();

        if (validItems.Count == 0)
            return;

        if (_isExtractionRunning)
        {
            ShowDropStatusMessage(
                "Extraction already running",
                "Wait for the current extraction to finish.",
                TimeSpan.FromSeconds(4),
                UiSoundEffect.Subtle);
            return;
        }

        _isExtractionRunning = true;
        _isExtractionCancelling = false;
        _extractionCts = new CancellationTokenSource();
        UpdateExtractionButtonsState();
        BeginExtractionOverviewSession();
        UiSoundService.Instance.Play(UiSoundEffect.Positive);
        PrepareExtractionDashboard(validItems);

        try
        {
            for (var index = 0; index < validItems.Count; index++)
            {
                _extractionCts!.Token.ThrowIfCancellationRequested();

                var item = validItems[index];
                var packagePath = item.Path;
                var queueEntry = item.QueueEntry;

                if (!File.Exists(packagePath))
                {
                    ShowDropStatusMessage(
                        "Package not found",
                        Path.GetFileName(packagePath),
                        TimeSpan.FromSeconds(4),
                        UiSoundEffect.Negative);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        UpdateExtractionDashboardQueueBadge(Math.Max(0, validItems.Count - index - 1));
                        UpdateExtractionDashboardSubtitle("Waiting for next package...");
                        UpdateExtractionDashboardNextPackageText(ResolveNextPackageName(validItems, index));
                    });

                    if (queueEntry is not null)
                    {
                        queueEntry.IsExtracting = false;
                        await Dispatcher.UIThread.InvokeAsync(() => AddOrUpdateQueueDisplayItem(queueEntry));
                    }

                    continue;
                }

                var normalizedPackagePath = TryNormalizeFilePath(packagePath);
                if (string.IsNullOrWhiteSpace(normalizedPackagePath))
                    normalizedPackagePath = packagePath;

                MaliciousCodeScanResult? securityResult = null;
                if (IsSecurityScanningEnabled)
                {
                    securityResult = await EnsureSecurityScanTaskAsync(normalizedPackagePath);
                    if (securityResult?.IsMalicious == true && _securityWarningsShown.Add(normalizedPackagePath))
                    {
                        var warningText = BuildSecuritySummaryText(securityResult);
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            ShowDropStatusMessage(
                                "Potentially malicious package",
                                warningText,
                                TimeSpan.FromSeconds(6),
                                UiSoundEffect.Negative);
                        });
                    }
                    else if (securityResult is null && _securityInfoNotified.Add(normalizedPackagePath))
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                            ShowDropStatusMessage(
                                "Security scan unavailable",
                                "Unable to verify this package for malicious code.",
                                TimeSpan.FromSeconds(4),
                                UiSoundEffect.Subtle));
                    }
                }

                if (queueEntry is not null)
                {
                    queueEntry.IsExtracting = true;
                    await Dispatcher.UIThread.InvokeAsync(() => AddOrUpdateQueueDisplayItem(queueEntry));
                }

                var outputDirectory = ResolveOutputDirectory(packagePath);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UpdateExtractionDashboardSecurityStatus(securityResult);
                    BeginExtractionDashboardForPackage(packagePath, outputDirectory, validItems, index);
                });

                var progress = new Progress<UnityPackageExtractionProgress>(update =>
                {
                    if (!string.IsNullOrWhiteSpace(update.AssetPath))
                    {
                        UpdateExtractionDashboardProgress(update.AssetPath, update.AssetsExtracted);
                        ShowDropStatusMessage(
                            $"Extracting {Path.GetFileName(packagePath)}",
                            update.AssetPath,
                            TimeSpan.Zero);
                    }
                    else
                    {
                        UpdateExtractionDashboardProgress(null, update.AssetsExtracted);
                    }
                });

                var extractionTimer = Stopwatch.StartNew();
                try
                {
                    var result =
                        await ExecuteExtractionAsync(packagePath, outputDirectory, progress, _extractionCts.Token);
                    extractionTimer.Stop();
                    if (result is not null)
                        await ApplyExtractionSuccessAsync(packagePath, result, queueEntry, extractionTimer.Elapsed,
                            outputDirectory);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                        CompleteCurrentPackageOnDashboard(
                            packagePath,
                            true,
                            result?.AssetsExtracted ?? 0,
                            remaining: Math.Max(0, validItems.Count - index - 1),
                            nextPackage: ResolveNextPackageName(validItems, index)));
                }
                catch (OperationCanceledException)
                {
                    extractionTimer.Stop();
                    await Dispatcher.UIThread.InvokeAsync(() =>
                        CompleteCurrentPackageOnDashboard(
                            packagePath,
                            false,
                            0,
                            true,
                            Math.Max(0, validItems.Count - index),
                            ResolveNextPackageName(validItems, index)));

                    if (queueEntry is not null)
                    {
                        queueEntry.IsExtracting = false;
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            AddOrUpdateQueueDisplayItem(queueEntry);
                            UpdateQueueVisualState();
                        });
                    }

                    throw;
                }
                catch (Exception ex)
                {
                    extractionTimer.Stop();
                    await HandleExtractionFailureAsync(packagePath, ex, queueEntry);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                        CompleteCurrentPackageOnDashboard(
                            packagePath,
                            false,
                            0,
                            remaining: Math.Max(0, validItems.Count - index - 1),
                            nextPackage: ResolveNextPackageName(validItems, index)));
                }
            }
        }
        catch (OperationCanceledException)
        {
            ShowDropStatusMessage(
                "Extraction cancelled",
                "The operation was cancelled.",
                TimeSpan.FromSeconds(4),
                UiSoundEffect.Subtle);
        }
        finally
        {
            _isExtractionCancelling = false;
            FinishExtractionDashboard(TimeSpan.FromSeconds(2));
            EndExtractionOverviewSession();
            _isExtractionRunning = false;
            _extractionCts?.Dispose();
            _extractionCts = null;
            UpdateExtractionButtonsState();
            RestorePresenceAfterOverlay();
            TryLaunchPendingUpdateAfterExtraction();
        }
    }


    private async Task<UnityPackageExtractionResult?> ExecuteExtractionAsync(
        string packagePath,
        string outputDirectory,
        IProgress<UnityPackageExtractionProgress> progress,
        CancellationToken cancellationToken)
    {
        var options = BuildExtractionOptions();
        return await _extractionService.ExtractAsync(packagePath, outputDirectory, options, progress,
            cancellationToken);
    }


    private async Task ApplyExtractionSuccessAsync(
        string packagePath,
        UnityPackageExtractionResult result,
        UnityPackageFile? queueEntry,
        TimeSpan duration,
        string outputDirectory)
    {
        OnExtractionOverviewPackageCompleted(result.AssetsExtracted);
        UpdateExtractionStatistics(packagePath, result);
        UpdateHistoryAfterExtraction(packagePath, result, duration, outputDirectory);

        if (queueEntry is not null)
        {
            queueEntry.IsExtracting = false;
            queueEntry.IsInQueue = false;
            var normalized = TryNormalizeFilePath(queueEntry.FilePath);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                RemoveQueueDisplayItem(normalized);
                UpdateQueueVisualState();
            });
        }

        ShowDropStatusMessage(
            "Extraction complete",
            $"{Path.GetFileName(packagePath)} extracted.",
            TimeSpan.FromSeconds(4),
            UiSoundEffect.Positive);
        _notificationService.ShowExtractionSuccess(packagePath, outputDirectory, result.AssetsExtracted);
    }


    private async Task HandleExtractionFailureAsync(
        string packagePath,
        Exception exception,
        UnityPackageFile? queueEntry)
    {
        Debug.WriteLine($"Extraction failed for '{packagePath}': {exception}");

        if (queueEntry is not null)
        {
            queueEntry.IsExtracting = false;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AddOrUpdateQueueDisplayItem(queueEntry);
                UpdateQueueVisualState();
            });
        }

        var message = exception switch
        {
            InvalidDataException => "The package could not be read. It may be damaged.",
            _ => exception.Message
        };

        ShowDropStatusMessage(
            "Extraction failed",
            message,
            TimeSpan.FromSeconds(6),
            UiSoundEffect.Negative);
    }


    private void UpdateExtractionStatistics(string packagePath, UnityPackageExtractionResult result)
    {
        _settings.LastExtractionTime = DateTimeOffset.Now;
        _settings.TotalExtracted = Math.Max(0, _settings.TotalExtracted) + 1;
        if (result.AssetsExtracted > 0)
            _settings.TotalFilesExtracted = Math.Max(0, _settings.TotalFilesExtracted) + result.AssetsExtracted;

        if (_settings.ExtractedUnitypackages is null)
            _settings.ExtractedUnitypackages = new List<string>();

        if (_settings.ExtractedUnitypackages.All(existing =>
                !string.Equals(existing, packagePath, StringComparison.OrdinalIgnoreCase)))
            _settings.ExtractedUnitypackages.Add(packagePath);

        var normalizedPackagePath = TryNormalizeFilePath(packagePath);

        if (_settings.UnitypackageFiles is { Count: > 0 })
            for (var index = _settings.UnitypackageFiles.Count - 1; index >= 0; index--)
            {
                var entry = _settings.UnitypackageFiles[index];
                if (entry is null)
                    continue;

                var entryPath = TryNormalizeFilePath(entry.FilePath);
                if (string.Equals(entryPath, normalizedPackagePath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(entry.FilePath, packagePath, StringComparison.OrdinalIgnoreCase))
                    _settings.UnitypackageFiles.RemoveAt(index);
            }

        AppSettingsService.Save(_settings);
        if (!_isExtractionOverviewLive)
            UpdateExtractionOverviewDisplay();
    }


    private UnityPackageExtractionOptions BuildExtractionOptions()
    {
        var settings = _settings;
        if (settings is null)
            throw new InvalidOperationException("Application settings have not been loaded.");

        var tempPath = string.IsNullOrWhiteSpace(settings.DefaultTempPath)
            ? null
            : settings.DefaultTempPath;

        var limits = UnityPackageExtractionLimits.Normalize(settings.ExtractionLimits);
        return new UnityPackageExtractionOptions(
            settings.ExtractedCategoryStructure,
            tempPath,
            limits);
    }


    private string ResolveOutputDirectory(string packagePath)
    {
        var baseOutput = _settings.DefaultOutputPath;
        if (string.IsNullOrWhiteSpace(baseOutput))
            baseOutput = Path.Combine(AppSettingsService.SettingsDirectory, "Extracted");

        var folderName = Path.GetFileNameWithoutExtension(packagePath);
        if (string.IsNullOrWhiteSpace(folderName))
            folderName = "ExtractedPackage";

        var targetPath = Path.Combine(baseOutput, folderName);
        Directory.CreateDirectory(targetPath);
        return targetPath;
    }


    private bool EnsureExtractionIdle()
    {
        if (!_isExtractionRunning)
            return true;

        ShowDropStatusMessage(
            "Extraction already running",
            "Please wait for the current extraction to complete.",
            TimeSpan.FromSeconds(4),
            UiSoundEffect.Subtle);
        return false;
    }



    private void UpdateExtractionButtonsState()
    {
        var hasQueueItems = _queueItems.Count > 0;

        if (_startExtractionButton is not null)
            _startExtractionButton.IsEnabled = !_isExtractionRunning;

        if (_batchExtractionButton is not null)
            _batchExtractionButton.IsEnabled = !_isExtractionRunning;

        if (_processQueueButton is not null)
            _processQueueButton.IsEnabled = !_isExtractionRunning && hasQueueItems;

        if (_clearQueueButton is not null)
            _clearQueueButton.IsEnabled = hasQueueItems && !_isExtractionRunning;

        if (_cancelExtractionButton is not null)
        {
            _cancelExtractionButton.IsVisible = _isExtractionRunning;
            _cancelExtractionButton.IsEnabled = _isExtractionRunning && !_isExtractionCancelling;
        }
    }

}
