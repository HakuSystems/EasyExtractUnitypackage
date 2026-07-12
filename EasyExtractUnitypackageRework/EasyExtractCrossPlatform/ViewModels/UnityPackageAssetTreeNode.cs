namespace EasyExtractCrossPlatform.ViewModels;

public sealed class UnityPackageAssetTreeNode : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isSelected;

    public UnityPackageAssetTreeNode(
        string name,
        string fullPath,
        bool isFolder,
        UnityPackageAssetPreviewItem? asset,
        UnityPackageAssetTreeNode? parent)
    {
        Name = name;
        FullPath = fullPath;
        IsFolder = isFolder;
        Asset = asset;
        Parent = parent;
        Children = new ObservableCollection<UnityPackageAssetTreeNode>();
        Children.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasChildren));
            OnPropertyChanged(nameof(HasFolderChildren));
            OnPropertyChanged(nameof(ShowCollapseDescendants));
            OnPropertyChanged(nameof(CanToggle));
        };
    }

    public string Name { get; }

    public string FullPath { get; }

    public bool IsFolder { get; }

    public UnityPackageAssetPreviewItem? Asset { get; }

    public UnityPackageAssetTreeNode? Parent { get; }

    public ObservableCollection<UnityPackageAssetTreeNode> Children { get; }

    public bool HasChildren => Children.Count > 0;

    public bool HasFolderChildren => Children.Any(child => child.IsFolder);

    public bool CanToggle => IsFolder && HasChildren;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
                return;
            _isExpanded = value;
            OnPropertyChanged(nameof(IsExpanded));
            OnPropertyChanged(nameof(IconGeometry));
            OnPropertyChanged(nameof(ToggleIconGeometry));
        }
    }

    public string? SizeText => Asset?.SizeText;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }
    }

    public bool IsCheckable => IsFolder
        ? ContainsSelectableAsset(this)
        : Asset is { IsSelectable: true };

    /// <summary>
    ///     Tri-state check status: file nodes mirror their asset, folder nodes
    ///     aggregate their descendants (null = partially checked).
    /// </summary>
    public bool? IsChecked
    {
        get
        {
            if (!IsFolder)
                return Asset is { IsSelectable: true, IsChecked: true };

            var sawChecked = false;
            var sawUnchecked = false;
            AggregateCheckState(this, ref sawChecked, ref sawUnchecked);

            if (sawChecked && sawUnchecked)
                return null;

            return sawChecked;
        }
        set
        {
            var effective = value == true;

            if (IsFolder)
                SetDescendantsChecked(this, effective);
            else if (Asset is { IsSelectable: true } asset)
                asset.IsChecked = effective;
        }
    }

    public void NotifyCheckStateChanged()
    {
        OnPropertyChanged(nameof(IsChecked));
    }

    private static bool ContainsSelectableAsset(UnityPackageAssetTreeNode node)
    {
        foreach (var child in node.Children)
        {
            if (child.Asset is { IsSelectable: true })
                return true;

            if (child.IsFolder && ContainsSelectableAsset(child))
                return true;
        }

        return false;
    }

    private static void AggregateCheckState(UnityPackageAssetTreeNode node, ref bool sawChecked, ref bool sawUnchecked)
    {
        foreach (var child in node.Children)
        {
            if (sawChecked && sawUnchecked)
                return;

            if (child.Asset is { IsSelectable: true } asset)
            {
                if (asset.IsChecked)
                    sawChecked = true;
                else
                    sawUnchecked = true;
            }

            if (child.IsFolder)
                AggregateCheckState(child, ref sawChecked, ref sawUnchecked);
        }
    }

    private static void SetDescendantsChecked(UnityPackageAssetTreeNode node, bool isChecked)
    {
        foreach (var child in node.Children)
        {
            if (child.Asset is { IsSelectable: true } asset)
                asset.IsChecked = isChecked;

            if (child.IsFolder)
                SetDescendantsChecked(child, isChecked);
        }
    }

    public Geometry IconGeometry => IsFolder
        ? UnityAssetIconProvider.GetFolderIcon(IsExpanded)
        : UnityAssetIconProvider.GetAssetIcon(Asset);

    public Geometry ToggleIconGeometry => UnityAssetIconProvider.GetChevron(IsExpanded);

    public Geometry CollapseDescendantsIcon => UnityAssetIconProvider.CollapseAll;

    public bool ShowCollapseDescendants => IsFolder && HasFolderChildren;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString()
    {
        return Name;
    }
}