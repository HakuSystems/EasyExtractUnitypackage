namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private void QueueDiscordPresenceUpdate(
        string state,
        string? detailsOverride = null,
        string? currentPackage = null,
        string? nextPackage = null,
        bool extractionActive = false,
        int? queueCountOverride = null,
        AppSettings? settingsOverride = null)
    {
        var targetSettings = settingsOverride ?? _settings;
        if (targetSettings is null)
            return;

        if (!targetSettings.DiscordRpc)
        {
            if (_lastDiscordPresenceEnabled)
            {
                _ = DiscordRpcService.Instance.UpdatePresenceAsync(targetSettings, DiscordPresenceContext.Disabled());
                _lastDiscordPresenceEnabled = false;
            }

            return;
        }

        _lastDiscordPresenceEnabled = true;

        var queueCount = Math.Max(0, queueCountOverride ?? GetActiveQueueCount());
        var normalizedState = string.IsNullOrWhiteSpace(state) ? "Dashboard" : state.Trim();
        var normalizedCurrentPackage = NormalizePackageLabel(currentPackage);
        var normalizedNextPackage = NormalizePackageLabel(nextPackage);
        var details = string.IsNullOrWhiteSpace(detailsOverride)
            ? BuildPresenceDetails(normalizedState, queueCount, extractionActive, normalizedCurrentPackage,
                normalizedNextPackage)
            : detailsOverride.Trim();

        var context = new DiscordPresenceContext(
            normalizedState,
            details,
            normalizedCurrentPackage,
            normalizedNextPackage,
            queueCount,
            extractionActive);

        _ = DiscordRpcService.Instance.UpdatePresenceAsync(targetSettings, context);
    }

    private static string ResolveOverlayPresenceState(Control view)
    {
        return view.GetType().Name;
    }

    private static string ResolveOverlayPresenceDetail(Control view)
    {
        return "Exploring EasyExtract";
    }

    private int GetActiveQueueCount()
    {
        if (_queueItems.Count == 0)
            return 0;

        var queued = _queueItems.Count(display => !display.IsExtracting);
        return Math.Max(0, queued);
    }

    private string? GetNextQueuedPackageName()
    {
        var next = _queueItems.FirstOrDefault(display => !display.IsExtracting);
        if (next is not null)
            return next.FileName;

        return _queueItems.FirstOrDefault()?.FileName;
    }

    private static string BuildPresenceDetails(
        string state,
        int queueCount,
        bool extractionActive,
        string? currentPackage,
        string? nextPackage)
    {
        if (extractionActive)
        {
            if (!string.IsNullOrWhiteSpace(currentPackage))
                return queueCount > 0
                    ? $"Extracting {currentPackage} - {queueCount} remaining"
                    : $"Finishing {currentPackage}";

            return queueCount > 0
                ? $"Processing extraction queue - {queueCount} remaining"
                : "Processing extraction queue";
        }

        if (string.Equals(state, "Settings", StringComparison.OrdinalIgnoreCase))
            return "Tuning EasyExtract preferences";

        if (string.Equals(state, "Feedback", StringComparison.OrdinalIgnoreCase))
            return "Sharing feedback with the team";

        if (queueCount > 0)
        {
            if (!string.IsNullOrWhiteSpace(nextPackage) &&
                !string.Equals(nextPackage, "All caught up", StringComparison.OrdinalIgnoreCase))
                return $"Queue ready - Next: {nextPackage}";

            return queueCount == 1
                ? "1 package queued"
                : $"{queueCount} packages queued";
        }

        return string.Equals(state, "Dashboard", StringComparison.OrdinalIgnoreCase)
            ? "Ready to extract Unitypackages"
            : "Exploring EasyExtract";
    }

    private void RestorePresenceAfterOverlay()
    {
        if (_isExtractionRunning)
        {
            var currentPackage = NormalizePackageLabel(_extractionDashboardPackageText?.Text);
            var nextPackage = NormalizePackageLabel(_extractionDashboardNextPackage?.Text);
            var remaining = Math.Max(0, GetActiveQueueCount());

            QueueDiscordPresenceUpdate(
                string.IsNullOrWhiteSpace(currentPackage) ? "Extraction in progress" : $"Extracting {currentPackage}",
                currentPackage: currentPackage,
                nextPackage: nextPackage,
                extractionActive: true,
                queueCountOverride: remaining);
            return;
        }

        var queueCount = GetActiveQueueCount();
        var nextQueued = queueCount > 0 ? GetNextQueuedPackageName() : null;
        QueueDiscordPresenceUpdate(
            queueCount > 0 ? "Queue ready" : "Dashboard",
            nextPackage: nextQueued,
            queueCountOverride: queueCount);
    }

    private static string? NormalizePackageLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed == "-" || string.Equals(trimmed, "All caught up", StringComparison.OrdinalIgnoreCase))
            return null;

        return trimmed;
    }
}