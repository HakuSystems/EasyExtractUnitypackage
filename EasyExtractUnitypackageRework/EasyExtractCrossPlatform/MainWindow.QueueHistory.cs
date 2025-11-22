namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
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
}