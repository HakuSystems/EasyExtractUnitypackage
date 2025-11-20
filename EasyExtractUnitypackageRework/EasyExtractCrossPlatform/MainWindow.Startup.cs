namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private void InitializeStartupExtractionTargets()
    {
        if (_startupArguments.Length == 0)
            return;

        var targets = ResolveStartupExtractionTargets(_startupArguments);
        if (targets.Count == 0)
            return;

        _pendingStartupExtractions = targets;
    }

    private async void OnMainWindowOpened(object? sender, EventArgs e)
    {
        Opened -= OnMainWindowOpened;

        if (_pendingStartupExtractions is null || _pendingStartupExtractions.Count == 0)
            return;

        var targets = _pendingStartupExtractions;
        _pendingStartupExtractions = null;

        try
        {
            QueueUnityPackages(targets);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to queue startup packages: {ex}");
            return;
        }

        var extractionItems = BuildExtractionItems(targets);
        if (extractionItems.Count == 0)
            return;

        try
        {
            await RunExtractionSequenceAsync(extractionItems);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Startup extraction failed: {ex}");
        }
    }

    private List<ExtractionItem> BuildExtractionItems(IEnumerable<string> targets)
    {
        var items = new List<ExtractionItem>();

        foreach (var target in targets)
        {
            var normalized = TryNormalizeFilePath(target);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            if (!File.Exists(normalized))
                continue;

            if (!IsUnityPackage(normalized))
                continue;

            var queueEntry = FindQueueEntryByPath(normalized);
            items.Add(new ExtractionItem(normalized, queueEntry));
        }

        return items;
    }

    private UnityPackageFile? FindQueueEntryByPath(string normalizedPath)
    {
        if (_settings.UnitypackageFiles is not { Count: > 0 })
            return null;

        foreach (var entry in _settings.UnitypackageFiles)
        {
            if (entry is null)
                continue;

            var entryPath = TryNormalizeFilePath(entry.FilePath);
            if (string.IsNullOrWhiteSpace(entryPath))
                continue;

            if (string.Equals(entryPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        return null;
    }

    private static List<string> ResolveStartupExtractionTargets(IReadOnlyList<string> arguments)
    {
        var results = new List<string>();
        var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var capturePaths = false;

        foreach (var argument in arguments)
        {
            if (string.IsNullOrWhiteSpace(argument))
                continue;

            if (IsExtractFlag(argument))
            {
                capturePaths = true;
                continue;
            }

            if (!capturePaths && !IsUnityPackage(argument))
                continue;

            var normalized = TryNormalizeFilePath(argument);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            if (!File.Exists(normalized))
                continue;

            if (!IsUnityPackage(normalized))
                continue;

            if (uniquePaths.Add(normalized))
                results.Add(normalized);
        }

        return results;
    }

    private static bool IsExtractFlag(string argument)
    {
        return argument.Equals("--extract", StringComparison.OrdinalIgnoreCase) ||
               argument.Equals("-extract", StringComparison.OrdinalIgnoreCase) ||
               argument.Equals("/extract", StringComparison.OrdinalIgnoreCase);
    }

}

