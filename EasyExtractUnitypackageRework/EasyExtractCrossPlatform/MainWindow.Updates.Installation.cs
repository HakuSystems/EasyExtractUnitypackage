namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private async Task HandleUpdateAvailabilityAsync(
        UpdateManifest manifest,
        bool isAutomatic,
        CancellationToken cancellationToken)
    {
        if (_isUpdateDownloadInProgress)
        {
            _pendingUpdateManifest = manifest;
            return;
        }

        if (_isExtractionRunning)
        {
            _pendingUpdateManifest = manifest;
            if (!isAutomatic)
                await Dispatcher.UIThread.InvokeAsync(() =>
                    ShowDropStatusMessage(
                        "Extraction still running",
                        "Update will start automatically when extraction finishes.",
                        TimeSpan.FromSeconds(6),
                        UiSoundEffect.Subtle));

            return;
        }

        _isUpdateDownloadInProgress = true;
        _activeUpdateManifest = manifest;
        _pendingUpdateManifest = null;

        var progress = new Progress<UpdateProgress>(update => ReportUpdateProgress(manifest, update));

        var preparationResult = await _updateService
            .DownloadAndPrepareUpdateAsync(manifest, progress, cancellationToken)
            .ConfigureAwait(false);

        if (!preparationResult.Success || preparationResult.Preparation is null)
        {
            ResetUpdateProgressState();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SetVersionStatusMessage("Update failed", TimeSpan.FromSeconds(6));
                var detail = preparationResult.ErrorMessage ?? "Update could not be prepared.";
                ShowDropStatusMessage(
                    "Update failed",
                    detail,
                    TimeSpan.FromSeconds(8),
                    UiSoundEffect.Negative);
            });
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var status = isAutomatic
                ? $"Installing update {manifest.Version}"
                : $"Ready to install update {manifest.Version}";
            SetVersionStatusMessage(status, TimeSpan.FromSeconds(6));

            var detail = isAutomatic
                ? "EasyExtract will restart automatically to finish installing."
                : "EasyExtract will restart to finish installing.";
            ShowDropStatusMessage(
                status,
                detail,
                TimeSpan.FromSeconds(6),
                UiSoundEffect.Positive);
        });

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            ResetUpdateProgressState();
            throw;
        }

        var launchSucceeded = _updateService.TryLaunchPreparedUpdate(preparationResult.Preparation);
        if (!launchSucceeded)
        {
            ResetUpdateProgressState();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SetVersionStatusMessage("Update failed", TimeSpan.FromSeconds(6));
                ShowDropStatusMessage(
                    "Update failed",
                    "Installer could not be started.",
                    TimeSpan.FromSeconds(8),
                    UiSoundEffect.Negative);
            });
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            SetVersionStatusMessage("Restarting to finish update");
            Close();
        });
    }

    private void ReportUpdateProgress(UpdateManifest manifest, UpdateProgress progress)
    {
        var now = DateTime.UtcNow;
        var phaseChanged = _lastUpdatePhase != progress.Phase;
        var percentChanged = progress.Percentage.HasValue &&
                             (!_lastUpdatePercentage.HasValue ||
                              Math.Abs(progress.Percentage.Value - _lastUpdatePercentage.Value) >= 0.01);
        var intervalElapsed = now - _lastUpdateUiRefresh > TimeSpan.FromMilliseconds(500);

        if (!phaseChanged && !percentChanged && !intervalElapsed)
            return;

        _lastUpdatePhase = progress.Phase;
        _lastUpdatePercentage = progress.Percentage;
        _lastUpdateUiRefresh = now;

        var statusText = BuildUpdateStatusText(progress);
        var announcePhase = phaseChanged;

        Dispatcher.UIThread.Post(() =>
        {
            SetVersionStatusMessage(statusText);

            if (!announcePhase)
                return;

            var phaseMessage = progress.Phase switch
            {
                UpdatePhase.Downloading => $"Downloading version {manifest.Version}",
                UpdatePhase.Extracting => $"Extracting version {manifest.Version}",
                UpdatePhase.Preparing => $"Preparing version {manifest.Version}",
                UpdatePhase.WaitingForRestart => $"Ready to install version {manifest.Version}",
                UpdatePhase.Completed => $"Version {manifest.Version} ready",
                _ => $"Updating to version {manifest.Version}"
            };

            ShowDropStatusMessage(phaseMessage, manifest.ReleaseName ?? $"Tag {manifest.TagName}",
                TimeSpan.FromSeconds(4));
        });
    }

    private static string BuildUpdateStatusText(UpdateProgress progress)
    {
        var phaseText = progress.Phase switch
        {
            UpdatePhase.Downloading => "Downloading update",
            UpdatePhase.Extracting => "Extracting update",
            UpdatePhase.Preparing => "Preparing update",
            UpdatePhase.WaitingForRestart => "Update ready",
            UpdatePhase.Completed => "Update ready",
            _ => "Updating"
        };

        if (progress.Percentage is { } percent)
            phaseText = $"{phaseText} {percent.ToString("P0", CultureInfo.CurrentCulture)}";

        return phaseText;
    }

    private void ResetUpdateProgressState()
    {
        _isUpdateDownloadInProgress = false;
        _activeUpdateManifest = null;
        _lastUpdatePhase = null;
        _lastUpdatePercentage = null;
        _lastUpdateUiRefresh = DateTime.MinValue;
    }
}