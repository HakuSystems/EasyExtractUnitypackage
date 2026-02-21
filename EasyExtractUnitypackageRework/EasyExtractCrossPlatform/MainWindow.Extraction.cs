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

    private void OpenOutputFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var path = _settings.DefaultOutputPath;
            if (string.IsNullOrWhiteSpace(path))
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "EasyExtractUnitypackage", "Extracted");

            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open output folder: {ex}");
            ShowDropStatusMessage(
                "Error opening folder",
                "Could not open the output folder.",
                TimeSpan.FromSeconds(4),
                UiSoundEffect.Negative);
        }
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
}