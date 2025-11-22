namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private void DropZoneBorder_OnDragEnter(object? sender, DragEventArgs e)
    {
        UpdateDragVisualState(e);
    }

    private void DropZoneBorder_OnDragOver(object? sender, DragEventArgs e)
    {
        UpdateDragVisualState(e);
    }

    private void DropZoneBorder_OnDragLeave(object? sender, DragEventArgs e)
    {
        if (_dropZoneBorder is null)
            return;

        ResetDragClasses();
        e.Handled = true;
    }

    private void DropZoneBorder_OnDrop(object? sender, DragEventArgs e)
    {
        var (validPaths, detectedUnityPackages) = ResolveUnityPackagePaths(e);

        if (validPaths.Count > 0)
        {
            QueueResult queueResult;
            try
            {
                queueResult = QueueUnityPackages(validPaths);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to queue dropped unitypackage files: {ex}");
                ShowDropStatusMessage(
                    "Failed to queue dropped files",
                    "See debug output for details.",
                    TimeSpan.FromSeconds(3),
                    UiSoundEffect.Negative);
                e.DragEffects = DragDropEffects.None;
                ResetDragClasses();
                e.Handled = true;
                return;
            }

            e.DragEffects = DragDropEffects.Copy;

            if (queueResult.AddedCount > 0)
            {
                SetDropZoneClass("drop-success", true);
                _dropSuccessReset?.Dispose();
                _dropSuccessReset = DispatcherTimer.RunOnce(
                    () => SetDropZoneClass("drop-success", false),
                    TimeSpan.FromMilliseconds(750));

                var secondary = queueResult.AlreadyQueuedCount > 0
                    ? $"{queueResult.AlreadyQueuedCount} already in queue."
                    : "Ready when extraction starts.";

                ShowDropStatusMessage(
                    queueResult.AddedCount == 1
                        ? "Queued 1 Unitypackage"
                        : $"Queued {queueResult.AddedCount} Unitypackages",
                    secondary,
                    TimeSpan.FromSeconds(3),
                    UiSoundEffect.Positive);
            }
            else if (queueResult.AlreadyQueuedCount > 0)
            {
                ShowDropStatusMessage(
                    queueResult.AlreadyQueuedCount == 1
                        ? "Already queued"
                        : "All packages already queued",
                    "Drop different files to add new items.",
                    TimeSpan.FromSeconds(3),
                    UiSoundEffect.Subtle);
            }
            else
            {
                ShowDropStatusMessage(
                    "No usable files found",
                    "Ensure the packages still exist on disk.",
                    TimeSpan.FromSeconds(3),
                    UiSoundEffect.Negative);
            }
        }
        else
        {
            e.DragEffects = DragDropEffects.None;

            if (detectedUnityPackages)
                ShowDropStatusMessage(
                    "Couldn't access dropped package",
                    "Try dropping files directly from a local folder.",
                    TimeSpan.FromSeconds(3),
                    UiSoundEffect.Negative);
            else
                ShowDropStatusMessage(
                    "Unsupported files",
                    "Drop .unitypackage files to queue them.",
                    TimeSpan.FromSeconds(3),
                    UiSoundEffect.Negative);
        }

        ResetDragClasses();
        e.Handled = true;
    }

    private void ClearQueueButton_OnClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ClearQueue();
    }

    private void UpdateDragVisualState(DragEventArgs e)
    {
        if (_dropZoneBorder is null)
            return;

        var isUnityPackage = ContainsUnityPackage(e);

        _dropSuccessReset?.Dispose();
        SetDropZoneClass("drop-success", false);

        SetDropZoneClass("drag-active", true);
        SetDropZoneClass("drag-valid", isUnityPackage);
        SetDropZoneClass("drag-invalid", !isUnityPackage);

        e.DragEffects = isUnityPackage ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void ResetDragClasses()
    {
        if (_dropZoneBorder is null)
            return;

        SetDropZoneClass("drag-active", false);
        SetDropZoneClass("drag-valid", false);
        SetDropZoneClass("drag-invalid", false);
    }

    private void SetDropZoneSectionVisibility(bool isVisible)
    {
        if (_startExtractionHeaderGrid is not null)
            _startExtractionHeaderGrid.IsVisible = isVisible;

        if (_dropZoneHostGrid is not null)
            _dropZoneHostGrid.IsVisible = isVisible;

        if (_searchRevealHost is not null)
            _searchRevealHost.IsHitTestVisible = isVisible;

        if (!isVisible)
        {
            if (_dropZoneBorder is not null)
                _dropZoneBorder.IsVisible = false;

            if (_searchResultsBorder is not null)
            {
                _searchResultsBorder.IsVisible = false;
                _searchResultsBorder.IsHitTestVisible = false;
                _searchResultsBorder.Opacity = 0;
            }

            if (_searchIconBorder is not null)
                _searchIconBorder.IsVisible = false;

            if (_searchHintContainer is not null)
                _searchHintContainer.Opacity = 0;
        }
        else
        {
            UpdateSearchUiState();
        }
    }

    private void SetDropZoneClass(string className, bool shouldApply)
    {
        if (_dropZoneBorder is null)
            return;

        if (shouldApply)
        {
            if (!_dropZoneBorder.Classes.Contains(className))
                _dropZoneBorder.Classes.Add(className);
        }
        else
        {
            _dropZoneBorder.Classes.Remove(className);
        }
    }

    private void ShowDropStatusMessage(
        string primary,
        string? secondary,
        TimeSpan? resetAfter = null,
        UiSoundEffect effect = UiSoundEffect.None)
    {
        if (string.IsNullOrWhiteSpace(primary))
            primary = _defaultDropPrimaryText;

        if (_dropZonePrimaryTextBlock is not null)
            _dropZonePrimaryTextBlock.Text = primary;

        if (_dropZoneSecondaryTextBlock is not null)
            _dropZoneSecondaryTextBlock.Text = string.IsNullOrWhiteSpace(secondary)
                ? _defaultDropSecondaryText
                : secondary;

        if (_dropStatusReset is not null)
        {
            _dropStatusReset.Dispose();
            _dropStatusReset = null;
        }

        var duration = resetAfter ?? TimeSpan.FromSeconds(3);
        if (duration > TimeSpan.Zero)
            _dropStatusReset = DispatcherTimer.RunOnce(ResetDropStatusMessage, duration);

        if (effect != UiSoundEffect.None)
            UiSoundService.Instance.Play(effect);
    }

    private void ResetDropStatusMessage()
    {
        if (_dropZonePrimaryTextBlock is not null)
            _dropZonePrimaryTextBlock.Text = _defaultDropPrimaryText;

        if (_dropZoneSecondaryTextBlock is not null)
            _dropZoneSecondaryTextBlock.Text = _defaultDropSecondaryText;

        _dropStatusReset = null;
    }

    private void RefreshDropZoneBaseTextIfIdle()
    {
        if (_dropStatusReset is not null)
            return;

        if (_dropZonePrimaryTextBlock is not null)
            _dropZonePrimaryTextBlock.Text = _defaultDropPrimaryText;

        if (_dropZoneSecondaryTextBlock is not null)
            _dropZoneSecondaryTextBlock.Text = _defaultDropSecondaryText;
    }
}