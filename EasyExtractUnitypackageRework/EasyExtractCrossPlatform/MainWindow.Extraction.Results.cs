namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
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
        if (_settings.EnableNotifications)
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
            InvalidDataException invalid when !string.IsNullOrWhiteSpace(invalid.Message)
                => invalid.Message,
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
            _settings.ExtractedUnitypackages = new List<ExtractedPackageModel>();

        if (_settings.ExtractedUnitypackages.All(existing =>
                !string.Equals(existing.FilePath, packagePath, StringComparison.OrdinalIgnoreCase)))
            _settings.ExtractedUnitypackages.Add(new ExtractedPackageModel
            {
                FileName = Path.GetFileName(packagePath),
                FilePath = packagePath,
                DateExtracted = DateTimeOffset.Now
            });

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
}