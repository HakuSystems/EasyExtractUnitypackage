using Avalonia.Layout;

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

            // Prevent UnobservedTaskException if this task is never awaited
            // and an OperationCanceledException (or other fault) is thrown.
            _ = scanTask.ContinueWith(
                t => _ = t.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

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
            var result = await _maliciousCodeDetectionService.ScanPackageAsync(normalizedPath)
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
        catch (InvalidDataException ex)
        {
            LoggingService.LogWarning($"Security scan skipped for '{normalizedPath}': {ex.Message}", ex);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!IsSecurityScanningEnabled)
                    return;

                if (_queueItemsByPath.TryGetValue(normalizedPath, out var display))
                    display.SetSecurityInfo(ex.Message);
            });

            return null;
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

    private async Task<bool> WaitForSecurityClearanceAsync(string packagePath, MaliciousCodeScanResult result)
    {
        var tcs = new TaskCompletionSource<bool>();

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var warningText = BuildSecuritySummaryText(result);

            var panel = new StackPanel
            {
                Spacing = 20,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 500
            };

            panel.Children.Add(new TextBlock
            {
                Text = "⚠️ SECURITY THREAT DETECTED",
                FontSize = 24,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.Red,
                TextAlignment = TextAlignment.Center
            });

            panel.Children.Add(new TextBlock
            {
                Text = $"The package '{Path.GetFileName(packagePath)}' contains signatures matching malicious code.",
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            });

            panel.Children.Add(new TextBlock
            {
                Text = warningText,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.OrangeRed,
                TextAlignment = TextAlignment.Center
            });
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 20,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var cancelButton = new Button
            {
                Content = "CANCEL (Recommended)",
                Padding = new Thickness(20, 10),
                Background = Brushes.DarkGreen // visual cue for safe option
            };
            cancelButton.Click += async (_, _) =>
            {
                tcs.TrySetResult(false);
                await CloseOverlayAsync();
            };

            var forceButton = new Button
            {
                Content = "Force Extract (Danger)",
                Foreground = Brushes.White,
                Background = Brushes.Red,
                FontWeight = FontWeight.Bold,
                Padding = new Thickness(20, 10),
                Margin = new Thickness(20, 0, 0, 0)
            };
            forceButton.Click += async (_, _) =>
            {
                tcs.TrySetResult(true);
                await CloseOverlayAsync();
            };

            buttons.Children.Add(cancelButton);
            buttons.Children.Add(forceButton);
            panel.Children.Add(buttons);

            var card = new Border
            {
                Background = Brushes.Black,
                BorderBrush = Brushes.Red,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(30),
                Child = panel
            };

            await ShowOverlayAsync(card);
        });

        return await tcs.Task;
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