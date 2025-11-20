namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private bool IsSecurityScanningEnabled => _settings?.EnableSecurityScanning ?? false;

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

    private Dictionary<string, HistoryEntry> BuildHistoryLookup()
    {
        var lookup = new Dictionary<string, HistoryEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _settings.History)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.FilePath))
                continue;

            var normalized = HistoryEntry.NormalizePathSafe(entry.FilePath) ?? entry.FilePath;
            entry.FilePath = normalized;

            if (!lookup.ContainsKey(normalized))
                lookup[normalized] = entry;
        }

        return lookup;
    }

    private HistoryEntry TrackHistoryEntry(
        IDictionary<string, HistoryEntry> historyLookup,
        FileInfo fileInfo,
        string normalizedPath)
    {
        var timestamp = DateTimeOffset.UtcNow;
        if (historyLookup.TryGetValue(normalizedPath, out var existing) && existing is not null)
        {
            existing.Touch(fileInfo, timestamp);
            return existing;
        }

        var entry = HistoryEntry.Create(fileInfo, normalizedPath, timestamp);
        historyLookup[normalizedPath] = entry;
        _settings.History.Add(entry);
        TrimHistoryEntries();
        return entry;
    }

    private void TrimHistoryEntries()
    {
        const int maxEntries = 512;
        if (_settings.History.Count <= maxEntries)
            return;

        var ordered = _settings.History
            .Where(entry => entry is not null)
            .OrderBy(entry => entry.AddedUtc)
            .ToList();

        while (ordered.Count > maxEntries)
        {
            var toRemove = ordered[0];
            ordered.RemoveAt(0);
            _settings.History.Remove(toRemove);
        }
    }

    private void UpdateHistoryAfterExtraction(
        string packagePath,
        UnityPackageExtractionResult result,
        TimeSpan duration,
        string outputDirectory)
    {
        var normalizedPath = TryNormalizeFilePath(packagePath) ?? packagePath;
        var historyLookup = BuildHistoryLookup();
        var fileInfo = new FileInfo(packagePath);
        var historyEntry = historyLookup.TryGetValue(normalizedPath, out var existing) && existing is not null
            ? existing
            : TrackHistoryEntry(historyLookup, fileInfo, normalizedPath);

        var extractedBytes = 0L;
        if (result.ExtractedFiles is { Count: > 0 })
            foreach (var extractedFile in result.ExtractedFiles)
            {
                if (string.IsNullOrWhiteSpace(extractedFile))
                    continue;

                try
                {
                    var info = new FileInfo(extractedFile);
                    if (info.Exists)
                        extractedBytes += info.Length;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to measure extracted file '{extractedFile}': {ex}");
                }
            }

        historyEntry.CaptureExtractionSnapshot(
            result.AssetsExtracted,
            result.ExtractedFiles?.Count ?? 0,
            extractedBytes,
            duration,
            outputDirectory,
            DateTimeOffset.UtcNow);
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

    private void RefreshQueueSecurityIndicators()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RefreshQueueSecurityIndicators);
            return;
        }

        if (!IsSecurityScanningEnabled)
        {
            foreach (var item in _queueItems)
                item.ClearSecurityIndicators();

            lock (_securityScanGate)
            {
                _securityScanResults.Clear();
                _securityScanTasks.Clear();
            }

            _securityWarningsShown.Clear();
            _securityInfoNotified.Clear();
            HideExtractionDashboardSecurityStatus();
            return;
        }

        foreach (var kvp in _queueItemsByPath)
        {
            var key = kvp.Key;
            if (string.IsNullOrWhiteSpace(key))
                continue;

            lock (_securityScanGate)
            {
                _securityScanResults.Remove(key);
                _securityScanTasks.Remove(key);
            }

            kvp.Value.ClearSecurityIndicators();
            StartSecurityScanForPackage(key);
        }
    }

    private void AddOrUpdateQueueDisplayItem(UnityPackageFile package, string? normalizedPath = null)
    {
        if (package is null)
            return;

        var key = !string.IsNullOrWhiteSpace(normalizedPath)
            ? normalizedPath!
            : TryNormalizeFilePath(package.FilePath);

        if (string.IsNullOrWhiteSpace(key))
            return;

        if (_queueItemsByPath.TryGetValue(key, out var existing))
        {
            existing.UpdateFrom(package);
            ApplySecurityResultToDisplay(key, existing);
        }
        else
        {
            var display = new QueueItemDisplay(package, key);
            _queueItems.Add(display);
            _queueItemsByPath[key] = display;
            ApplySecurityResultToDisplay(key, display);
        }

        StartSecurityScanForPackage(key);
    }

    private void ApplySecurityResultToDisplay(string normalizedPath, QueueItemDisplay display)
    {
        if (string.IsNullOrWhiteSpace(normalizedPath))
            return;

        if (!IsSecurityScanningEnabled)
        {
            display.ClearSecurityIndicators();
            return;
        }

        MaliciousCodeScanResult? result;
        lock (_securityScanGate)
        {
            _securityScanResults.TryGetValue(normalizedPath, out result);
        }

        if (result is null)
            return;

        if (result.IsMalicious)
            display.SetSecurityWarning(BuildSecuritySummaryText(result));
        else
            display.ClearSecurityIndicators();
    }

    private void StartSecurityScanForPackage(string normalizedPath)
    {
        if (string.IsNullOrWhiteSpace(normalizedPath))
            return;

        if (!IsSecurityScanningEnabled)
        {
            if (_queueItemsByPath.TryGetValue(normalizedPath, out var disabledDisplay))
                disabledDisplay.ClearSecurityIndicators();
            return;
        }

        if (!File.Exists(normalizedPath))
            return;

        _ = EnsureSecurityScanTaskAsync(normalizedPath);
    }

    private Task<MaliciousCodeScanResult?> EnsureSecurityScanTaskAsync(string normalizedPath)
    {
        if (string.IsNullOrWhiteSpace(normalizedPath))
            return Task.FromResult<MaliciousCodeScanResult?>(null);

        if (!IsSecurityScanningEnabled)
            return Task.FromResult<MaliciousCodeScanResult?>(null);

        lock (_securityScanGate)
        {
            if (_securityScanResults.TryGetValue(normalizedPath, out var cached))
                return Task.FromResult<MaliciousCodeScanResult?>(cached);

            if (_securityScanTasks.TryGetValue(normalizedPath, out var existingTask))
                return existingTask;

            var scanTask = PerformSecurityScanAsync(normalizedPath);
            _securityScanTasks[normalizedPath] = scanTask;
            return scanTask;
        }
    }

    private async Task<MaliciousCodeScanResult?> PerformSecurityScanAsync(string normalizedPath)
    {
        if (!IsSecurityScanningEnabled)
            return null;

        try
        {
            var result = await _maliciousCodeDetectionService.ScanUnityPackageAsync(normalizedPath)
                .ConfigureAwait(false);

            if (!IsSecurityScanningEnabled)
                return null;

            lock (_securityScanGate)
            {
                _securityScanResults[normalizedPath] = result;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_queueItemsByPath.TryGetValue(normalizedPath, out var display))
                    ApplySecurityResultToDisplay(normalizedPath, display);
            });

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Security scan failed for '{normalizedPath}'.", ex);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!IsSecurityScanningEnabled)
                    return;

                if (_queueItemsByPath.TryGetValue(normalizedPath, out var display))
                    display.SetSecurityInfo("Security scan failed");
            });

            return null;
        }
        finally
        {
            lock (_securityScanGate)
            {
                _securityScanTasks.Remove(normalizedPath);
            }
        }
    }

    private static string BuildSecuritySummaryText(MaliciousCodeScanResult result)
    {
        if (result is not { Threats.Count: > 0 })
            return "Potentially malicious content detected.";

        var parts = result.Threats.Select(threat =>
        {
            var label = threat.Type switch
            {
                MaliciousThreatType.DiscordWebhook => "Discord webhook",
                MaliciousThreatType.UnsafeLinks => "Unsafe links",
                MaliciousThreatType.SuspiciousCodePatterns => "Suspicious code patterns",
                _ => threat.Type.ToString()
            };

            return threat.Matches is { Count: > 0 }
                ? $"{label} ({threat.Matches.Count})"
                : label;
        });

        return string.Join(", ", parts);
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


    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0)
            return "0 B";

        var units = new[] { "B", "KB", "MB", "GB", "TB", "PB" };
        var size = (double)bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{bytes} {units[unitIndex]}"
            : $"{size:0.##} {units[unitIndex]}";
    }


    private sealed class QueueItemDisplay : INotifyPropertyChanged
    {
        private bool _isExtracting;
        private string? _securityInfoText;
        private string? _securityWarningText;

        public QueueItemDisplay(UnityPackageFile source, string normalizedPath)
        {
            NormalizedPath = normalizedPath;
            FilePath = string.IsNullOrWhiteSpace(source.FilePath) ? normalizedPath : source.FilePath;
            FileName = string.IsNullOrWhiteSpace(source.FileName)
                ? Path.GetFileName(FilePath)
                : source.FileName;
            FileSizeBytes = ParseFileSize(source.FileSize);
            LastUpdated = ParseLastUpdated(source.FileDate);
            UpdateFrom(source);
        }

        public string NormalizedPath { get; }

        public string FilePath { get; }

        public string FileName { get; }

        public long FileSizeBytes { get; }

        public DateTimeOffset? LastUpdated { get; }

        public string SizeText => FormatFileSize(FileSizeBytes);

        public string StatusText => IsExtracting ? "Extracting..." : "Queued";

        public string LocationText => string.IsNullOrWhiteSpace(FilePath)
            ? "Location unavailable"
            : Path.GetDirectoryName(FilePath) is { Length: > 0 } directory
                ? directory
                : FilePath;

        public bool HasSecurityWarning => !string.IsNullOrWhiteSpace(SecurityWarningText);

        public string? SecurityWarningText
        {
            get => _securityWarningText;
            private set
            {
                if (_securityWarningText == value)
                    return;

                _securityWarningText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSecurityWarning));
            }
        }

        public bool HasSecurityInfo => !string.IsNullOrWhiteSpace(SecurityInfoText);

        public string? SecurityInfoText
        {
            get => _securityInfoText;
            private set
            {
                if (_securityInfoText == value)
                    return;

                _securityInfoText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSecurityInfo));
            }
        }

        public bool IsExtracting
        {
            get => _isExtracting;
            private set
            {
                if (_isExtracting == value)
                    return;

                _isExtracting = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void UpdateFrom(UnityPackageFile source)
        {
            IsExtracting = source.IsExtracting;
        }

        public void SetSecurityWarning(string? text)
        {
            SecurityWarningText = string.IsNullOrWhiteSpace(text) ? null : text;
            if (!string.IsNullOrWhiteSpace(SecurityWarningText))
                SecurityInfoText = null;
        }

        public void SetSecurityInfo(string? text)
        {
            SecurityInfoText = string.IsNullOrWhiteSpace(text) ? null : text;
            if (!string.IsNullOrWhiteSpace(SecurityInfoText))
                SecurityWarningText = null;
        }

        public void ClearSecurityIndicators()
        {
            if (_securityWarningText is null && _securityInfoText is null)
                return;

            _securityWarningText = null;
            _securityInfoText = null;
            OnPropertyChanged(nameof(SecurityWarningText));
            OnPropertyChanged(nameof(SecurityInfoText));
            OnPropertyChanged(nameof(HasSecurityWarning));
            OnPropertyChanged(nameof(HasSecurityInfo));
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static long ParseFileSize(string? input)
        {
            if (long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 0)
                return value;

            return 0;
        }

        private static DateTimeOffset? ParseLastUpdated(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            if (DateTimeOffset.TryParse(
                    input,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var result))
                return result;

            if (DateTimeOffset.TryParse(input, CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal,
                    out var fallback))
                return fallback;

            return null;
        }
    }

    private readonly record struct QueueResult(int AddedCount, int AlreadyQueuedCount);
}

