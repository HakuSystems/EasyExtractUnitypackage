namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private async void CheckUpdatesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_isCheckingForUpdates || _isUpdateDownloadInProgress)
            return;

        if (_isExtractionRunning)
        {
            ShowDropStatusMessage(
                "Extraction in progress",
                "Finish the current extraction before installing an update.",
                TimeSpan.FromSeconds(6),
                UiSoundEffect.Subtle);
            return;
        }

        _isCheckingForUpdates = true;

        var button = _checkUpdatesButton ?? sender as Button;
        if (button is not null)
        {
            _checkUpdatesButtonOriginalContent ??= button.Content;
            button.IsEnabled = false;
            button.Content = "Checking...";
        }

        await Dispatcher.UIThread.InvokeAsync(() => SetVersionStatusMessage("Checking for updates..."));

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        try
        {
            var currentVersion = ResolveCurrentVersionForUpdates();
            var settings = _settings.Update ?? new UpdateSettings();
            var result = await _updateService
                .CheckForUpdatesAsync(settings, currentVersion, cts.Token)
                .ConfigureAwait(false);

            if (!result.IsUpdateAvailable || result.Manifest is null)
            {
                var message = string.IsNullOrWhiteSpace(result.Message) ? "You're up to date" : result.Message!;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SetVersionStatusMessage(message, TimeSpan.FromSeconds(6));
                    ShowDropStatusMessage(
                        "No updates available",
                        message,
                        TimeSpan.FromSeconds(6),
                        UiSoundEffect.Subtle);
                });
                return;
            }

            await HandleUpdateAvailabilityAsync(result.Manifest, false, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SetVersionStatusMessage("Update check cancelled", TimeSpan.FromSeconds(6));
                ShowDropStatusMessage(
                    "Update check cancelled",
                    "Try again in a moment.",
                    TimeSpan.FromSeconds(6),
                    UiSoundEffect.Subtle);
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SetVersionStatusMessage("Update check failed", TimeSpan.FromSeconds(6));
                ShowDropStatusMessage(
                    "Update check failed",
                    ex.Message,
                    TimeSpan.FromSeconds(8),
                    UiSoundEffect.Negative);
            });
        }
        finally
        {
            if (button is not null)
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_checkUpdatesButtonOriginalContent is not null)
                        button.Content = _checkUpdatesButtonOriginalContent;
                    button.IsEnabled = true;
                });

            _isCheckingForUpdates = false;
        }
    }

    private Version? ResolveCurrentVersionForUpdates()
    {
        var currentVersionText = GetCurrentVersionForComparison();
        return TryParseVersion(currentVersionText, out var version) ? version : null;
    }

    private void QueueAutomaticUpdateCheck()
    {
        if (_settings.Update?.AutoUpdate != true)
            return;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(8)).ConfigureAwait(false);
                await PerformAutomaticUpdateCheckAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Automatic update task failed: {ex}");
            }
        });
    }

    private async Task PerformAutomaticUpdateCheckAsync()
    {
        if (_isCheckingForUpdates || _isUpdateDownloadInProgress)
            return;

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        try
        {
            _isCheckingForUpdates = true;
            var currentVersion = ResolveCurrentVersionForUpdates();
            var settings = _settings.Update ?? new UpdateSettings();

            var result = await _updateService
                .CheckForUpdatesAsync(settings, currentVersion, cts.Token)
                .ConfigureAwait(false);

            if (!result.IsUpdateAvailable || result.Manifest is null)
                return;

            await HandleUpdateAvailabilityAsync(result.Manifest, true, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The check was cancelled or timed out; ignore silently.
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Automatic update check encountered an error: {ex}");
        }
        finally
        {
            _isCheckingForUpdates = false;
        }
    }

    private void TryLaunchPendingUpdateAfterExtraction()
    {
        if (_pendingUpdateManifest is null || _isUpdateDownloadInProgress)
            return;

        var manifest = _pendingUpdateManifest;
        _pendingUpdateManifest = null;

        _ = Task.Run(async () =>
        {
            try
            {
                await HandleUpdateAvailabilityAsync(manifest, true, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to resume pending update: {ex}");
            }
        });
    }
}