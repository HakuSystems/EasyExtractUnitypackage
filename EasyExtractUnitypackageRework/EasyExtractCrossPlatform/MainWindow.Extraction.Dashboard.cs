namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
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


}
