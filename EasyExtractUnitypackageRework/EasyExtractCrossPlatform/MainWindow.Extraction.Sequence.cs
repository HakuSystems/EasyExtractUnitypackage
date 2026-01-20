namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
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
                    if (securityResult?.IsMalicious == true)
                    {
                        // BLOCKING UI: Wait for user to Confirm or Cancel
                        var forceExtract = await WaitForSecurityClearanceAsync(normalizedPackagePath, securityResult);
                        if (!forceExtract)
                        {
                            ShowDropStatusMessage("Extraction Blocked", "Malicious package skipped by user.",
                                TimeSpan.FromSeconds(4), UiSoundEffect.Subtle);

                            await Dispatcher.UIThread.InvokeAsync(() =>
                                CompleteCurrentPackageOnDashboard(
                                    packagePath,
                                    false,
                                    0,
                                    true, // treat as cancelled
                                    Math.Max(0, validItems.Count - index - 1),
                                    ResolveNextPackageName(validItems, index)));

                            continue;
                        }

                        // User chose to Force Extract -> Proceed (maybe log this explicitly)
                        ShowDropStatusMessage("Risk Accepted", "Proceeding with extraction...", TimeSpan.FromSeconds(2),
                            UiSoundEffect.Negative);
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
            RestorePresenceAfterOverlay();
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
}