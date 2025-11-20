namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private bool IsSecurityScanningEnabled => _settings?.EnableSecurityScanning ?? false;

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
}
