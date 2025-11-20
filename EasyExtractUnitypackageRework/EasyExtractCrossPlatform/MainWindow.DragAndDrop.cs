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


    private void SearchRevealHost_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        _isSearchHover = true;
        if (_searchIconBorder is not null)
            _searchIconBorder.IsVisible = false;

        if (_searchHintContainer is not null)
            _searchHintContainer.Opacity = 1;

        if (_unityPackageSearchBox is null)
            return;

        Dispatcher.UIThread.Post(() => _unityPackageSearchBox.Focus());
    }

    private void SearchRevealHost_OnPointerExited(object? sender, PointerEventArgs e)
    {
        _isSearchHover = false;

        if (_searchRevealHost is not null && _searchRevealHost.IsPointerOver)
            return;

        if (SearchViewModel?.IsInteractionActive == true)
            return;

        if (_searchIconBorder is not null)
            _searchIconBorder.IsVisible = true;

        if (_searchHintContainer is not null)
            _searchHintContainer.Opacity = 0;

        UpdateSearchUiState();
    }

    private void OnSearchViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EverythingSearchViewModel.IsInteractionActive))
            Dispatcher.UIThread.Post(UpdateSearchUiState);
    }

    private void UpdateSearchUiState()
    {
        var isActive = SearchViewModel?.IsInteractionActive ?? false;
        var isDropZoneSectionVisible = _dropZoneHostGrid?.IsVisible ?? true;

        if (_dropZoneBorder is not null)
            _dropZoneBorder.IsVisible = isDropZoneSectionVisible && !isActive;

        if (_searchResultsBorder is not null)
        {
            var shouldShowResults = isDropZoneSectionVisible && isActive;
            _searchResultsBorder.IsVisible = shouldShowResults;
            _searchResultsBorder.IsHitTestVisible = shouldShowResults;
            _searchResultsBorder.Opacity = shouldShowResults ? 1 : 0;
        }

        if (_searchIconBorder is not null)
            _searchIconBorder.IsVisible = isDropZoneSectionVisible && !isActive && !_isSearchHover;

        if (_searchRevealHost is not null)
            _searchRevealHost.Classes.Set("search-active", isActive);

        if (_searchHintContainer is not null)
            _searchHintContainer.Opacity = isDropZoneSectionVisible && (isActive || _isSearchHover)
                ? 1
                : 0;
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

    public void QueueUnityPackageFromSearch(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            return;

        try
        {
            var result = QueueUnityPackages(new[] { packagePath });
            var fileName = Path.GetFileName(packagePath);

            if (result.AddedCount > 0)
            {
                SetDropZoneClass("drop-success", true);
                _dropSuccessReset?.Dispose();
                _dropSuccessReset = DispatcherTimer.RunOnce(
                    () => SetDropZoneClass("drop-success", false),
                    TimeSpan.FromMilliseconds(750));

                ShowDropStatusMessage(
                    "Queued 1 Unitypackage",
                    fileName,
                    TimeSpan.FromSeconds(3),
                    UiSoundEffect.Positive);
            }
            else if (result.AlreadyQueuedCount > 0)
            {
                ShowDropStatusMessage(
                    "Already queued",
                    fileName,
                    TimeSpan.FromSeconds(3),
                    UiSoundEffect.Subtle);
            }
            else
            {
                ShowDropStatusMessage(
                    "Nothing queued",
                    "The selected package could not be added.",
                    TimeSpan.FromSeconds(3),
                    UiSoundEffect.Negative);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to queue package from search: {ex}");
            ShowDropStatusMessage(
                "Failed to queue package",
                ex.Message,
                TimeSpan.FromSeconds(3),
                UiSoundEffect.Negative);
        }
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

    private static (List<string> ValidPaths, bool DetectedUnityPackage) ResolveUnityPackagePaths(DragEventArgs e)
    {
        var validPaths = new List<string>();
        var detectedUnityPackage = false;
        var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in EnumerateDroppedItemNames(e))
        {
            var trimmedCandidate = candidate?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedCandidate))
                continue;

            if (IsUnityPackage(trimmedCandidate))
                detectedUnityPackage = true;

            if (!TryResolveDroppedPath(trimmedCandidate, out var resolvedPath))
                continue;

            if (!IsUnityPackage(resolvedPath))
                continue;

            try
            {
                var normalizedPath = Path.GetFullPath(resolvedPath);
                if (!File.Exists(normalizedPath))
                    continue;

                if (uniquePaths.Add(normalizedPath))
                    validPaths.Add(normalizedPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to normalize dropped path '{resolvedPath}': {ex}");
            }
        }

        return (validPaths, detectedUnityPackage);
    }

    private static bool ContainsUnityPackage(DragEventArgs e)
    {
        foreach (var candidate in EnumerateDroppedItemNames(e))
            if (IsUnityPackage(candidate))
                return true;

        return false;
    }

    private static IEnumerable<string> EnumerateDroppedItemNames(DragEventArgs e)
    {
        var dataObject = e.Data;
        if (dataObject is null)
            yield break;

        List<IStorageItem>? storageItems = null;

        try
        {
            var files = dataObject.GetFiles();
            if (files is not null)
                storageItems = files.ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to enumerate dropped storage items: {ex}");
        }

        if (storageItems is not null)
            foreach (var item in storageItems)
            {
                if (item is null)
                    continue;

                foreach (var candidate in EnumerateStorageItemEntries(item))
                    yield return candidate;
            }

        if (dataObject.Contains(DataFormats.FileNames))
        {
            var rawFileNames = dataObject.Get(DataFormats.FileNames);
            switch (rawFileNames)
            {
                case IEnumerable<string> names:
                    foreach (var name in names)
                        if (!string.IsNullOrWhiteSpace(name))
                            yield return name;
                    break;
                case string singleName when !string.IsNullOrWhiteSpace(singleName):
                    yield return singleName;
                    break;
            }
        }

        if (dataObject.Contains(DataFormats.Text))
        {
            var textData = dataObject.Get(DataFormats.Text);
            foreach (var entry in EnumerateTextDataEntries(textData))
                if (!string.IsNullOrWhiteSpace(entry))
                    yield return entry;
        }
    }

    private static IEnumerable<string> EnumerateStorageItemEntries(IStorageItem item)
    {
        string? localPath = null;
        try
        {
            localPath = item.TryGetLocalPath();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to resolve local path for dropped item: {ex}");
        }

        if (!string.IsNullOrWhiteSpace(localPath))
            yield return localPath;

        Uri? itemUri = null;
        try
        {
            itemUri = item.Path;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to resolve URI for dropped item: {ex}");
        }

        if (itemUri is { IsAbsoluteUri: true })
        {
            if (itemUri.IsFile && !string.IsNullOrWhiteSpace(itemUri.LocalPath))
                yield return itemUri.LocalPath;
            else
                yield return itemUri.AbsoluteUri;
        }

        if (item is IStorageFile file && !string.IsNullOrWhiteSpace(file.Name))
            yield return file.Name;
    }

    private static bool TryResolveDroppedPath(string candidate, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        var input = candidate.Trim();

        if (Uri.TryCreate(input, UriKind.Absolute, out var absoluteUri))
        {
            if (!absoluteUri.IsFile)
                return false;

            input = absoluteUri.LocalPath;
        }

        if (!Path.IsPathRooted(input))
            return false;

        try
        {
            resolvedPath = Path.GetFullPath(input);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to resolve dropped path '{candidate}': {ex}");
            return false;
        }
    }

    private static IEnumerable<string> EnumerateTextDataEntries(object? textData)
    {
        switch (textData)
        {
            case null:
                yield break;
            case string text:
                foreach (var entry in SplitUriListEntries(text))
                    yield return entry;
                break;
            case IEnumerable<string> entries:
                foreach (var entry in entries)
                foreach (var value in SplitUriListEntries(entry))
                    yield return value;
                break;
            default:
                var asString = textData.ToString();
                if (!string.IsNullOrWhiteSpace(asString))
                    foreach (var entry in SplitUriListEntries(asString))
                        yield return entry;
                break;
        }
    }

    private static IEnumerable<string> SplitUriListEntries(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            yield break;

        var segments = input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            var trimmed = segment.Trim();
            if (trimmed.Length == 0)
                continue;

            if (trimmed[0] == '#')
                continue;

            yield return trimmed;
        }
    }

    private static bool IsUnityPackage(string? pathOrName)
    {
        if (string.IsNullOrWhiteSpace(pathOrName))
            return false;

        var extension = Path.GetExtension(pathOrName);
        return string.Equals(extension, UnityPackageExtension, StringComparison.OrdinalIgnoreCase);
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

