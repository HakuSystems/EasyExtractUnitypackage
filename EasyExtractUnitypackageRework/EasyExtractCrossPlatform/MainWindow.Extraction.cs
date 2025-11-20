namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private async void StartExtractionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await RunSingleExtractionPickerAsync();
    }

    private async void BatchExtractionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await RunBatchExtractionPickerAsync();
    }

    private void CancelExtractionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!_isExtractionRunning || _extractionCts is null)
            return;

        if (_isExtractionCancelling)
            return;

        _isExtractionCancelling = true;
        _extractionCts.Cancel();

        UpdateExtractionDashboardSubtitle("Cancelling extraction...");
        UpdateExtractionDashboardAsset("Stopping current operation...");
        if (_extractionDashboardProgressBar is not null)
        {
            _extractionDashboardProgressBar.IsIndeterminate = true;
            _extractionDashboardProgressBar.Value = 0;
        }

        ShowDropStatusMessage(
            "Cancelling extraction",
            "Stopping the current operation...",
            TimeSpan.Zero,
            UiSoundEffect.Subtle);

        UpdateExtractionButtonsState();
    }

    private async void ProcessQueueButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await RunQueueExtractionAsync();
    }

    private async void PreviewQueueItemButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: QueueItemDisplay display })
            return;

        var sourcePath = !string.IsNullOrWhiteSpace(display.FilePath)
            ? display.FilePath
            : display.NormalizedPath;

        var packagePath = TryNormalizeFilePath(sourcePath);
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
        {
            ShowDropStatusMessage(
                "Preview unavailable",
                "The selected package could not be found on disk.",
                TimeSpan.FromSeconds(4),
                UiSoundEffect.Negative);
            return;
        }

        await OpenPackagePreviewAsync(packagePath);
    }

    private async Task OpenPackagePreviewAsync(string packagePath)
    {
        try
        {
            var normalizedPath = TryNormalizeFilePath(packagePath);
            if (string.IsNullOrWhiteSpace(normalizedPath))
                normalizedPath = packagePath;

            Func<Task<MaliciousCodeScanResult?>>? scanProvider = null;
            if (IsSecurityScanningEnabled)
            {
                var targetPath = normalizedPath;
                scanProvider = () => EnsureSecurityScanTaskAsync(targetPath);
            }

            var previewWindow = new UnityPackagePreviewWindow
            {
                DataContext = new UnityPackagePreviewViewModel(
                    _previewService,
                    packagePath,
                    scanProvider)
            };

            previewWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            await previewWindow.ShowDialog(this);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open preview window: {ex}");
            ShowDropStatusMessage(
                "Preview failed",
                "Unable to open the package preview window.",
                TimeSpan.FromSeconds(4),
                UiSoundEffect.Negative);
        }
    }

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
        var tempPath = string.IsNullOrWhiteSpace(_settings.DefaultTempPath)
            ? null
            : _settings.DefaultTempPath;

        var limits = UnityPackageExtractionLimits.Normalize(_settings?.ExtractionLimits);
        return new UnityPackageExtractionOptions(
            _settings.ExtractedCategoryStructure,
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

    private void PrepareExtractionDashboard(IReadOnlyList<ExtractionItem> items)
    {
        if (_extractionDashboard is null)
            return;

        _dropZoneVisibilityReset?.Dispose();
        _dropZoneVisibilityReset = null;
        SetDropZoneSectionVisibility(false);

        _extractionDashboardHideReset?.Dispose();
        _extractionDashboardHideReset = null;

        _extractionDashboard.IsVisible = true;
        _extractionDashboard.Opacity = 1;

        UpdateExtractionDashboardSubtitle("Preparing package...");
        UpdateExtractionDashboardPackageText(items.Count > 0 ? Path.GetFileName(items[0].Path) : "—");
        UpdateExtractionDashboardAsset("Waiting...");
        UpdateExtractionDashboardOutput("—");
        UpdateExtractionDashboardAssetCount(0);
        UpdateExtractionDashboardElapsedText("0s");
        UpdateExtractionDashboardNextPackageText(ResolveNextPackageName(items, -1));
        UpdateExtractionDashboardQueueBadge(Math.Max(0, items.Count - 1));

        if (_extractionDashboardProgressBar is not null)
        {
            _extractionDashboardProgressBar.IsIndeterminate = true;
            _extractionDashboardProgressBar.Value = 0;
            _extractionDashboardProgressBar.Maximum = 1;
        }

        _extractionStopwatch?.Stop();
        _extractionStopwatch = null;
        _extractionElapsedTimer.Stop();
    }

    private void BeginExtractionDashboardForPackage(
        string packagePath,
        string outputDirectory,
        IReadOnlyList<ExtractionItem> items,
        int currentIndex)
    {
        var fileName = Path.GetFileName(packagePath);
        var nextPackage = ResolveNextPackageName(items, currentIndex);
        var remaining = Math.Max(0, items.Count - currentIndex - 1);

        OnExtractionOverviewPackageStarted();
        UpdateExtractionDashboardSubtitle("Extracting assets...");
        UpdateExtractionDashboardPackageText(fileName);
        UpdateExtractionDashboardAsset("Starting...");
        UpdateExtractionDashboardOutput(outputDirectory);
        UpdateExtractionDashboardAssetCount(0);
        UpdateExtractionDashboardElapsedText("0s");
        UpdateExtractionDashboardNextPackageText(nextPackage);
        UpdateExtractionDashboardQueueBadge(remaining);
        QueueDiscordPresenceUpdate(
            $"Extracting {fileName}",
            currentPackage: fileName,
            nextPackage: nextPackage,
            extractionActive: true,
            queueCountOverride: remaining);

        if (_extractionDashboardProgressBar is not null)
        {
            _extractionDashboardProgressBar.IsIndeterminate = true;
            _extractionDashboardProgressBar.Value = 0;
            _extractionDashboardProgressBar.Maximum = 1;
        }

        _extractionStopwatch?.Stop();
        _extractionStopwatch = Stopwatch.StartNew();
        _extractionElapsedTimer.Stop();
        _extractionElapsedTimer.Start();
        OnExtractionElapsedTick(this, EventArgs.Empty);
    }

    private void UpdateExtractionDashboardSecurityStatus(MaliciousCodeScanResult? result)
    {
        if (_extractionDashboardSecurityBanner is null || _extractionDashboardSecurityText is null)
            return;

        if (!IsSecurityScanningEnabled)
        {
            HideExtractionDashboardSecurityStatus();
            return;
        }

        if (result is null)
        {
            _extractionDashboardSecurityText.Text = "Security scan unavailable for this package.";
            ApplyExtractionSecurityBannerVisuals(SecurityBannerVisualState.Info);
            _extractionDashboardSecurityBanner.IsVisible = true;
            return;
        }

        if (result.IsMalicious)
        {
            _extractionDashboardSecurityText.Text = BuildSecuritySummaryText(result);
            ApplyExtractionSecurityBannerVisuals(SecurityBannerVisualState.Warning);
            _extractionDashboardSecurityBanner.IsVisible = true;
        }
        else
        {
            _extractionDashboardSecurityBanner.IsVisible = false;
        }
    }

    private void ApplyExtractionSecurityBannerVisuals(SecurityBannerVisualState state)
    {
        if (_extractionDashboardSecurityBanner is null)
            return;

        switch (state)
        {
            case SecurityBannerVisualState.Warning:
                _extractionDashboardSecurityBanner.Background =
                    ResolveBrushResource("EasyWarningBrush", Brushes.Orange);
                _extractionDashboardSecurityBanner.BorderBrush =
                    ResolveBrushResource("EasyWarningBrush", Brushes.OrangeRed);
                break;
            default:
                _extractionDashboardSecurityBanner.Background =
                    ResolveBrushResource("EasyGlassOverlayStrongBrush", Brushes.DimGray);
                _extractionDashboardSecurityBanner.BorderBrush =
                    ResolveBrushResource("EasyAccentBrush", Brushes.Gray);
                break;
        }
    }

    private void HideExtractionDashboardSecurityStatus()
    {
        if (_extractionDashboardSecurityBanner is null)
            return;

        _extractionDashboardSecurityBanner.IsVisible = false;
        if (_extractionDashboardSecurityText is not null)
            _extractionDashboardSecurityText.Text = string.Empty;
    }

    private void UpdateExtractionDashboardProgress(string? assetPath, int assetsExtracted)
    {
        UpdateExtractionDashboardAssetCount(Math.Max(0, assetsExtracted));
        UpdateExtractionOverviewProgress(assetsExtracted);
        if (!string.IsNullOrWhiteSpace(assetPath))
            UpdateExtractionDashboardAsset(assetPath);
    }

    private void CompleteCurrentPackageOnDashboard(
        string packagePath,
        bool success,
        int assetsExtracted,
        bool isCancelled = false,
        int remaining = 0,
        string? nextPackage = null)
    {
        var fileName = Path.GetFileName(packagePath);

        _extractionStopwatch?.Stop();
        _extractionElapsedTimer.Stop();

        var statusText = success
            ? $"Completed {fileName}"
            : isCancelled
                ? $"Cancelled {fileName}"
                : $"Failed {fileName}";
        UpdateExtractionDashboardSubtitle(statusText);

        if (_extractionDashboardAssetText is not null)
            _extractionDashboardAssetText.Text = success
                ? "All assets extracted."
                : isCancelled
                    ? "Extraction cancelled."
                    : "Extraction failed.";

        UpdateExtractionDashboardAssetCount(Math.Max(0, assetsExtracted));
        UpdateExtractionDashboardQueueBadge(Math.Max(0, remaining));
        UpdateExtractionDashboardNextPackageText(nextPackage);

        if (_extractionDashboardProgressBar is not null)
        {
            _extractionDashboardProgressBar.IsIndeterminate = false;
            _extractionDashboardProgressBar.Maximum = 1;
            _extractionDashboardProgressBar.Value = success ? 1 : 0;
        }

        var detailOverride = success
            ? remaining > 0
                ? $"Moving to next package - {remaining} remaining"
                : "All assets extracted"
            : isCancelled
                ? "Extraction cancelled by user"
                : "Extraction failed - Check notifications";

        QueueDiscordPresenceUpdate(
            statusText,
            detailOverride,
            success ? null : fileName,
            nextPackage,
            remaining > 0,
            remaining);
    }

    private void FinishExtractionDashboard(TimeSpan delay)
    {
        _extractionStopwatch?.Stop();
        _extractionElapsedTimer.Stop();
        HideExtractionDashboardSecurityStatus();

        _dropZoneVisibilityReset?.Dispose();
        _dropZoneVisibilityReset = null;

        if (_extractionDashboard is null)
        {
            SetDropZoneSectionVisibility(true);
            return;
        }

        _extractionDashboardHideReset?.Dispose();
        _extractionDashboardHideReset = DispatcherTimer.RunOnce(() =>
        {
            if (_extractionDashboard is null)
            {
                SetDropZoneSectionVisibility(true);
                return;
            }

            _extractionDashboard.Opacity = 0;
            _dropZoneVisibilityReset?.Dispose();
            _dropZoneVisibilityReset = DispatcherTimer.RunOnce(() =>
            {
                if (_extractionDashboard is not null)
                    _extractionDashboard.IsVisible = false;

                SetDropZoneSectionVisibility(true);

                _dropZoneVisibilityReset?.Dispose();
                _dropZoneVisibilityReset = null;

                _extractionDashboardHideReset?.Dispose();
                _extractionDashboardHideReset = null;
            }, TimeSpan.FromMilliseconds(280));
        }, delay);
    }

    private void UpdateExtractionDashboardSubtitle(string text)
    {
        if (_extractionDashboardSubtitle is not null)
            _extractionDashboardSubtitle.Text = text;
    }

    private void UpdateExtractionDashboardQueueBadge(int pendingCount)
    {
        if (_extractionDashboardQueueCount is null)
            return;

        _extractionDashboardQueueCount.Text = pendingCount > 0
            ? $"Queue: {pendingCount}"
            : "Queue clear";
    }

    private void UpdateExtractionDashboardPackageText(string text)
    {
        if (_extractionDashboardPackageText is not null)
            _extractionDashboardPackageText.Text = text;
    }

    private void UpdateExtractionDashboardAsset(string text)
    {
        if (_extractionDashboardAssetText is not null)
            _extractionDashboardAssetText.Text = text;
    }

    private void UpdateExtractionDashboardOutput(string text)
    {
        if (_extractionDashboardOutputText is null)
            return;

        _extractionDashboardOutputText.Text = string.IsNullOrWhiteSpace(text)
            ? "—"
            : text;
    }

    private void UpdateExtractionDashboardAssetCount(int value)
    {
        if (_extractionDashboardAssetCount is not null)
            _extractionDashboardAssetCount.Text = value.ToString("N0", CultureInfo.CurrentCulture);
    }

    private void UpdateExtractionDashboardElapsedText(string text)
    {
        if (_extractionDashboardElapsed is not null)
            _extractionDashboardElapsed.Text = text;
    }

    private void UpdateExtractionDashboardNextPackageText(string? nextPackage)
    {
        if (_extractionDashboardNextPackage is null)
            return;

        _extractionDashboardNextPackage.Text = string.IsNullOrWhiteSpace(nextPackage)
            ? "All caught up"
            : nextPackage!;
    }

    private string ResolveNextPackageName(IReadOnlyList<ExtractionItem> items, int currentIndex)
    {
        var nextIndex = currentIndex + 1;
        if (nextIndex >= 0 && nextIndex < items.Count)
            return Path.GetFileName(items[nextIndex].Path);

        var nextQueued = _queueItems.FirstOrDefault(display => !display.IsExtracting);
        return nextQueued?.FileName ?? "All caught up";
    }

    private void OnExtractionElapsedTick(object? sender, EventArgs e)
    {
        if (_extractionStopwatch is null || !_extractionStopwatch.IsRunning)
            return;

        UpdateExtractionDashboardElapsedText(FormatElapsed(_extractionStopwatch.Elapsed));
    }

    private void BeginExtractionOverviewSession()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(BeginExtractionOverviewSession);
            return;
        }

        _extractionOverviewPackageBaseline = Math.Max(0, _settings.TotalExtracted);
        _extractionOverviewAssetBaseline = Math.Max(0, _settings.TotalFilesExtracted);
        _extractionOverviewSessionPackages = 0;
        _extractionOverviewSessionAssets = 0;
        _extractionOverviewCurrentPackageAssets = 0;
        _extractionOverviewStartTime = DateTimeOffset.Now;
        _isExtractionOverviewLive = true;
        UpdateExtractionOverviewStatusInProgress();
        UpdateExtractionOverviewLiveCounts();
    }

    private void OnExtractionOverviewPackageStarted()
    {
        if (!_isExtractionOverviewLive)
            return;

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(OnExtractionOverviewPackageStarted);
            return;
        }

        _extractionOverviewCurrentPackageAssets = 0;
        UpdateExtractionOverviewLiveCounts();
    }

    private void UpdateExtractionOverviewProgress(int assetsExtracted)
    {
        if (!_isExtractionOverviewLive)
            return;

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => UpdateExtractionOverviewProgress(assetsExtracted));
            return;
        }

        var sanitized = Math.Max(0, assetsExtracted);
        if (sanitized <= _extractionOverviewCurrentPackageAssets)
            return;

        var delta = sanitized - _extractionOverviewCurrentPackageAssets;
        _extractionOverviewCurrentPackageAssets = sanitized;
        _extractionOverviewSessionAssets += delta;
        UpdateExtractionOverviewLiveCounts();
    }

    private void OnExtractionOverviewPackageCompleted(int assetsExtracted)
    {
        if (!_isExtractionOverviewLive)
            return;

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnExtractionOverviewPackageCompleted(assetsExtracted));
            return;
        }

        var sanitized = Math.Max(0, assetsExtracted);
        if (sanitized > _extractionOverviewCurrentPackageAssets)
        {
            _extractionOverviewSessionAssets += sanitized - _extractionOverviewCurrentPackageAssets;
            _extractionOverviewCurrentPackageAssets = sanitized;
        }

        _extractionOverviewSessionPackages++;
        UpdateExtractionOverviewLiveCounts();
    }

    private void UpdateExtractionOverviewLiveCounts()
    {
        if (!_isExtractionOverviewLive)
            return;

        var packages = _extractionOverviewPackageBaseline + _extractionOverviewSessionPackages;
        var assets = _extractionOverviewAssetBaseline + _extractionOverviewSessionAssets;
        UpdateExtractionOverviewCounts(packages, assets);
    }

    private void UpdateExtractionOverviewCounts(int packages, int assets)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => UpdateExtractionOverviewCounts(packages, assets));
            return;
        }

        if (_extractionOverviewPackagesCompletedText is not null)
            _extractionOverviewPackagesCompletedText.Text = packages.ToString("N0", CultureInfo.CurrentCulture);

        if (_extractionOverviewAssetsExtractedText is not null)
            _extractionOverviewAssetsExtractedText.Text = assets.ToString("N0", CultureInfo.CurrentCulture);
    }

    private void UpdateExtractionOverviewStatus(string text)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => UpdateExtractionOverviewStatus(text));
            return;
        }

        if (_extractionOverviewLastExtractionText is not null)
            _extractionOverviewLastExtractionText.Text = text;
    }

    private void UpdateExtractionOverviewStatusInProgress()
    {
        var statusText = _extractionOverviewStartTime.HasValue
            ? $"Started at {_extractionOverviewStartTime.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)}"
            : "In progress�";

        UpdateExtractionOverviewStatus(statusText);
    }

    private void UpdateExtractionOverviewDisplay()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(UpdateExtractionOverviewDisplay);
            return;
        }

        var packages = Math.Max(0, _settings.TotalExtracted);
        var assets = Math.Max(0, _settings.TotalFilesExtracted);
        UpdateExtractionOverviewCounts(packages, assets);

        if (_extractionOverviewLastExtractionText is null)
            return;

        _extractionOverviewLastExtractionText.Text = _settings.LastExtractionTime.HasValue
            ? _settings.LastExtractionTime.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
            : "Not started";
    }

    private void EndExtractionOverviewSession()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(EndExtractionOverviewSession);
            return;
        }

        _isExtractionOverviewLive = false;
        _extractionOverviewSessionPackages = 0;
        _extractionOverviewSessionAssets = 0;
        _extractionOverviewCurrentPackageAssets = 0;
        _extractionOverviewStartTime = null;
        UpdateExtractionOverviewDisplay();
    }


    private IBrush ResolveBrushResource(string resourceKey, IBrush fallback)
    {
        if (Application.Current?.TryFindResource(resourceKey, out var resource) == true &&
            resource is IBrush brush)
            return brush;

        return fallback;
    }


    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.FromSeconds(1))
            return $"{elapsed.TotalMilliseconds:0} ms";

        if (elapsed < TimeSpan.FromMinutes(1))
            return $"{elapsed.TotalSeconds:0}s";

        if (elapsed < TimeSpan.FromHours(1))
            return $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds:D2}s";

        return $"{(int)elapsed.TotalHours}h {elapsed.Minutes:D2}m";
    }


    private readonly record struct ExtractionItem(string Path, UnityPackageFile? QueueEntry);

    private enum SecurityBannerVisualState
    {
        Warning,
        Info
    }

}

