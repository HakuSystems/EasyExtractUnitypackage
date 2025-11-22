namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private QueueResult QueueUnityPackages(IReadOnlyList<string> packagePaths)
    {
        if (packagePaths.Count == 0)
            return new QueueResult(0, 0);

        var addedCount = 0;
        var alreadyQueuedCount = 0;
        var newlyAddedPackages = new List<(UnityPackageFile Package, string NormalizedPath)>();

        var existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in _settings.UnitypackageFiles)
        {
            if (string.IsNullOrWhiteSpace(package.FilePath))
                continue;

            try
            {
                existingPaths.Add(Path.GetFullPath(package.FilePath));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to normalize queued package path '{package.FilePath}': {ex}");
            }
        }

        var historyLookup = BuildHistoryLookup();

        foreach (var path in packagePaths)
        {
            string normalizedPath;
            try
            {
                normalizedPath = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to normalize dropped path '{path}': {ex}");
                continue;
            }

            if (!File.Exists(normalizedPath))
                continue;

            if (!existingPaths.Add(normalizedPath))
            {
                alreadyQueuedCount++;
                continue;
            }

            var fileInfo = new FileInfo(normalizedPath);
            var queueEntry = new UnityPackageFile
            {
                FileName = fileInfo.Name,
                FilePath = normalizedPath,
                FileExtension = fileInfo.Extension,
                FileSize = fileInfo.Exists
                    ? fileInfo.Length.ToString(CultureInfo.InvariantCulture)
                    : string.Empty,
                FileDate = fileInfo.Exists
                    ? fileInfo.LastWriteTimeUtc.ToString("u", CultureInfo.InvariantCulture)
                    : string.Empty,
                IsInQueue = true,
                IsExtracting = false
            };

            _settings.UnitypackageFiles.Add(queueEntry);
            newlyAddedPackages.Add((queueEntry, normalizedPath));

            TrackHistoryEntry(historyLookup, fileInfo, normalizedPath);

            addedCount++;
        }

        if (newlyAddedPackages.Count > 0)
            foreach (var (package, normalizedPath) in newlyAddedPackages)
                AddOrUpdateQueueDisplayItem(package, normalizedPath);

        UpdateQueueVisualState();

        if (addedCount > 0)
            AppSettingsService.Save(_settings);

        return new QueueResult(addedCount, alreadyQueuedCount);
    }

    private void ClearQueue()
    {
        var modified = false;

        if (_settings.UnitypackageFiles is { Count: > 0 })
        {
            _settings.UnitypackageFiles.Clear();
            modified = true;
        }

        if (_queueItems.Count > 0)
        {
            _queueItems.Clear();
            modified = true;
        }

        if (_queueItemsByPath.Count > 0)
            _queueItemsByPath.Clear();

        UpdateQueueVisualState();

        if (!modified)
            return;

        ShowDropStatusMessage(
            "Queue cleared",
            "Drop or search for .unitypackage files to add new items.",
            TimeSpan.FromSeconds(3),
            UiSoundEffect.Subtle);
        AppSettingsService.Save(_settings);
    }

    private void ReloadQueueFromSettings()
    {
        _queueItems.Clear();
        _queueItemsByPath.Clear();

        var queuedPackages = _settings.UnitypackageFiles;
        if (queuedPackages is not null)
            foreach (var package in queuedPackages)
            {
                if (package is null || !package.IsInQueue)
                    continue;

                AddOrUpdateQueueDisplayItem(package);
            }

        UpdateQueueVisualState();
        RefreshQueueSecurityIndicators();
    }

    private void RemoveQueueDisplayItem(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        if (_queueItemsByPath.TryGetValue(key, out var display))
        {
            _queueItems.Remove(display);
            _queueItemsByPath.Remove(key);
        }
    }

    private void UpdateQueueVisualState()
    {
        var itemCount = _queueItems.Count;

        if (_queueSummaryTextBlock is not null)
            _queueSummaryTextBlock.Text = itemCount switch
            {
                0 => "Queue is empty",
                1 => "1 package queued",
                _ => $"{itemCount} packages queued"
            };

        if (_queueEmptyState is not null)
            _queueEmptyState.IsVisible = itemCount == 0;

        if (_queueItemsScrollViewer is not null)
            _queueItemsScrollViewer.IsVisible = itemCount > 0;

        UpdateExtractionButtonsState();

        if (_isExtractionRunning)
            return;

        var nextPackage = itemCount > 0 ? GetNextQueuedPackageName() : null;
        QueueDiscordPresenceUpdate(
            itemCount > 0 ? "Queue ready" : "Dashboard",
            nextPackage: nextPackage,
            queueCountOverride: itemCount);
    }

    private static string? TryResolveLocalPath(IStorageFile storageFile)
    {
        if (storageFile is null)
            return null;

        return storageFile.Path?.LocalPath;
    }

    private static string TryNormalizeFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    private readonly record struct QueueResult(int AddedCount, int AlreadyQueuedCount);
}