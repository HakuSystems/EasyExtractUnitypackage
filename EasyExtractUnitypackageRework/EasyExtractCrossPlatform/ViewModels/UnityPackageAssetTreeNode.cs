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