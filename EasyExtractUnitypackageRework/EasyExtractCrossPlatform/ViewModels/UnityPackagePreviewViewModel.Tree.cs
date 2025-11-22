using System.Text;

namespace EasyExtractCrossPlatform.ViewModels;

public sealed partial class UnityPackagePreviewViewModel
{
    private readonly HashSet<string> _directoriesToPrune = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, UnityPackageAssetTreeNode> _treeNodesByPath =
        new(StringComparer.OrdinalIgnoreCase);

    private UnityPackageAssetTreeNode? _selectedTreeNode;
    private bool _suppressTreeSelectionSync;

    public ObservableCollection<UnityPackageAssetTreeNode> RootNodes { get; } = new();

    public RelayCommand SelectTreeNodeCommand { get; }

    public RelayCommand NodeToggleCommand { get; }

    public RelayCommand CollapseAllFoldersCommand { get; }

    public UnityPackageAssetTreeNode? SelectedTreeNode
    {
        get => _selectedTreeNode;
        set
        {
            if (ReferenceEquals(_selectedTreeNode, value))
                return;

            if (_selectedTreeNode is not null)
                _selectedTreeNode.IsSelected = false;

            _selectedTreeNode = value;

            if (_selectedTreeNode is not null)
                _selectedTreeNode.IsSelected = true;

            OnPropertyChanged(nameof(SelectedTreeNode));

            if (_selectedTreeNode?.Asset is not null)
            {
                _suppressTreeSelectionSync = true;
                SelectedAsset = _selectedTreeNode.Asset;
                _suppressTreeSelectionSync = false;
            }
        }
    }

    public bool IsTreeViewVisible =>
        string.Equals(SelectedCategory, AllCategory, StringComparison.OrdinalIgnoreCase) && !IsSearchActive;

    public bool IsListViewVisible => !IsTreeViewVisible;

    private void RebuildTreeNodes()
    {
        var previousSelection = _selectedTreeNode;

        RootNodes.Clear();
        _treeNodesByPath.Clear();

        if (_allAssets.Count == 0)
        {
            SynchronizeTreeSelection(null);
            return;
        }

        foreach (var asset in _allAssets)
        {
            var normalizedPath = NormalizeAssetPath(asset);
            var segments = normalizedPath.Length == 0
                ? Array.Empty<string>()
                : normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (string.Equals(asset.Category, "Folder", StringComparison.OrdinalIgnoreCase))
            {
                if (segments.Length > 0)
                    EnsureFolderNode(segments);
                continue;
            }

            var parent = segments.Length > 1
                ? EnsureFolderNode(segments[..^1])
                : null;

            var fileName = segments.Length > 0
                ? segments[^1]
                : asset.FileName ?? asset.RelativePath ?? "Asset";

            fileName = string.IsNullOrWhiteSpace(fileName) ? "Asset" : fileName;
            if (_treeNodesByPath.ContainsKey(normalizedPath))
                continue;

            var fileNode = new UnityPackageAssetTreeNode(fileName, normalizedPath, false, asset, parent);
            AddNodeToParent(fileNode, parent);
            _treeNodesByPath[normalizedPath] = fileNode;
        }

        if (_directoriesToPrune.Count > 0)
            PruneCorruptedFolders(RootNodes);

        SortNodes(RootNodes);

        foreach (var root in RootNodes)
            if (root.IsFolder)
                root.IsExpanded = true;

        SynchronizeTreeSelection(previousSelection);
    }

    private void ToggleNode(UnityPackageAssetTreeNode? node)
    {
        if (node is null)
            return;

        if (node.IsFolder)
        {
            var wasExpanded = node.IsExpanded;
            node.IsExpanded = !node.IsExpanded;

            if (wasExpanded && !node.IsExpanded)
                SelectedTreeNode = node;
        }
        else if (node.Asset is not null)
        {
            SelectedAsset = node.Asset;
        }
    }

    private void SelectTreeNode(UnityPackageAssetTreeNode? node)
    {
        if (node is null)
            return;

        SelectedTreeNode = node;
    }

    private void CollapseDescendants(UnityPackageAssetTreeNode node, bool includeSelf)
    {
        if (node is null || !node.IsFolder)
            return;

        CollapseRecursive(node, includeSelf);

        if (_selectedTreeNode is not null &&
            !ReferenceEquals(_selectedTreeNode, node) &&
            IsDescendantOf(_selectedTreeNode, node))
        {
            _suppressTreeSelectionSync = true;
            SelectedTreeNode = node;
            _suppressTreeSelectionSync = false;
        }
    }

    private static void CollapseRecursive(UnityPackageAssetTreeNode node, bool includeSelf)
    {
        foreach (var child in node.Children)
            if (child.IsFolder)
                CollapseRecursive(child, true);

        if (includeSelf)
            node.IsExpanded = false;
    }

    private static bool IsDescendantOf(UnityPackageAssetTreeNode? candidate, UnityPackageAssetTreeNode ancestor)
    {
        var current = candidate?.Parent;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
                return true;

            current = current.Parent;
        }

        return false;
    }

    private void SynchronizeTreeSelection(UnityPackageAssetTreeNode? previousNode)
    {
        if (!IsTreeViewVisible || _treeNodesByPath.Count == 0)
        {
            SelectedTreeNode = null;
            return;
        }

        if (previousNode is not null &&
            _treeNodesByPath.TryGetValue(previousNode.FullPath, out var mapped))
        {
            SelectedTreeNode = mapped;
            return;
        }

        if (_selectedAsset is not null)
        {
            UpdateTreeSelectionFromAsset(_selectedAsset);

            if (_selectedTreeNode is not null &&
                _treeNodesByPath.ContainsKey(_selectedTreeNode.FullPath))
                return;
        }

        SelectedTreeNode = null;
    }

    private UnityPackageAssetTreeNode? EnsureFolderNode(IReadOnlyList<string> segments)
    {
        if (segments.Count == 0)
            return null;

        UnityPackageAssetTreeNode? parent = null;
        var builder = new StringBuilder();

        for (var i = 0; i < segments.Count; i++)
        {
            if (builder.Length > 0)
                builder.Append('/');
            builder.Append(segments[i]);
            var key = builder.ToString();

            if (!_treeNodesByPath.TryGetValue(key, out var node))
            {
                node = new UnityPackageAssetTreeNode(segments[i], key, true, null, parent);
                AddNodeToParent(node, parent);
                _treeNodesByPath[key] = node;
            }

            parent = node;
        }

        return parent;
    }

    private void AddNodeToParent(UnityPackageAssetTreeNode node, UnityPackageAssetTreeNode? parent)
    {
        var target = parent?.Children ?? RootNodes;
        if (!target.Contains(node))
            target.Add(node);
    }

    private void SortNodes(ObservableCollection<UnityPackageAssetTreeNode> nodes)
    {
        var ordered = nodes
            .OrderBy(n => n.IsFolder ? 0 : 1)
            .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!ordered.SequenceEqual(nodes))
        {
            nodes.Clear();
            foreach (var node in ordered)
                nodes.Add(node);
        }

        foreach (var node in nodes)
            if (node.Children.Count > 0)
                SortNodes(node.Children);
    }

    private void PruneCorruptedFolders(ObservableCollection<UnityPackageAssetTreeNode> nodes)
    {
        for (var i = nodes.Count - 1; i >= 0; i--)
        {
            var node = nodes[i];
            if (node.Children.Count > 0)
                PruneCorruptedFolders(node.Children);

            if (!node.IsFolder || node.Children.Count > 0 || node.Asset is not null)
                continue;

            if (!_directoriesToPrune.Contains(node.FullPath))
                continue;

            nodes.RemoveAt(i);
            _treeNodesByPath.Remove(node.FullPath);
        }
    }

    private void UpdateTreeSelectionFromAsset(UnityPackageAssetPreviewItem? asset)
    {
        if (!IsTreeViewVisible)
            return;

        if (asset is null)
        {
            _suppressTreeSelectionSync = true;
            SelectedTreeNode = null;
            _suppressTreeSelectionSync = false;
            return;
        }

        var key = NormalizeAssetPath(asset);
        if (!_treeNodesByPath.TryGetValue(key, out var node))
            return;

        ExpandAncestors(node);

        if (ReferenceEquals(_selectedTreeNode, node))
            return;

        _suppressTreeSelectionSync = true;
        SelectedTreeNode = node;
        _suppressTreeSelectionSync = false;
    }

    private static void ExpandAncestors(UnityPackageAssetTreeNode node)
    {
        var current = node.Parent;
        while (current is not null)
        {
            current.IsExpanded = true;
            current = current.Parent;
        }
    }

    private static string NormalizeAssetPath(UnityPackageAssetPreviewItem asset)
    {
        var normalized = NormalizePath(asset.RelativePath);
        if (string.IsNullOrWhiteSpace(normalized))
            normalized = NormalizePath(asset.FileName);

        return normalized;
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        return path.Replace('\\', '/').Trim('/');
    }
}