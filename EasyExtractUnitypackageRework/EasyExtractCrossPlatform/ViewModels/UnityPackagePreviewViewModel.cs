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

public sealed partial class UnityPackagePreviewViewModel : INotifyPropertyChanged, IDisposable
{
    private const string AllCategory = "All assets";
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly IUnityPackagePreviewService _previewService;
    private readonly Func<Task<MaliciousCodeScanResult?>>? _securityScanProvider;


    private bool _hasError;
    private bool _isDisposed;
    private bool _isLoading;

    private Task? _loadTask;
    private string? _packageModifiedText;
    private string _packageName = string.Empty;
    private string _packageSizeText = string.Empty;
    private string? _statusMessage;
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

