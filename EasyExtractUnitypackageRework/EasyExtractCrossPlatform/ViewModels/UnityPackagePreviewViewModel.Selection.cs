namespace EasyExtractCrossPlatform.ViewModels;

public sealed partial class UnityPackagePreviewViewModel
{
    private bool _selectionRefreshQueued;

    public RelayCommand SelectAllAssetsCommand { get; private set; } = null!;

    public RelayCommand ClearAssetSelectionCommand { get; private set; } = null!;

    public int CheckedAssetCount =>
        _allAssets.Count(asset => asset is { IsSelectable: true, IsChecked: true });

    public bool HasCheckedAssets => CheckedAssetCount > 0;

    public string SelectionSummaryText
    {
        get
        {
            var selectable = _allAssets.Count(asset => asset.IsSelectable);
            var checkedAssets = _allAssets
                .Where(asset => asset is { IsSelectable: true, IsChecked: true })
                .ToList();

            if (checkedAssets.Count == 0)
                return $"0 of {selectable} selected";

            var totalBytes = checkedAssets.Sum(asset => asset.AssetSizeBytes);
            return $"{checkedAssets.Count} of {selectable} selected ({FormatFileSize(totalBytes)})";
        }
    }

    public IReadOnlyList<string> GetCheckedAssetKeys()
    {
        return _allAssets
            .Where(asset => asset is { IsSelectable: true, IsChecked: true } &&
                            !string.IsNullOrWhiteSpace(asset.AssetKey))
            .Select(asset => asset.AssetKey!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void InitializeSelectionCommands()
    {
        SelectAllAssetsCommand = new RelayCommand(
            () => SetVisibleAssetsChecked(true),
            () => Assets.Any(asset => asset.IsSelectable));
        ClearAssetSelectionCommand = new RelayCommand(
            () => SetAllAssetsChecked(false),
            () => HasCheckedAssets);
    }

    private void SetVisibleAssetsChecked(bool isChecked)
    {
        foreach (var asset in Assets)
            if (asset.IsSelectable)
                asset.IsChecked = isChecked;
    }

    private void SetAllAssetsChecked(bool isChecked)
    {
        foreach (var asset in _allAssets)
            if (asset.IsSelectable)
                asset.IsChecked = isChecked;
    }

    private void OnAssetCheckedChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(UnityPackageAssetPreviewItem.IsChecked), StringComparison.Ordinal))
            return;

        QueueSelectionRefresh();
    }

    // Folder checkboxes flip many assets in one gesture; coalesce the tree and
    // summary updates into a single pass after the batch completes.
    private void QueueSelectionRefresh()
    {
        if (_selectionRefreshQueued || _isDisposed)
            return;

        _selectionRefreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _selectionRefreshQueued = false;
            if (_isDisposed)
                return;

            foreach (var node in _treeNodesByPath.Values)
                node.NotifyCheckStateChanged();

            RefreshSelectionSummary();
        }, DispatcherPriority.Background);
    }

    private void RefreshSelectionSummary()
    {
        OnPropertyChanged(nameof(CheckedAssetCount));
        OnPropertyChanged(nameof(HasCheckedAssets));
        OnPropertyChanged(nameof(SelectionSummaryText));
        SelectAllAssetsCommand.NotifyCanExecuteChanged();
        ClearAssetSelectionCommand.NotifyCanExecuteChanged();
    }

    private void ClearAllAssets()
    {
        foreach (var asset in _allAssets)
            asset.PropertyChanged -= OnAssetCheckedChanged;

        _allAssets.Clear();
    }
}
