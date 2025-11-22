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