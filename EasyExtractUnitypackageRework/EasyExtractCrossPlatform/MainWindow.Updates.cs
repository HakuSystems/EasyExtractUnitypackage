namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private async Task CheckForUpdatesSilentlyAsync()
    {
        try
        {
            var updateInfo = await _velopackUpdateService.CheckForUpdatesAsync();
            if (updateInfo != null)
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SetVersionStatusMessage($"Update available: {updateInfo.TargetFullRelease.Version}",
                        TimeSpan.FromSeconds(10));
                    ShowDropStatusMessage(
                        "Update available",
                        $"Version {updateInfo.TargetFullRelease.Version} is ready to download.",
                        TimeSpan.FromSeconds(8),
                        UiSoundEffect.Subtle);
                });
        }
        catch (Exception ex)
        {
            // Silent fail for startup check
            LoggingService.LogError("Silent update check failed", ex);
        }
    }

    private async void CheckUpdatesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_isExtractionRunning)
        {
            ShowDropStatusMessage(
                "Extraction in progress",
                "Finish the current extraction before installing an update.",
                TimeSpan.FromSeconds(6),
                UiSoundEffect.Subtle);
            return;
        }

        var button = _checkUpdatesButton ?? sender as Button;
        if (button is not null)
        {
            _checkUpdatesButtonOriginalContent ??= button.Content;
            button.IsEnabled = false;
            button.Content = "Checking...";
        }

        await Dispatcher.UIThread.InvokeAsync(() => SetVersionStatusMessage("Checking for updates..."));

        try
        {
            // Check
            var updateInfo = await _velopackUpdateService.CheckForUpdatesAsync();

            if (updateInfo == null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SetVersionStatusMessage("You're up to date", TimeSpan.FromSeconds(6));
                    ShowDropStatusMessage(
                        "No updates available",
                        "You are using the latest version.",
                        TimeSpan.FromSeconds(6),
                        UiSoundEffect.Subtle);
                });
            }
            else
            {
                // Update found
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SetVersionStatusMessage($"Found version {updateInfo.TargetFullRelease.Version}",
                        TimeSpan.FromSeconds(6));
                    ShowDropStatusMessage(
                        "Update found",
                        $"Downloading version {updateInfo.TargetFullRelease.Version}...",
                        TimeSpan.FromSeconds(6),
                        UiSoundEffect.Positive);
                });

                // Download
                await _velopackUpdateService.DownloadUpdatesAsync(updateInfo,
                    progress =>
                    {
                        Dispatcher.UIThread.Post(() => { SetVersionStatusMessage($"Downloading... {progress}%"); });
                    });

                // Apply
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SetVersionStatusMessage("Installing update...");
                    ShowDropStatusMessage(
                        "Update ready",
                        "Restarting to apply update...",
                        TimeSpan.FromSeconds(6),
                        UiSoundEffect.Positive);
                });

                // Small delay to show message
                await Task.Delay(2000);

                _velopackUpdateService.ApplyUpdates(updateInfo);
            }
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
        }
    }
}