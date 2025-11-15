using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Docnet.Core;
using Docnet.Core.Models;
using EasyExtractCrossPlatform.Models;
using EasyExtractCrossPlatform.Services;
using EasyExtractCrossPlatform.Utilities;

namespace EasyExtractCrossPlatform.ViewModels;

public sealed class UnityPackagePreviewViewModel : INotifyPropertyChanged, IDisposable
{
    private const string AllCategory = "All assets";
    private static readonly PageDimensions PdfPreviewDimensions = new(2.0d);
    private readonly List<UnityPackageAssetPreviewItem> _allAssets = new();
    private readonly HashSet<string> _directoriesToPrune = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly IUnityPackagePreviewService _previewService;
    private readonly Func<Task<MaliciousCodeScanResult?>>? _securityScanProvider;

    private readonly Dictionary<string, UnityPackageAssetTreeNode> _treeNodesByPath =
        new(StringComparer.OrdinalIgnoreCase);

    private string _audioDurationText = "00:00";
    private string _audioPositionText = "00:00";
    private AudioPreviewSession? _audioPreviewSession;
    private double _audioProgress;
    private string _audioStatusText = "Ready";
    private DispatcherTimer? _audioTimer;
    private Bitmap? _fallbackPreviewImage;
    private bool _hasError;
    private bool _isDisposed;
    private bool _isLoading;

    private Task? _loadTask;
    private string? _packageModifiedText;
    private string _packageName = string.Empty;
    private string _packageSizeText = string.Empty;
    private int _previewTabIndex;
    private Bitmap? _primaryImagePreview;
    private string _searchText = string.Empty;
    private string? _securityErrorText;
    private bool _securityScanFailed;
    private bool _securityScanInProgress;
    private MaliciousCodeScanResult? _securityScanResult;
    private Task? _securityScanTask;
    private string? _securityStatusText = "Security scan pending...";
    private UnityPackageAssetPreviewItem? _selectedAsset;
    private string _selectedCategory = AllCategory;
    private UnityPackageAssetTreeNode? _selectedTreeNode;
    private string? _statusMessage;
    private bool _suppressTreeSelectionSync;
    private string _totalAssetSizeText = string.Empty;

    public UnityPackagePreviewViewModel(
        IUnityPackagePreviewService previewService,
        string packagePath,
        Func<Task<MaliciousCodeScanResult?>>? securityScanProvider = null)
    {
        _previewService = previewService ?? throw new ArgumentNullException(nameof(previewService));
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        PackagePath = packagePath;
        _securityScanProvider = securityScanProvider;

        Categories.Add(AllCategory);
        Assets.CollectionChanged += OnAssetsCollectionChanged;
        SecurityThreats.CollectionChanged += OnSecurityThreatsCollectionChanged;

        PlayAudioPreviewCommand = new RelayCommand(_ => ToggleAudioPlayback(), _ => HasAudioPreview);
        StopAudioPreviewCommand = new RelayCommand(_ => StopAudioPlayback(), _ => HasAudioPreview);
        NodeToggleCommand = new RelayCommand(parameter =>
        {
            if (parameter is UnityPackageAssetTreeNode node)
                ToggleNode(node);
        });
        SelectTreeNodeCommand = new RelayCommand(parameter =>
        {
            if (parameter is UnityPackageAssetTreeNode node)
                SelectTreeNode(node);
        });
        CollapseAllFoldersCommand = new RelayCommand(parameter =>
        {
            if (parameter is UnityPackageAssetTreeNode node)
                CollapseDescendants(node, true);
        });
        ClearCommand = new RelayCommand(_ => SearchText = string.Empty, _ => IsSearchActive);

        if (_securityScanProvider is not null)
            _securityScanTask = RunSecurityScanAsync();
    }

    public ObservableCollection<UnityPackageAssetPreviewItem> Assets { get; } = new();

    public ObservableCollection<string> Categories { get; } = new();

    public ObservableCollection<UnityPackageAssetTreeNode> RootNodes { get; } = new();

    public ObservableCollection<SecurityThreatDisplay> SecurityThreats { get; } = new();

    public RelayCommand ClearCommand { get; }

    public RelayCommand SelectTreeNodeCommand { get; }

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

    public bool SecurityScanInProgress
    {
        get => _securityScanInProgress;
        private set
        {
            if (value == _securityScanInProgress)
                return;

            _securityScanInProgress = value;
            OnPropertyChanged(nameof(SecurityScanInProgress));
            OnPropertyChanged(nameof(ShowSecuritySection));
        }
    }

    public bool SecurityScanFailed
    {
        get => _securityScanFailed;
        private set
        {
            if (value == _securityScanFailed)
                return;

            _securityScanFailed = value;
            OnPropertyChanged(nameof(SecurityScanFailed));
            OnPropertyChanged(nameof(ShowSecuritySection));
        }
    }

    public string? SecurityStatusText
    {
        get => _securityStatusText;
        private set
        {
            if (string.Equals(_securityStatusText, value, StringComparison.Ordinal))
                return;

            _securityStatusText = value;
            OnPropertyChanged(nameof(SecurityStatusText));
        }
    }

    public string? SecurityErrorText
    {
        get => _securityErrorText;
        private set
        {
            if (string.Equals(_securityErrorText, value, StringComparison.Ordinal))
                return;

            _securityErrorText = value;
            OnPropertyChanged(nameof(SecurityErrorText));
            OnPropertyChanged(nameof(HasSecurityError));
            OnPropertyChanged(nameof(ShowSecuritySection));
        }
    }

    public bool SecurityHasThreats => SecurityThreats.Count > 0;

    public bool HasSecurityError => !string.IsNullOrWhiteSpace(SecurityErrorText);

    public bool ShowSecuritySection =>
        SecurityScanInProgress || SecurityScanFailed || SecurityHasThreats || HasSecurityError;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (value == _isLoading)
                return;

            _isLoading = value;
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    public bool HasError
    {
        get => _hasError;
        private set
        {
            if (value == _hasError)
                return;

            _hasError = value;
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (string.Equals(_statusMessage, value, StringComparison.Ordinal))
                return;

            _statusMessage = value;
            OnPropertyChanged(nameof(StatusMessage));
        }
    }

    public string PackageName
    {
        get => _packageName;
        private set
        {
            if (string.Equals(_packageName, value, StringComparison.Ordinal))
                return;

            _packageName = value ?? string.Empty;
            OnPropertyChanged(nameof(PackageName));
            OnPropertyChanged(nameof(WindowTitle));
        }
    }

    public string PackagePath { get; }

    public string PackageSizeText
    {
        get => _packageSizeText;
        private set
        {
            if (string.Equals(_packageSizeText, value, StringComparison.Ordinal))
                return;

            _packageSizeText = value ?? string.Empty;
            OnPropertyChanged(nameof(PackageSizeText));
        }
    }

    public string TotalAssetSizeText
    {
        get => _totalAssetSizeText;
        private set
        {
            if (string.Equals(_totalAssetSizeText, value, StringComparison.Ordinal))
                return;

            _totalAssetSizeText = value ?? string.Empty;
            OnPropertyChanged(nameof(TotalAssetSizeText));
        }
    }

    public string? PackageModifiedText
    {
        get => _packageModifiedText;
        private set
        {
            if (string.Equals(_packageModifiedText, value, StringComparison.Ordinal))
                return;

            _packageModifiedText = value;
            OnPropertyChanged(nameof(PackageModifiedText));
        }
    }

    public UnityPackageAssetPreviewItem? SelectedAsset
    {
        get => _selectedAsset;
        set
        {
            if (ReferenceEquals(_selectedAsset, value))
                return;

            _selectedAsset = value;
            OnPropertyChanged(nameof(SelectedAsset));
            OnPropertyChanged(nameof(SelectedAssetPath));
            OnPropertyChanged(nameof(SelectedAssetSizeText));
            OnPropertyChanged(nameof(SelectedAssetCategory));
            OnPropertyChanged(nameof(SelectedAssetFolder));
            OnPropertyChanged(nameof(IsAssetDataTruncated));
            UpdateSelectedPreviewContent();
            OnPropertyChanged(nameof(ShowUnsupportedModelMessage));
            OnPropertyChanged(nameof(ShowUnsupportedAudioMessage));

            if (!_suppressTreeSelectionSync)
                UpdateTreeSelectionFromAsset(_selectedAsset);
        }
    }

    public string? SelectedAssetPath => SelectedAsset?.RelativePath;

    public string? SelectedAssetSizeText => SelectedAsset?.SizeText;

    public string? SelectedAssetCategory => SelectedAsset?.Category;

    public string? SelectedAssetFolder => SelectedAsset?.Directory;

    public bool IsAssetDataTruncated => SelectedAsset?.IsAssetDataTruncated == true;

    public Bitmap? ImagePreview => _primaryImagePreview;

    public bool HasImagePreview => _primaryImagePreview is not null;

    public Bitmap? FallbackPreviewImage => _fallbackPreviewImage;

    public bool HasFallbackPreview => _fallbackPreviewImage is not null;

    public bool HasAnyImagePreview => HasImagePreview || HasFallbackPreview;

    public string? TextPreview { get; private set; }

    public bool HasTextPreview => !string.IsNullOrEmpty(TextPreview);

    public bool IsTextPreviewTruncated { get; private set; }

    public bool HasAudioPreview => _audioPreviewSession is not null;

    public bool IsAudioPlaying => _audioPreviewSession?.IsPlaying ?? false;

    public string AudioStatusText
    {
        get => _audioStatusText;
        private set
        {
            if (string.Equals(_audioStatusText, value, StringComparison.Ordinal))
                return;

            _audioStatusText = value;
            OnPropertyChanged(nameof(AudioStatusText));
        }
    }

    public string AudioPositionText
    {
        get => _audioPositionText;
        private set
        {
            if (string.Equals(_audioPositionText, value, StringComparison.Ordinal))
                return;

            _audioPositionText = value;
            OnPropertyChanged(nameof(AudioPositionText));
        }
    }

    public string AudioDurationText
    {
        get => _audioDurationText;
        private set
        {
            if (string.Equals(_audioDurationText, value, StringComparison.Ordinal))
                return;

            _audioDurationText = value;
            OnPropertyChanged(nameof(AudioDurationText));
        }
    }

    public double AudioProgress
    {
        get => _audioProgress;
        private set
        {
            if (Math.Abs(_audioProgress - value) < 0.0001)
                return;

            _audioProgress = value;
            OnPropertyChanged(nameof(AudioProgress));
        }
    }

    public bool CanSeekAudio => _audioPreviewSession?.CanSeek ?? false;

    public bool HasModelPreview => ModelPreview is not null;

    public ModelPreviewData? ModelPreview { get; private set; }

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

    public bool HasAnyPreview =>
        HasImagePreview || HasFallbackPreview || HasTextPreview || HasAudioPreview || HasModelPreview;

    public bool ShowUnsupportedModelMessage =>
        string.Equals(SelectedAsset?.Category, "3D Model", StringComparison.OrdinalIgnoreCase) && !HasModelPreview;

    public bool ShowUnsupportedAudioMessage =>
        string.Equals(SelectedAsset?.Category, "Audio", StringComparison.OrdinalIgnoreCase) && !HasAudioPreview;

    public int PreviewTabIndex
    {
        get => _previewTabIndex;
        set
        {
            if (_previewTabIndex == value)
                return;

            _previewTabIndex = value;
            OnPropertyChanged(nameof(PreviewTabIndex));
        }
    }

    public RelayCommand PlayAudioPreviewCommand { get; }

    public RelayCommand StopAudioPreviewCommand { get; }

    public RelayCommand NodeToggleCommand { get; }

    public RelayCommand CollapseAllFoldersCommand { get; }

    public int AssetCount => Assets.Count;

    public bool HasAssets => Assets.Count > 0;

    public bool ShowEmptyState => !IsLoading && !HasError && !HasAssets;

    public string WindowTitle => string.IsNullOrWhiteSpace(PackageName)
        ? "Package Preview"
        : $"Preview - {PackageName}";

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _disposeCts.Cancel();
        _disposeCts.Dispose();

        Assets.CollectionChanged -= OnAssetsCollectionChanged;
        SecurityThreats.CollectionChanged -= OnSecurityThreatsCollectionChanged;

        DisposeBitmap(ref _primaryImagePreview);
        DisposeBitmap(ref _fallbackPreviewImage);
        ResetAudioPreview();
        StopAudioTimer();
        _audioTimer = null;
        TextPreview = null;
        ModelPreview = null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Task EnsureLoadedAsync()
    {
        ThrowIfDisposed();
        LoggingService.LogInformation($"EnsureLoadedAsync invoked for '{PackagePath}'.");
        return _loadTask ??= LoadInternalAsync();
    }

    private async Task LoadInternalAsync()
    {
        using var loadCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);
        var cancellationToken = loadCts.Token;
        LoggingService.LogInformation($"Loading package preview for '{PackagePath}'.");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    IsLoading = true;
                    HasError = false;
                    StatusMessage = "Reading package contents...";
                },
                DispatcherPriority.Background);

            var preview = await _previewService.LoadPreviewAsync(PackagePath, cancellationToken)
                .ConfigureAwait(false);

            LoggingService.LogInformation(
                $"Preview service returned {preview.Assets.Count} assets (totalSize={preview.TotalAssetSizeBytes}) for '{PackagePath}'.");

            await Dispatcher.UIThread.InvokeAsync(
                () => ApplyPreview(preview),
                DispatcherPriority.Background,
                cancellationToken);

            await Dispatcher.UIThread.InvokeAsync(
                () => StatusMessage = $"{AssetCount} assets ready.",
                DispatcherPriority.Background);
        }
        catch (OperationCanceledException)
        {
            // No-op: the window was closed.
            LoggingService.LogInformation($"Preview loading cancelled for '{PackagePath}'.");
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    _allAssets.Clear();
                    RefreshCategories();
                    ApplyCategoryFilter();
                    HasError = true;
                    StatusMessage = $"Failed to load preview: {ex.Message}";
                },
                DispatcherPriority.Background);
            LoggingService.LogError($"Failed to load package preview for '{PackagePath}'.", ex);
        }
        finally
        {
            stopwatch.Stop();
            LoggingService.LogInformation(
                $"Preview loading finished for '{PackagePath}' in {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");

            await Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    IsLoading = false;
                    UpdateCollectionsState();
                },
                DispatcherPriority.Background);
        }
    }

    private void ApplyPreview(UnityPackagePreviewResult preview)
    {
        PackageName = preview.PackageName;
        PackageSizeText = FormatFileSize(preview.PackageSizeBytes);
        TotalAssetSizeText = FormatFileSize(preview.TotalAssetSizeBytes);
        PackageModifiedText = preview.LastModifiedUtc?.ToLocalTime().ToString("g");

        _allAssets.Clear();
        _directoriesToPrune.Clear();
        if (preview.DirectoriesToPrune.Count > 0)
            _directoriesToPrune.UnionWith(preview.DirectoriesToPrune);

        foreach (var asset in preview.Assets)
            _allAssets.Add(new UnityPackageAssetPreviewItem(asset));

        RefreshCategories();
        ApplyCategoryFilter();
        LoggingService.LogInformation(
            $"Preview applied for '{PackagePath}'. AssetCount={_allAssets.Count}, Categories={Categories.Count}, DirectoriesToPrune={_directoriesToPrune.Count}.");
    }

    private Task? RunSecurityScanAsync()
    {
        if (_securityScanProvider is null)
            return null;

        return Task.Run(async () =>
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SecurityScanInProgress = true;
                    SecurityScanFailed = false;
                    SecurityErrorText = null;
                    SecurityStatusText = "Scanning for malicious code...";
                }, DispatcherPriority.Background);

                var result = await _securityScanProvider().ConfigureAwait(false);
                await Dispatcher.UIThread.InvokeAsync(
                    () => ApplySecurityScanResult(result),
                    DispatcherPriority.Background);
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SecurityScanInProgress = false;
                    SecurityStatusText = "Security scan cancelled.";
                }, DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Security scan failed for preview '{PackagePath}'.", ex);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SecurityScanInProgress = false;
                    SecurityScanFailed = true;
                    SecurityStatusText = "Security scan failed.";
                    SecurityErrorText = "Unable to determine if the package is safe.";
                }, DispatcherPriority.Background);
            }
        }, _disposeCts.Token);
    }

    private void ApplySecurityScanResult(MaliciousCodeScanResult? result)
    {
        _securityScanResult = result;
        SecurityThreats.Clear();

        if (result?.Threats is { Count: > 0 })
        {
            foreach (var threat in result.Threats)
                SecurityThreats.Add(SecurityThreatDisplay.FromThreat(threat));

            SecurityStatusText = "Potentially malicious content detected.";
            SecurityScanFailed = false;
            SecurityErrorText = null;
        }
        else if (result is not null)
        {
            SecurityStatusText = "No malicious code detected.";
            SecurityScanFailed = false;
            SecurityErrorText = null;
        }
        else
        {
            SecurityStatusText = "Security scan unavailable.";
            SecurityScanFailed = true;
            SecurityErrorText = "This package could not be scanned.";
        }

        SecurityScanInProgress = false;
    }

    private void OnSecurityThreatsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SecurityHasThreats));
        OnPropertyChanged(nameof(ShowSecuritySection));
    }

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

    private void UpdateSelectedPreviewContent()
    {
        var asset = SelectedAsset;
        var assetLabel = asset?.RelativePath ?? asset?.FileName ?? "<none>";
        LoggingService.LogInformation($"Updating preview content for '{assetLabel}'.");

        DisposeBitmap(ref _primaryImagePreview);
        DisposeBitmap(ref _fallbackPreviewImage);
        ResetAudioPreview();
        TextPreview = null;
        IsTextPreviewTruncated = false;
        ModelPreview = null;
        AudioStatusText = "Ready";
        AudioPositionText = FormatTime(TimeSpan.Zero);
        AudioDurationText = FormatTime(TimeSpan.Zero);
        AudioProgress = 0;

        if (asset is not null)
        {
            TryCreatePrimaryImagePreview(asset);
            TryCreateFallbackPreview(asset);
            TryCreateTextPreview(asset);
            TryCreateAudioPreview(asset);
            TryCreateModelPreview(asset);
        }

        LoggingService.LogInformation(
            $"Preview state for '{assetLabel}': HasImage={_primaryImagePreview is not null}, HasFallback={_fallbackPreviewImage is not null}, HasText={TextPreview is not null}, HasAudio={_audioPreviewSession is not null}, HasModel={ModelPreview is not null}.");

        RaisePreviewPropertyChanges();
        PreviewTabIndex = DetermineDefaultPreviewTabIndex();
        UpdateAudioCommands();
    }

    private void RaisePreviewPropertyChanges()
    {
        OnPropertyChanged(nameof(ImagePreview));
        OnPropertyChanged(nameof(HasImagePreview));
        OnPropertyChanged(nameof(FallbackPreviewImage));
        OnPropertyChanged(nameof(HasFallbackPreview));
        OnPropertyChanged(nameof(HasAnyImagePreview));
        OnPropertyChanged(nameof(TextPreview));
        OnPropertyChanged(nameof(HasTextPreview));
        OnPropertyChanged(nameof(IsTextPreviewTruncated));
        OnPropertyChanged(nameof(HasAudioPreview));
        OnPropertyChanged(nameof(IsAudioPlaying));
        OnPropertyChanged(nameof(CanSeekAudio));
        OnPropertyChanged(nameof(ModelPreview));
        OnPropertyChanged(nameof(HasModelPreview));
        OnPropertyChanged(nameof(HasAnyPreview));
        OnPropertyChanged(nameof(ShowUnsupportedModelMessage));
        OnPropertyChanged(nameof(ShowUnsupportedAudioMessage));
    }

    private void TryCreatePrimaryImagePreview(UnityPackageAssetPreviewItem asset)
    {
        if (_primaryImagePreview is not null)
            return;

        if (asset.AssetData is not { Length: > 0 } || asset.IsAssetDataTruncated)
            return;

        if (IsImageExtension(asset.Extension))
        {
            try
            {
                using var memoryStream = new MemoryStream(asset.AssetData);
                _primaryImagePreview = new Bitmap(memoryStream);
                LoggingService.LogInformation($"Primary image preview created for '{asset.RelativePath}'.");
            }
            catch
            {
                DisposeBitmap(ref _primaryImagePreview);
                LoggingService.LogError($"Failed to create primary image preview for '{asset.RelativePath}'.");
            }

            return;
        }

        if (!IsPdfExtension(asset.Extension))
            return;

        _primaryImagePreview = TryCreatePdfBitmap(asset.AssetData);
        if (_primaryImagePreview is null)
        {
            DisposeBitmap(ref _primaryImagePreview);
            LoggingService.LogError($"Failed to render PDF preview for '{asset.RelativePath}'.");
        }
        else
        {
            LoggingService.LogInformation($"PDF preview generated for '{asset.RelativePath}'.");
        }
    }

    private static Bitmap? TryCreatePdfBitmap(byte[] pdfData)
    {
        if (pdfData.Length == 0)
            return null;

        try
        {
            using var document = DocLib.Instance.GetDocReader(pdfData, PdfPreviewDimensions);
            var pageCount = document.GetPageCount();
            if (pageCount <= 0)
                return null;

            using var page = document.GetPageReader(0);
            var width = page.GetPageWidth();
            var height = page.GetPageHeight();
            if (width <= 0 || height <= 0)
                return null;

            var pixelData = page.GetImage(RenderFlags.RenderAnnotations);
            if (pixelData is null || pixelData.Length == 0)
                return null;

            var bitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            using (var buffer = bitmap.Lock())
            {
                var srcStride = width * 4;
                var dstStride = buffer.RowBytes;
                var rows = Math.Min(height, buffer.Size.Height);

                if (srcStride == dstStride)
                    Marshal.Copy(pixelData, 0, buffer.Address, srcStride * rows);
                else
                    for (var row = 0; row < rows; row++)
                    {
                        var srcOffset = row * srcStride;
                        var destPtr = buffer.Address + row * dstStride;
                        Marshal.Copy(pixelData, srcOffset, destPtr, Math.Min(srcStride, dstStride));
                    }
            }

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private void TryCreateFallbackPreview(UnityPackageAssetPreviewItem asset)
    {
        if (_fallbackPreviewImage is not null)
            return;

        if (asset.PreviewImageData is not { Length: > 0 })
            return;

        try
        {
            using var memoryStream = new MemoryStream(asset.PreviewImageData);
            _fallbackPreviewImage = new Bitmap(memoryStream);
            LoggingService.LogInformation($"Fallback preview image created for '{asset.RelativePath}'.");
        }
        catch
        {
            DisposeBitmap(ref _fallbackPreviewImage);
            LoggingService.LogError($"Failed to create fallback preview for '{asset.RelativePath}'.");
        }
    }

    private void TryCreateTextPreview(UnityPackageAssetPreviewItem asset)
    {
        if (asset.AssetData is not { Length: > 0 })
            return;

        if (!IsTextCandidate(asset))
            return;

        const int maxPreviewBytes = 256 * 1024;
        var data = asset.AssetData;
        var sliceLength = Math.Min(data.Length, maxPreviewBytes);
        var buffer = sliceLength == data.Length ? data : data[..sliceLength];
        var (text, usedEncoding) = TryDecodeText(buffer);
        if (text is null)
            return;

        if (sliceLength < data.Length || asset.IsAssetDataTruncated)
        {
            text +=
                $"{Environment.NewLine}{Environment.NewLine}... Preview truncated (showing first {FormatFileSize(sliceLength)}).";
            IsTextPreviewTruncated = true;
        }
        else
        {
            IsTextPreviewTruncated = false;
        }

        if (usedEncoding is not null && !usedEncoding.Equals(Encoding.UTF8))
            text = $"// Encoding: {usedEncoding.EncodingName}{Environment.NewLine}{text}";

        TextPreview = text;
        LoggingService.LogInformation(
            $"Generated text preview for '{asset.RelativePath}' (length={text.Length}).");
    }

    private void TryCreateAudioPreview(UnityPackageAssetPreviewItem asset)
    {
        ResetAudioPreview();

        var hasData = asset.AssetData is { Length: > 0 };
        var hasFile = !string.IsNullOrWhiteSpace(asset.AssetFilePath);

        if (!hasData && !hasFile)
            return;

        if (!AudioPreviewSession.Supports(asset.Extension, hasFile))
            return;

        var session = AudioPreviewSession.TryCreate(asset.AssetData, asset.Extension, asset.AssetFilePath);
        if (session is null)
        {
            AudioStatusText = "Audio preview unsupported";
            LoggingService.LogInformation($"Audio preview unsupported for '{asset.RelativePath}'.");
            return;
        }

        _audioPreviewSession = session;
        _audioPreviewSession.PlaybackStopped += HandleAudioPlaybackStopped;
        AudioDurationText = FormatTime(session.TotalTime);
        AudioPositionText = FormatTime(TimeSpan.Zero);
        AudioStatusText = "Ready";
        AudioProgress = 0;
        LoggingService.LogInformation(
            $"Audio preview ready for '{asset.RelativePath}'. Duration={session.TotalTime}.");
    }

    private void TryCreateModelPreview(UnityPackageAssetPreviewItem asset)
    {
        if (asset.AssetData is not { Length: > 0 } || asset.IsAssetDataTruncated)
            return;

        if (!IsModelExtension(asset.Extension))
            return;

        if (string.Equals(asset.Extension, ".obj", StringComparison.OrdinalIgnoreCase))
        {
            ModelPreview = ObjModelParser.TryParse(asset.AssetData);
            LoggingService.LogInformation(
                ModelPreview is null
                    ? $"Failed to generate OBJ model preview for '{asset.RelativePath}'."
                    : $"OBJ model preview generated for '{asset.RelativePath}'.");
        }
        else
        {
            ModelPreview = null;
        }
    }

    private void ResetAudioPreview()
    {
        StopAudioTimer();
        if (_audioPreviewSession is null)
            return;

        _audioPreviewSession.PlaybackStopped -= HandleAudioPlaybackStopped;
        _audioPreviewSession.Dispose();
        _audioPreviewSession = null;
        LoggingService.LogInformation("Audio preview session reset.");
    }

    private void ToggleAudioPlayback()
    {
        if (_audioPreviewSession is null)
            return;

        if (_audioPreviewSession.IsPlaying)
        {
            _audioPreviewSession.Pause();
            AudioStatusText = "Paused";
            StopAudioTimer();
            LoggingService.LogInformation("Audio preview paused.");
        }
        else
        {
            if (_audioPreviewSession.IsCompleted)
                _audioPreviewSession.Rewind();

            _audioPreviewSession.Play();
            AudioStatusText = "Playing";
            StartAudioTimer();
            LoggingService.LogInformation("Audio preview playback started.");
        }

        OnPropertyChanged(nameof(IsAudioPlaying));
        AudioPositionText = FormatTime(_audioPreviewSession.CurrentTime);
        AudioDurationText = FormatTime(_audioPreviewSession.TotalTime);
    }

    private void StopAudioPlayback()
    {
        if (_audioPreviewSession is null)
            return;

        _audioPreviewSession.Stop();
        AudioStatusText = "Stopped";
        StopAudioTimer();
        AudioPositionText = FormatTime(TimeSpan.Zero);
        AudioProgress = 0;
        OnPropertyChanged(nameof(IsAudioPlaying));
        LoggingService.LogInformation("Audio preview stopped.");
    }

    private void HandleAudioPlaybackStopped(object? sender, AudioPlaybackStoppedEventArgs e)
    {
        StopAudioTimer();
        if (_audioPreviewSession is null)
            return;

        AudioPositionText = FormatTime(_audioPreviewSession.CurrentTime);
        AudioDurationText = FormatTime(_audioPreviewSession.TotalTime);
        AudioProgress = e.Completed && _audioPreviewSession.TotalTime.TotalSeconds > 0
            ? 1
            : 0;
        if (!e.Completed)
            AudioProgress = 0;
        AudioStatusText = e.Completed ? "Completed" : "Stopped";
        OnPropertyChanged(nameof(IsAudioPlaying));
        LoggingService.LogInformation(
            $"Audio playback stopped. Completed={e.Completed}, Position={_audioPreviewSession.CurrentTime}.");
    }

    private void StartAudioTimer()
    {
        _audioTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _audioTimer.Tick -= OnAudioTimerTick;
        _audioTimer.Tick += OnAudioTimerTick;
        _audioTimer.Start();
    }

    private void StopAudioTimer()
    {
        if (_audioTimer is null)
            return;

        _audioTimer.Stop();
        _audioTimer.Tick -= OnAudioTimerTick;
    }

    private void OnAudioTimerTick(object? sender, EventArgs e)
    {
        if (_audioPreviewSession is null)
        {
            StopAudioTimer();
            return;
        }

        AudioPositionText = FormatTime(_audioPreviewSession.CurrentTime);
        AudioDurationText = FormatTime(_audioPreviewSession.TotalTime);
        var totalSeconds = _audioPreviewSession.TotalTime.TotalSeconds;
        AudioProgress = totalSeconds > 0
            ? Math.Clamp(_audioPreviewSession.CurrentTime.TotalSeconds / totalSeconds, 0, 1)
            : 0;

        OnPropertyChanged(nameof(IsAudioPlaying));
    }

    private void UpdateAudioCommands()
    {
        PlayAudioPreviewCommand.RaiseCanExecuteChanged();
        StopAudioPreviewCommand.RaiseCanExecuteChanged();
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

    private int DetermineDefaultPreviewTabIndex()
    {
        if (HasImagePreview)
            return 0;
        if (HasFallbackPreview)
            return 1;
        if (HasTextPreview)
            return 2;
        if (HasAudioPreview)
            return 3;
        if (HasModelPreview)
            return 4;
        return 0;
    }

    private static void DisposeBitmap(ref Bitmap? bitmap)
    {
        bitmap?.Dispose();
        bitmap = null;
    }

    private static bool IsImageExtension(string extension)
    {
        return UnityAssetClassification.IsTextureExtension(extension);
    }

    private static bool IsPdfExtension(string extension)
    {
        return UnityAssetClassification.IsPdfExtension(extension);
    }

    private static bool IsModelExtension(string extension)
    {
        return UnityAssetClassification.IsModelExtension(extension);
    }

    private static bool IsTextExtension(string extension)
    {
        return extension switch
        {
            ".cs" or ".js" or ".boo" or ".shader" or ".cg" or ".cginc" or ".compute" or ".hlsl" or ".glsl"
                or ".shadergraph"
                or ".shadersubgraph" or ".txt" or ".json"
                or ".xml" or ".yaml" or ".yml" or ".asmdef" or ".prefab" or ".mat" or ".anim" or ".controller"
                or ".overridecontroller" or ".mask" or ".meta" or ".uxml" or ".uss" => true,
            _ => false
        };
    }

    private static bool IsTextCandidate(UnityPackageAssetPreviewItem asset)
    {
        if (IsTextExtension(asset.Extension))
            return true;

        if (asset.Category is "Script" or "Animation")
            return true;

        if (asset.AssetData is not { Length: > 0 })
            return false;

        return IsLikelyText(asset.AssetData);
    }

    private static bool IsLikelyText(byte[] data)
    {
        var length = Math.Min(data.Length, 4096);
        var nonPrintable = 0;
        for (var i = 0; i < length; i++)
        {
            var b = data[i];
            if (b == 0)
                return false;

            if (b < 9 || (b > 13 && b < 32))
                nonPrintable++;
        }

        return nonPrintable / (double)length < 0.2;
    }

    private static (string? Text, Encoding? Encoding) TryDecodeText(byte[] data)
    {
        Encoding? encoding = null;
        string? text = null;

        var encodings = new[]
        {
            new UTF8Encoding(false, true),
            Encoding.Unicode,
            Encoding.BigEndianUnicode,
            Encoding.UTF32
        };

        foreach (var candidate in encodings)
            try
            {
                text = candidate.GetString(data);
                encoding = candidate;
                break;
            }
            catch
            {
                // Try next encoding
            }

        if (text is null)
            try
            {
                text = Encoding.GetEncoding("ISO-8859-1").GetString(data);
                encoding = Encoding.GetEncoding("ISO-8859-1");
            }
            catch
            {
                text = null;
                encoding = null;
            }

        return (text, encoding);
    }

    private static string FormatTime(TimeSpan timeSpan)
    {
        return timeSpan.TotalHours >= 1
            ? timeSpan.ToString(@"hh\:mm\:ss")
            : timeSpan.ToString(@"mm\:ss");
    }

    private void OnAssetsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateCollectionsState();
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0)
            return "0 B";

        var units = new[] { "B", "KB", "MB", "GB", "TB", "PB" };
        var size = (double)bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{bytes} {units[unitIndex]}"
            : $"{size:0.##} {units[unitIndex]}";
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(UnityPackagePreviewViewModel));
    }
}

public sealed class SecurityThreatDisplay
{
    private SecurityThreatDisplay(
        string title,
        string description,
        MaliciousThreatSeverity severity,
        IReadOnlyList<string> matches)
    {
        Title = title;
        Description = description;
        Severity = severity;
        Matches = matches;
    }

    public string Title { get; }

    public string Description { get; }

    public MaliciousThreatSeverity Severity { get; }

    public IReadOnlyList<string> Matches { get; }

    public bool HasMatches => Matches.Count > 0;

    public string SeverityLabel => Severity switch
    {
        MaliciousThreatSeverity.High => "HIGH",
        MaliciousThreatSeverity.Medium => "MEDIUM",
        _ => "LOW"
    };

    public static SecurityThreatDisplay FromThreat(MaliciousThreat threat)
    {
        if (threat is null)
            throw new ArgumentNullException(nameof(threat));

        var matches = threat.Matches is { Count: > 0 }
            ? threat.Matches
                .Select(match => $"{match.FilePath}: {match.Snippet}")
                .Take(6)
                .ToList()
            : new List<string>();

        return new SecurityThreatDisplay(
            ResolveThreatTitle(threat.Type),
            threat.Description,
            threat.Severity,
            matches);
    }

    private static string ResolveThreatTitle(MaliciousThreatType type)
    {
        return type switch
        {
            MaliciousThreatType.DiscordWebhook => "Discord webhook detected",
            MaliciousThreatType.UnsafeLinks => "Unsafe links detected",
            MaliciousThreatType.SuspiciousCodePatterns => "Suspicious code patterns",
            _ => type.ToString()
        };
    }
}

public sealed class UnityPackageAssetPreviewItem
{
    public UnityPackageAssetPreviewItem(UnityPackagePreviewAsset asset)
    {
        if (asset is null)
            throw new ArgumentNullException(nameof(asset));

        RelativePath = asset.RelativePath;
        FileName = Path.GetFileName(RelativePath);
        Directory = Path.GetDirectoryName(RelativePath) ?? string.Empty;
        AssetSizeBytes = asset.AssetSizeBytes;
        HasMetaFile = asset.HasMetaFile;
        PreviewImageData = asset.PreviewImageData;
        AssetData = asset.AssetData;
        IsAssetDataTruncated = asset.IsAssetDataTruncated;
        AssetFilePath = asset.AssetFilePath;

        Extension = Path.GetExtension(RelativePath)?.ToLowerInvariant() ?? string.Empty;
        Category = UnityAssetClassification.ResolveCategory(
            asset.RelativePath,
            asset.AssetSizeBytes,
            asset.AssetData is { Length: > 0 });
        SizeText = FormatFileSize(AssetSizeBytes);
    }

    public string RelativePath { get; }

    public string FileName { get; }

    public string Directory { get; }

    public long AssetSizeBytes { get; }

    public bool HasMetaFile { get; }

    public byte[]? PreviewImageData { get; }

    public byte[]? AssetData { get; }

    public string? AssetFilePath { get; }

    public bool IsAssetDataTruncated { get; }

    public string Extension { get; }

    public string Category { get; }

    public bool HasPreview => PreviewImageData is { Length: > 0 };

    public string SizeText { get; }

    public Geometry IconGeometry => UnityAssetIconProvider.GetAssetIcon(Category);

    [SuppressMessage("Globalization", "CA1305:Specify IFormatProvider")]
    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0)
            return "0 B";

        var units = new[] { "B", "KB", "MB", "GB", "TB", "PB" };
        var size = (double)bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{bytes} {units[unitIndex]}"
            : $"{size:0.##} {units[unitIndex]}";
    }
}

public sealed class UnityPackageAssetTreeNode : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isSelected;

    public UnityPackageAssetTreeNode(string name, string fullPath, bool isFolder, UnityPackageAssetPreviewItem? asset,
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