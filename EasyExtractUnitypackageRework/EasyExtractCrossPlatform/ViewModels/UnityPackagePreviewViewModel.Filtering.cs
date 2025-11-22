namespace EasyExtractCrossPlatform.ViewModels;

public sealed partial class UnityPackagePreviewViewModel
{
    private readonly List<UnityPackageAssetPreviewItem> _allAssets = new();
    private string _searchText = string.Empty;
    private string _selectedCategory = AllCategory;

    public ObservableCollection<UnityPackageAssetPreviewItem> Assets { get; } = new();

    public ObservableCollection<string> Categories { get; } = new();

    public RelayCommand ClearCommand { get; }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            var normalized = NormalizeCategory(value);
            if (string.Equals(_selectedCategory, normalized, StringComparison.OrdinalIgnoreCase))
                return;

            _selectedCategory = normalized;

            var match = Categories.FirstOrDefault(c =>
                string.Equals(c, _selectedCategory, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                _selectedCategory = match;

            OnPropertyChanged(nameof(SelectedCategory));
            OnPropertyChanged(nameof(IsTreeViewVisible));
            OnPropertyChanged(nameof(IsListViewVisible));
            ApplyCategoryFilter();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(_searchText, normalized, StringComparison.Ordinal))
                return;

            _searchText = normalized;
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(IsSearchActive));
            OnPropertyChanged(nameof(IsTreeViewVisible));
            OnPropertyChanged(nameof(IsListViewVisible));
            ClearCommand?.RaiseCanExecuteChanged();
            ApplyCategoryFilter();
        }
    }

    public bool IsSearchActive => !string.IsNullOrWhiteSpace(_searchText);

    public bool HasMultipleCategories => Categories.Count > 1;

    public int AssetCount => Assets.Count;

    public bool HasAssets => Assets.Count > 0;

    public bool ShowEmptyState => !IsLoading && !HasError && !HasAssets;

    private void UpdateCollectionsState()
    {
        OnPropertyChanged(nameof(AssetCount));
        OnPropertyChanged(nameof(HasAssets));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private void RefreshCategories()
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        var previousSelection = _selectedCategory;

        var distinctCategories = _allAssets
            .Select(a => a.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(comparer)
            .OrderBy(c => c, comparer)
            .ToList();

        Categories.Clear();
        Categories.Add(AllCategory);
        foreach (var category in distinctCategories)
            Categories.Add(category);

        if (distinctCategories.Count == 0)
        {
            _selectedCategory = AllCategory;
        }
        else
        {
            var match = distinctCategories.FirstOrDefault(c => comparer.Equals(c, previousSelection));
            _selectedCategory = match ?? AllCategory;
        }

        OnPropertyChanged(nameof(SelectedCategory));
        OnPropertyChanged(nameof(IsTreeViewVisible));
        OnPropertyChanged(nameof(IsListViewVisible));
        OnPropertyChanged(nameof(HasMultipleCategories));
    }

    private void ApplyCategoryFilter()
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        var normalizedSelection = NormalizeCategory(_selectedCategory);
        var hasSearch = IsSearchActive;
        var query = hasSearch ? SearchText.Trim() : string.Empty;

        var isAllCategory = normalizedSelection == AllCategory;
        IEnumerable<UnityPackageAssetPreviewItem> filtered = _allAssets;

        if (!isAllCategory)
            filtered = filtered.Where(asset => comparer.Equals(asset.Category, normalizedSelection));

        if (hasSearch)
            filtered = filtered.Where(asset => MatchesSearch(asset, query));

        var filteredList = filtered.ToList();

        var previousSelection = _selectedAsset;

        Assets.Clear();
        foreach (var item in filteredList)
            Assets.Add(item);

        UpdateCollectionsState();

        LoggingService.LogInformation(
            $"Category filter applied. Selection='{normalizedSelection}', Search='{query}', ResultCount={Assets.Count}.");

        var shouldShowTree = isAllCategory && !hasSearch;

        if (shouldShowTree)
        {
            RebuildTreeNodes();
            UpdateTreeSelectionFromAsset(previousSelection);
        }
        else
        {
            RootNodes.Clear();
            _treeNodesByPath.Clear();
            _suppressTreeSelectionSync = true;
            SelectedTreeNode = null;
            _suppressTreeSelectionSync = false;
        }

        if (Assets.Count == 0)
        {
            if (_selectedAsset is not null)
                SelectedAsset = null;
            else
                UpdateSelectedPreviewContent();
            return;
        }

        if (previousSelection is not null && Assets.Contains(previousSelection))
        {
            UpdateSelectedPreviewContent();
            return;
        }

        SelectedAsset = Assets[0];
        if (shouldShowTree)
            UpdateTreeSelectionFromAsset(SelectedAsset);
    }

    private static string NormalizeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return AllCategory;

        var trimmed = category.Trim();
        return string.Equals(trimmed, AllCategory, StringComparison.OrdinalIgnoreCase)
            ? AllCategory
            : trimmed;
    }

    private static bool MatchesSearch(UnityPackageAssetPreviewItem asset, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        var comparison = StringComparison.OrdinalIgnoreCase;

        if (!string.IsNullOrEmpty(asset.RelativePath) && asset.RelativePath.Contains(query, comparison))
            return true;
        if (!string.IsNullOrEmpty(asset.FileName) && asset.FileName.Contains(query, comparison))
            return true;
        if (!string.IsNullOrEmpty(asset.Directory) && asset.Directory.Contains(query, comparison))
            return true;

        return false;
    }

    private void OnAssetsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateCollectionsState();
    }
}