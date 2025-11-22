namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
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
}