using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using EasyExtractCrossPlatform.Models;
using EasyExtractCrossPlatform.Services;
using EasyExtractCrossPlatform.Utilities;
using EasyExtractCrossPlatform.ViewModels;

namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private const string UnityPackageExtension = ".unitypackage";
    private const string UnknownVersionLabel = "Version unknown";

    private const string ProUpgradeInfoUrl = "https://github.com/HakuSystems/EasyExtractUnitypackage#readme";

    private static readonly HttpClient BackgroundHttpClient = new();
    private readonly Button? _batchExtractionButton;
    private readonly Button? _cancelExtractionButton;
    private readonly Button? _checkUpdatesButton;
    private readonly Button? _clearQueueButton;
    private readonly IBrush _defaultBackgroundBrush;
    private readonly string _defaultDropPrimaryText = "Drag & drop files here";
    private readonly string _defaultDropSecondaryText = "Supports batch extraction and live progress updates.";
    private readonly Border? _dropZoneBorder;
    private readonly Grid? _dropZoneHostGrid;
    private readonly TextBlock? _dropZonePrimaryTextBlock;
    private readonly TextBlock? _dropZoneSecondaryTextBlock;
    private readonly EverythingSearchView? _everythingSearchView;
    private readonly Border? _extractionDashboard;
    private readonly TextBlock? _extractionDashboardAssetCount;
    private readonly TextBlock? _extractionDashboardAssetText;
    private readonly TextBlock? _extractionDashboardElapsed;
    private readonly TextBlock? _extractionDashboardNextPackage;
    private readonly TextBlock? _extractionDashboardOutputText;
    private readonly TextBlock? _extractionDashboardPackageText;
    private readonly ProgressBar? _extractionDashboardProgressBar;
    private readonly TextBlock? _extractionDashboardQueueCount;
    private readonly TextBlock? _extractionDashboardSubtitle;
    private readonly DispatcherTimer _extractionElapsedTimer;
    private readonly TextBlock? _extractionOverviewAssetsExtractedText;
    private readonly TextBlock? _extractionOverviewLastExtractionText;
    private readonly TextBlock? _extractionOverviewPackagesCompletedText;
    private readonly IUnityPackageExtractionService _extractionService = new UnityPackageExtractionService();
    private readonly Border? _licenseTierBadge;
    private readonly TextBlock? _licenseTierTextBlock;
    private readonly Border? _overlayCard;
    private readonly ContentControl? _overlayContent;
    private readonly Border? _overlayHost;
    private readonly IUnityPackagePreviewService _previewService = new UnityPackagePreviewService();
    private readonly Button? _processQueueButton;
    private readonly Control? _queueEmptyState;
    private readonly ObservableCollection<QueueItemDisplay> _queueItems = new();
    private readonly Dictionary<string, QueueItemDisplay> _queueItemsByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly ItemsControl? _queueItemsControl;
    private readonly ScrollViewer? _queueItemsScrollViewer;
    private readonly TextBlock? _queueSummaryTextBlock;
    private readonly Border? _searchHintContainer;
    private readonly Border? _searchIconBorder;
    private readonly Border? _searchResultsBorder;
    private readonly Border? _searchRevealHost;
    private readonly Button? _startExtractionButton;
    private readonly Grid? _startExtractionHeaderGrid;
    private readonly string[] _startupArguments;
    private readonly TextBox? _unityPackageSearchBox;
    private readonly IUpdateService _updateService = UpdateService.Instance;
    private readonly Button? _upgradeButton;
    private readonly TextBlock? _versionTextBlock;
    private Control? _activeOverlayContent;
    private UpdateManifest? _activeUpdateManifest;
    private object? _checkUpdatesButtonOriginalContent;
    private Bitmap? _currentBackgroundBitmap;
    private string? _currentVersionDisplay;
    private IDisposable? _dropStatusReset;
    private IDisposable? _dropSuccessReset;
    private IDisposable? _dropZoneVisibilityReset;
    private CancellationTokenSource? _extractionCts;
    private IDisposable? _extractionDashboardHideReset;
    private int _extractionOverviewAssetBaseline;
    private int _extractionOverviewCurrentPackageAssets;
    private int _extractionOverviewPackageBaseline;
    private int _extractionOverviewSessionAssets;
    private int _extractionOverviewSessionPackages;
    private DateTimeOffset? _extractionOverviewStartTime;
    private Stopwatch? _extractionStopwatch;
    private FeedbackWindow? _feedbackWindow;
    private bool _isCheckingForUpdates;
    private bool _isExtractionCancelling;
    private bool _isExtractionOverviewLive;
    private bool _isExtractionRunning;
    private bool _isSearchHover;
    private bool _isUpdateDownloadInProgress;
    private bool _lastDiscordPresenceEnabled;
    private PixelPoint? _lastNormalPosition;
    private Size? _lastNormalSize;
    private double? _lastUpdatePercentage;
    private UpdatePhase? _lastUpdatePhase;
    private DateTime _lastUpdateUiRefresh = DateTime.MinValue;
    private CancellationTokenSource? _overlayAnimationCts;
    private ScaleTransform? _overlayCardScaleTransform;
    private List<string>? _pendingStartupExtractions;
    private UpdateManifest? _pendingUpdateManifest;
    private AppSettings _settings = new();
    private SettingsWindow? _settingsWindow;
    private IDisposable? _versionStatusReset;

    public MainWindow(string[]? startupArguments = null)
    {
        _startupArguments = startupArguments ?? Array.Empty<string>();

        InitializeComponent();
        _defaultBackgroundBrush = ResolveDefaultBackgroundBrush();
        _dropZoneBorder = this.FindControl<Border>("DropZoneBorder");
        _dropZoneHostGrid = this.FindControl<Grid>("DropZoneHostGrid");
        _dropZonePrimaryTextBlock = this.FindControl<TextBlock>("DropZonePrimaryTextBlock");
        _dropZoneSecondaryTextBlock = this.FindControl<TextBlock>("DropZoneSecondaryTextBlock");
        if (_dropZonePrimaryTextBlock?.Text is { Length: > 0 } primaryText)
            _defaultDropPrimaryText = primaryText;
        if (_dropZoneSecondaryTextBlock?.Text is { Length: > 0 } secondaryText)
            _defaultDropSecondaryText = secondaryText;
        _startExtractionHeaderGrid = this.FindControl<Grid>("StartExtractionHeaderGrid");
        _versionTextBlock = this.FindControl<TextBlock>("VersionTextBlock");
        _licenseTierBadge = this.FindControl<Border>("LicenseTierBadge");
        _licenseTierTextBlock = this.FindControl<TextBlock>("LicenseTierTextBlock");
        _upgradeButton = this.FindControl<Button>("UpgradeButton");
        _everythingSearchView = this.FindControl<EverythingSearchView>("EverythingSearch");
        _searchResultsBorder = this.FindControl<Border>("SearchResultsBorder");
        _searchRevealHost = this.FindControl<Border>("SearchRevealHost");
        _searchIconBorder = this.FindControl<Border>("SearchIconBorder");
        _searchHintContainer = this.FindControl<Border>("SearchHintContainer");
        _unityPackageSearchBox = this.FindControl<TextBox>("UnityPackageSearchBox");
        if (_everythingSearchView?.ViewModel is { } searchViewModel)
        {
            SearchViewModel = searchViewModel;
            SearchViewModel.PropertyChanged += OnSearchViewModelPropertyChanged;
            if (_unityPackageSearchBox is not null)
            {
                _unityPackageSearchBox.DataContext = SearchViewModel;
                _unityPackageSearchBox.KeyBindings.Clear();
                _unityPackageSearchBox.KeyBindings.Add(new KeyBinding
                {
                    Gesture = new KeyGesture(Key.Enter),
                    Command = SearchViewModel.SearchCommand
                });
                _unityPackageSearchBox.KeyBindings.Add(new KeyBinding
                {
                    Gesture = new KeyGesture(Key.Escape),
                    Command = SearchViewModel.ClearCommand
                });
            }
        }

        UpdateSearchUiState();

        _startExtractionButton = this.FindControl<Button>("StartExtractionButton");
        _cancelExtractionButton = this.FindControl<Button>("CancelExtractionButton");
        _batchExtractionButton = this.FindControl<Button>("BatchExtractionButton");
        _processQueueButton = this.FindControl<Button>("ProcessQueueButton");
        _extractionDashboard = this.FindControl<Border>("ExtractionDashboard");
        _extractionDashboardSubtitle = this.FindControl<TextBlock>("ExtractionDashboardSubtitle");
        _extractionDashboardQueueCount = this.FindControl<TextBlock>("ExtractionDashboardQueueCount");
        _extractionDashboardProgressBar = this.FindControl<ProgressBar>("ExtractionDashboardProgressBar");
        _extractionDashboardPackageText = this.FindControl<TextBlock>("ExtractionDashboardPackageText");
        _extractionDashboardAssetText = this.FindControl<TextBlock>("ExtractionDashboardAssetText");
        _extractionDashboardOutputText = this.FindControl<TextBlock>("ExtractionDashboardOutputText");
        _extractionDashboardAssetCount = this.FindControl<TextBlock>("ExtractionDashboardAssetCount");
        _extractionDashboardElapsed = this.FindControl<TextBlock>("ExtractionDashboardElapsed");
        _extractionDashboardNextPackage = this.FindControl<TextBlock>("ExtractionDashboardNextPackage");
        _extractionOverviewPackagesCompletedText =
            this.FindControl<TextBlock>("ExtractionOverviewPackagesCompletedText");
        _extractionOverviewAssetsExtractedText =
            this.FindControl<TextBlock>("ExtractionOverviewAssetsExtractedText");
        _extractionOverviewLastExtractionText =
            this.FindControl<TextBlock>("ExtractionOverviewLastExtractionText");
        _checkUpdatesButton = this.FindControl<Button>("CheckUpdatesButton");
        if (_checkUpdatesButton is not null)
            _checkUpdatesButtonOriginalContent = _checkUpdatesButton.Content;
        _overlayHost = this.FindControl<Border>("OverlayHost");
        _overlayContent = this.FindControl<ContentControl>("OverlayContent");
        _overlayCard = this.FindControl<Border>("OverlayCard");
        if (_overlayCard?.RenderTransform is ScaleTransform transform)
        {
            _overlayCardScaleTransform = transform;
        }
        else if (_overlayCard is not null)
        {
            _overlayCardScaleTransform = new ScaleTransform(1, 1);
            _overlayCard.RenderTransform = _overlayCardScaleTransform;
        }

        _queueItemsControl = this.FindControl<ItemsControl>("QueueItemsControl");
        if (_queueItemsControl is not null)
            _queueItemsControl.ItemsSource = _queueItems;
        _queueEmptyState = this.FindControl<Control>("QueueEmptyState");
        _queueSummaryTextBlock = this.FindControl<TextBlock>("QueueSummaryTextBlock");
        _clearQueueButton = this.FindControl<Button>("ClearQueueButton");
        _queueItemsScrollViewer = this.FindControl<ScrollViewer>("QueueItemsScrollViewer");
        UpdateQueueVisualState();
        UpdateExtractionButtonsState();

        _extractionElapsedTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
            IsEnabled = false
        };
        _extractionElapsedTimer.Tick += OnExtractionElapsedTick;

        Closing += OnMainWindowClosing;
        PositionChanged += OnMainWindowPositionChanged;
        PropertyChanged += OnMainWindowPropertyChanged;
        Opened += OnMainWindowOpened;

        LoadSettings();
        SetVersionText();
        QueueDiscordPresenceUpdate("Dashboard");
        InitializeStartupExtractionTargets();
        QueueAutomaticUpdateCheck();
    }

    public EverythingSearchViewModel? SearchViewModel { get; }

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
                    TimeSpan.FromSeconds(3));
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
                    TimeSpan.FromSeconds(3));
            }
            else if (queueResult.AlreadyQueuedCount > 0)
            {
                ShowDropStatusMessage(
                    queueResult.AlreadyQueuedCount == 1
                        ? "Already queued"
                        : "All packages already queued",
                    "Drop different files to add new items.",
                    TimeSpan.FromSeconds(3));
            }
            else
            {
                ShowDropStatusMessage(
                    "No usable files found",
                    "Ensure the packages still exist on disk.",
                    TimeSpan.FromSeconds(3));
            }
        }
        else
        {
            e.DragEffects = DragDropEffects.None;

            if (detectedUnityPackages)
                ShowDropStatusMessage(
                    "Couldn't access dropped package",
                    "Try dropping files directly from a local folder.",
                    TimeSpan.FromSeconds(3));
            else
                ShowDropStatusMessage(
                    "Unsupported files",
                    "Drop .unitypackage files to queue them.",
                    TimeSpan.FromSeconds(3));
        }

        ResetDragClasses();
        e.Handled = true;
    }

    private async void StartExtractionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await RunSingleExtractionPickerAsync();
    }

    private async void BatchExtractionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await RunBatchExtractionPickerAsync();
    }

    private void CancelExtractionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!_isExtractionRunning || _extractionCts is null)
            return;

        if (_isExtractionCancelling)
            return;

        _isExtractionCancelling = true;
        _extractionCts.Cancel();

        UpdateExtractionDashboardSubtitle("Cancelling extraction...");
        UpdateExtractionDashboardAsset("Stopping current operation...");
        if (_extractionDashboardProgressBar is not null)
        {
            _extractionDashboardProgressBar.IsIndeterminate = true;
            _extractionDashboardProgressBar.Value = 0;
        }

        ShowDropStatusMessage("Cancelling extraction", "Stopping the current operation...", TimeSpan.Zero);

        UpdateExtractionButtonsState();
    }

    private async void ProcessQueueButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await RunQueueExtractionAsync();
    }

    private async void PreviewQueueItemButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: QueueItemDisplay display })
            return;

        var sourcePath = !string.IsNullOrWhiteSpace(display.FilePath)
            ? display.FilePath
            : display.NormalizedPath;

        var packagePath = TryNormalizeFilePath(sourcePath);
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
        {
            ShowDropStatusMessage(
                "Preview unavailable",
                "The selected package could not be found on disk.",
                TimeSpan.FromSeconds(4));
            return;
        }

        await OpenPackagePreviewAsync(packagePath);
    }

    private async Task OpenPackagePreviewAsync(string packagePath)
    {
        try
        {
            var previewWindow = new UnityPackagePreviewWindow
            {
                DataContext = new UnityPackagePreviewViewModel(_previewService, packagePath)
            };

            previewWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            await previewWindow.ShowDialog(this);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open preview window: {ex}");
            ShowDropStatusMessage(
                "Preview failed",
                "Unable to open the package preview window.",
                TimeSpan.FromSeconds(4));
        }
    }

    private async Task RunSingleExtractionPickerAsync()
    {
        if (!EnsureExtractionIdle())
            return;

        if (StorageProvider is null)
        {
            ShowDropStatusMessage("File picker unavailable", "Restart the app and try again.", TimeSpan.FromSeconds(4));
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select a Unity package",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Unity package")
                {
                    Patterns = new[] { "*.unitypackage" }
                }
            }
        });

        if (files.Count == 0)
            return;

        var path = TryResolveLocalPath(files[0]);
        if (string.IsNullOrWhiteSpace(path))
        {
            ShowDropStatusMessage("Unsupported location", "Only local files can be extracted.",
                TimeSpan.FromSeconds(4));
            return;
        }

        await RunExtractionSequenceAsync(new[] { new ExtractionItem(path, null) });
    }

    private async Task RunBatchExtractionPickerAsync()
    {
        if (!EnsureExtractionIdle())
            return;

        if (StorageProvider is null)
        {
            ShowDropStatusMessage("File picker unavailable", "Restart the app and try again.", TimeSpan.FromSeconds(4));
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select one or more Unity packages",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Unity packages")
                {
                    Patterns = new[] { "*.unitypackage" }
                }
            }
        });

        if (files.Count == 0)
            return;

        var paths = files
            .Select(TryResolveLocalPath)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(p => new ExtractionItem(p!, null))
            .ToList();

        if (paths.Count == 0)
        {
            ShowDropStatusMessage("No local files selected", "Select files stored on this device.",
                TimeSpan.FromSeconds(4));
            return;
        }

        await RunExtractionSequenceAsync(paths);
    }

    private async Task RunQueueExtractionAsync()
    {
        var queuedPackages = _settings.UnitypackageFiles?
            .Where(p => p is { IsInQueue: true })
            .Select(p => new ExtractionItem(p.FilePath, p))
            .Where(item => !string.IsNullOrWhiteSpace(item.Path))
            .ToList();

        if (queuedPackages is null || queuedPackages.Count == 0)
        {
            ShowDropStatusMessage("Queue is empty", "Add packages before starting extraction.",
                TimeSpan.FromSeconds(4));
            return;
        }

        await RunExtractionSequenceAsync(queuedPackages);
    }

    private async Task RunExtractionSequenceAsync(IReadOnlyList<ExtractionItem> items)
    {
        var validItems = items
            .Where(item => !string.IsNullOrWhiteSpace(item.Path))
            .ToList();

        if (validItems.Count == 0)
            return;

        if (_isExtractionRunning)
        {
            ShowDropStatusMessage("Extraction already running", "Wait for the current extraction to finish.",
                TimeSpan.FromSeconds(4));
            return;
        }

        _isExtractionRunning = true;
        _isExtractionCancelling = false;
        _extractionCts = new CancellationTokenSource();
        UpdateExtractionButtonsState();
        BeginExtractionOverviewSession();
        PrepareExtractionDashboard(validItems);

        try
        {
            for (var index = 0; index < validItems.Count; index++)
            {
                _extractionCts!.Token.ThrowIfCancellationRequested();

                var item = validItems[index];
                var packagePath = item.Path;
                var queueEntry = item.QueueEntry;

                if (!File.Exists(packagePath))
                {
                    ShowDropStatusMessage("Package not found", Path.GetFileName(packagePath), TimeSpan.FromSeconds(4));
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        UpdateExtractionDashboardQueueBadge(Math.Max(0, validItems.Count - index - 1));
                        UpdateExtractionDashboardSubtitle("Waiting for next package...");
                        UpdateExtractionDashboardNextPackageText(ResolveNextPackageName(validItems, index));
                    });

                    if (queueEntry is not null)
                    {
                        queueEntry.IsExtracting = false;
                        await Dispatcher.UIThread.InvokeAsync(() => AddOrUpdateQueueDisplayItem(queueEntry));
                    }

                    continue;
                }

                if (queueEntry is not null)
                {
                    queueEntry.IsExtracting = true;
                    await Dispatcher.UIThread.InvokeAsync(() => AddOrUpdateQueueDisplayItem(queueEntry));
                }

                var outputDirectory = ResolveOutputDirectory(packagePath);
                await Dispatcher.UIThread.InvokeAsync(() =>
                    BeginExtractionDashboardForPackage(packagePath, outputDirectory, validItems, index));

                var progress = new Progress<UnityPackageExtractionProgress>(update =>
                {
                    if (!string.IsNullOrWhiteSpace(update.AssetPath))
                    {
                        UpdateExtractionDashboardProgress(update.AssetPath, update.AssetsExtracted);
                        ShowDropStatusMessage(
                            $"Extracting {Path.GetFileName(packagePath)}",
                            update.AssetPath,
                            TimeSpan.Zero);
                    }
                    else
                    {
                        UpdateExtractionDashboardProgress(null, update.AssetsExtracted);
                    }
                });

                try
                {
                    var result =
                        await ExecuteExtractionAsync(packagePath, outputDirectory, progress, _extractionCts.Token);
                    if (result is not null)
                        await ApplyExtractionSuccessAsync(packagePath, result, queueEntry);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                        CompleteCurrentPackageOnDashboard(
                            packagePath,
                            true,
                            result?.AssetsExtracted ?? 0,
                            remaining: Math.Max(0, validItems.Count - index - 1),
                            nextPackage: ResolveNextPackageName(validItems, index)));
                }
                catch (OperationCanceledException)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                        CompleteCurrentPackageOnDashboard(
                            packagePath,
                            false,
                            0,
                            true,
                            Math.Max(0, validItems.Count - index),
                            ResolveNextPackageName(validItems, index)));

                    if (queueEntry is not null)
                    {
                        queueEntry.IsExtracting = false;
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            AddOrUpdateQueueDisplayItem(queueEntry);
                            UpdateQueueVisualState();
                        });
                    }

                    throw;
                }
                catch (Exception ex)
                {
                    await HandleExtractionFailureAsync(packagePath, ex, queueEntry);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                        CompleteCurrentPackageOnDashboard(
                            packagePath,
                            false,
                            0,
                            remaining: Math.Max(0, validItems.Count - index - 1),
                            nextPackage: ResolveNextPackageName(validItems, index)));
                }
            }
        }
        catch (OperationCanceledException)
        {
            ShowDropStatusMessage("Extraction cancelled", "The operation was cancelled.", TimeSpan.FromSeconds(4));
        }
        finally
        {
            _isExtractionCancelling = false;
            FinishExtractionDashboard(TimeSpan.FromSeconds(2));
            EndExtractionOverviewSession();
            _isExtractionRunning = false;
            _extractionCts?.Dispose();
            _extractionCts = null;
            UpdateExtractionButtonsState();
            RestorePresenceAfterOverlay();
            TryLaunchPendingUpdateAfterExtraction();
        }
    }

    private async Task<UnityPackageExtractionResult?> ExecuteExtractionAsync(
        string packagePath,
        string outputDirectory,
        IProgress<UnityPackageExtractionProgress> progress,
        CancellationToken cancellationToken)
    {
        var options = BuildExtractionOptions();
        return await _extractionService.ExtractAsync(packagePath, outputDirectory, options, progress,
            cancellationToken);
    }

    private async Task ApplyExtractionSuccessAsync(
        string packagePath,
        UnityPackageExtractionResult result,
        UnityPackageFile? queueEntry)
    {
        OnExtractionOverviewPackageCompleted(result.AssetsExtracted);
        UpdateExtractionStatistics(packagePath, result);

        if (queueEntry is not null)
        {
            queueEntry.IsExtracting = false;
            queueEntry.IsInQueue = false;
            var normalized = TryNormalizeFilePath(queueEntry.FilePath);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                RemoveQueueDisplayItem(normalized);
                UpdateQueueVisualState();
            });
        }

        ShowDropStatusMessage("Extraction complete", $"{Path.GetFileName(packagePath)} extracted.",
            TimeSpan.FromSeconds(4));
    }

    private async Task HandleExtractionFailureAsync(
        string packagePath,
        Exception exception,
        UnityPackageFile? queueEntry)
    {
        Debug.WriteLine($"Extraction failed for '{packagePath}': {exception}");

        if (queueEntry is not null)
        {
            queueEntry.IsExtracting = false;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AddOrUpdateQueueDisplayItem(queueEntry);
                UpdateQueueVisualState();
            });
        }

        var message = exception switch
        {
            InvalidDataException => "The package could not be read. It may be damaged.",
            _ => exception.Message
        };

        ShowDropStatusMessage("Extraction failed", message, TimeSpan.FromSeconds(6));
    }

    private void UpdateExtractionStatistics(string packagePath, UnityPackageExtractionResult result)
    {
        _settings.LastExtractionTime = DateTimeOffset.Now;
        _settings.TotalExtracted = Math.Max(0, _settings.TotalExtracted) + 1;
        if (result.AssetsExtracted > 0)
            _settings.TotalFilesExtracted = Math.Max(0, _settings.TotalFilesExtracted) + result.AssetsExtracted;

        if (_settings.ExtractedUnitypackages is null)
            _settings.ExtractedUnitypackages = new List<string>();

        if (_settings.ExtractedUnitypackages.All(existing =>
                !string.Equals(existing, packagePath, StringComparison.OrdinalIgnoreCase)))
            _settings.ExtractedUnitypackages.Add(packagePath);

        var normalizedPackagePath = TryNormalizeFilePath(packagePath);

        if (_settings.UnitypackageFiles is { Count: > 0 })
            for (var index = _settings.UnitypackageFiles.Count - 1; index >= 0; index--)
            {
                var entry = _settings.UnitypackageFiles[index];
                if (entry is null)
                    continue;

                var entryPath = TryNormalizeFilePath(entry.FilePath);
                if (string.Equals(entryPath, normalizedPackagePath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(entry.FilePath, packagePath, StringComparison.OrdinalIgnoreCase))
                    _settings.UnitypackageFiles.RemoveAt(index);
            }

        AppSettingsService.Save(_settings);
        if (!_isExtractionOverviewLive)
            UpdateExtractionOverviewDisplay();
    }

    private UnityPackageExtractionOptions BuildExtractionOptions()
    {
        var tempPath = string.IsNullOrWhiteSpace(_settings.DefaultTempPath)
            ? null
            : _settings.DefaultTempPath;

        return new UnityPackageExtractionOptions(
            _settings.ExtractedCategoryStructure,
            tempPath);
    }

    private string ResolveOutputDirectory(string packagePath)
    {
        var baseOutput = _settings.DefaultOutputPath;
        if (string.IsNullOrWhiteSpace(baseOutput))
            baseOutput = Path.Combine(AppSettingsService.SettingsDirectory, "Extracted");

        var folderName = Path.GetFileNameWithoutExtension(packagePath);
        if (string.IsNullOrWhiteSpace(folderName))
            folderName = "ExtractedPackage";

        var targetPath = Path.Combine(baseOutput, folderName);
        Directory.CreateDirectory(targetPath);
        return targetPath;
    }

    private bool EnsureExtractionIdle()
    {
        if (!_isExtractionRunning)
            return true;

        ShowDropStatusMessage("Extraction already running", "Please wait for the current extraction to complete.",
            TimeSpan.FromSeconds(4));
        return false;
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
                    TimeSpan.FromSeconds(3));
            }
            else if (result.AlreadyQueuedCount > 0)
            {
                ShowDropStatusMessage(
                    "Already queued",
                    fileName,
                    TimeSpan.FromSeconds(3));
            }
            else
            {
                ShowDropStatusMessage(
                    "Nothing queued",
                    "The selected package could not be added.",
                    TimeSpan.FromSeconds(3));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to queue package from search: {ex}");
            ShowDropStatusMessage(
                "Failed to queue package",
                ex.Message,
                TimeSpan.FromSeconds(3));
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
            if (!IsUnityPackage(candidate))
                continue;

            detectedUnityPackage = true;

            if (!Path.IsPathRooted(candidate))
                continue;

            try
            {
                var normalizedPath = Path.GetFullPath(candidate);
                if (!File.Exists(normalizedPath))
                    continue;

                if (uniquePaths.Add(normalizedPath))
                    validPaths.Add(normalizedPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to resolve dropped path '{candidate}': {ex}");
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

                if (item is IStorageFile file && !string.IsNullOrWhiteSpace(file.Name))
                    yield return file.Name;

                var localPath = item.TryGetLocalPath();
                if (!string.IsNullOrWhiteSpace(localPath))
                    yield return localPath;
            }

        if (!dataObject.Contains(DataFormats.FileNames))
            yield break;

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

    private static bool IsUnityPackage(string? pathOrName)
    {
        if (string.IsNullOrWhiteSpace(pathOrName))
            return false;

        var extension = Path.GetExtension(pathOrName);
        return string.Equals(extension, UnityPackageExtension, StringComparison.OrdinalIgnoreCase);
    }

    private QueueResult QueueUnityPackages(IReadOnlyList<string> packagePaths)
    {
        if (packagePaths.Count == 0)
            return new QueueResult(0, 0);

        var addedCount = 0;
        var alreadyQueuedCount = 0;
        var newlyAddedPackages = new List<(UnityPackageFile Package, string NormalizedPath)>();

        var existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in _settings.UnitypackageFiles)
        {
            if (string.IsNullOrWhiteSpace(package.FilePath))
                continue;

            try
            {
                existingPaths.Add(Path.GetFullPath(package.FilePath));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to normalize queued package path '{package.FilePath}': {ex}");
            }
        }

        var historySet = _settings.History is { Count: > 0 }
            ? new HashSet<string>(_settings.History, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in packagePaths)
        {
            string normalizedPath;
            try
            {
                normalizedPath = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to normalize dropped path '{path}': {ex}");
                continue;
            }

            if (!File.Exists(normalizedPath))
                continue;

            if (!existingPaths.Add(normalizedPath))
            {
                alreadyQueuedCount++;
                continue;
            }

            var fileInfo = new FileInfo(normalizedPath);
            var queueEntry = new UnityPackageFile
            {
                FileName = fileInfo.Name,
                FilePath = normalizedPath,
                FileExtension = fileInfo.Extension,
                FileSize = fileInfo.Exists
                    ? fileInfo.Length.ToString(CultureInfo.InvariantCulture)
                    : string.Empty,
                FileDate = fileInfo.Exists
                    ? fileInfo.LastWriteTimeUtc.ToString("u", CultureInfo.InvariantCulture)
                    : string.Empty,
                IsInQueue = true,
                IsExtracting = false
            };

            _settings.UnitypackageFiles.Add(queueEntry);
            newlyAddedPackages.Add((queueEntry, normalizedPath));

            if (historySet.Add(normalizedPath))
                _settings.History.Add(normalizedPath);

            addedCount++;
        }

        if (newlyAddedPackages.Count > 0)
            foreach (var (package, normalizedPath) in newlyAddedPackages)
                AddOrUpdateQueueDisplayItem(package, normalizedPath);

        UpdateQueueVisualState();

        if (addedCount > 0)
            AppSettingsService.Save(_settings);

        return new QueueResult(addedCount, alreadyQueuedCount);
    }

    private void ClearQueue()
    {
        var modified = false;

        if (_settings.UnitypackageFiles is { Count: > 0 })
        {
            _settings.UnitypackageFiles.Clear();
            modified = true;
        }

        if (_queueItems.Count > 0)
        {
            _queueItems.Clear();
            modified = true;
        }

        if (_queueItemsByPath.Count > 0)
            _queueItemsByPath.Clear();

        UpdateQueueVisualState();

        if (!modified)
            return;

        ShowDropStatusMessage(
            "Queue cleared",
            "Drop or search for .unitypackage files to add new items.",
            TimeSpan.FromSeconds(3));
        AppSettingsService.Save(_settings);
    }

    private void ReloadQueueFromSettings()
    {
        _queueItems.Clear();
        _queueItemsByPath.Clear();

        var queuedPackages = _settings.UnitypackageFiles;
        if (queuedPackages is not null)
            foreach (var package in queuedPackages)
            {
                if (package is null || !package.IsInQueue)
                    continue;

                AddOrUpdateQueueDisplayItem(package);
            }

        UpdateQueueVisualState();
    }

    private void AddOrUpdateQueueDisplayItem(UnityPackageFile package, string? normalizedPath = null)
    {
        if (package is null)
            return;

        var key = !string.IsNullOrWhiteSpace(normalizedPath)
            ? normalizedPath!
            : TryNormalizeFilePath(package.FilePath);

        if (string.IsNullOrWhiteSpace(key))
            return;

        if (_queueItemsByPath.TryGetValue(key, out var existing))
        {
            existing.UpdateFrom(package);
            return;
        }

        var display = new QueueItemDisplay(package, key);
        _queueItems.Add(display);
        _queueItemsByPath[key] = display;
    }

    private void RemoveQueueDisplayItem(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        if (_queueItemsByPath.TryGetValue(key, out var display))
        {
            _queueItems.Remove(display);
            _queueItemsByPath.Remove(key);
        }
    }

    private void UpdateQueueVisualState()
    {
        var itemCount = _queueItems.Count;

        if (_queueSummaryTextBlock is not null)
            _queueSummaryTextBlock.Text = itemCount switch
            {
                0 => "Queue is empty",
                1 => "1 package queued",
                _ => $"{itemCount} packages queued"
            };

        if (_queueEmptyState is not null)
            _queueEmptyState.IsVisible = itemCount == 0;

        if (_queueItemsScrollViewer is not null)
            _queueItemsScrollViewer.IsVisible = itemCount > 0;

        UpdateExtractionButtonsState();

        if (_isExtractionRunning)
            return;

        var nextPackage = itemCount > 0 ? GetNextQueuedPackageName() : null;
        QueueDiscordPresenceUpdate(
            itemCount > 0 ? "Queue ready" : "Dashboard",
            nextPackage: nextPackage,
            queueCountOverride: itemCount);
    }

    private void UpdateExtractionButtonsState()
    {
        var hasQueueItems = _queueItems.Count > 0;

        if (_startExtractionButton is not null)
            _startExtractionButton.IsEnabled = !_isExtractionRunning;

        if (_batchExtractionButton is not null)
            _batchExtractionButton.IsEnabled = !_isExtractionRunning;

        if (_processQueueButton is not null)
            _processQueueButton.IsEnabled = !_isExtractionRunning && hasQueueItems;

        if (_clearQueueButton is not null)
            _clearQueueButton.IsEnabled = hasQueueItems && !_isExtractionRunning;

        if (_cancelExtractionButton is not null)
        {
            _cancelExtractionButton.IsVisible = _isExtractionRunning;
            _cancelExtractionButton.IsEnabled = _isExtractionRunning && !_isExtractionCancelling;
        }
    }

    private void PrepareExtractionDashboard(IReadOnlyList<ExtractionItem> items)
    {
        if (_extractionDashboard is null)
            return;

        _dropZoneVisibilityReset?.Dispose();
        _dropZoneVisibilityReset = null;
        SetDropZoneSectionVisibility(false);

        _extractionDashboardHideReset?.Dispose();
        _extractionDashboardHideReset = null;

        _extractionDashboard.IsVisible = true;
        _extractionDashboard.Opacity = 1;

        UpdateExtractionDashboardSubtitle("Preparing package...");
        UpdateExtractionDashboardPackageText(items.Count > 0 ? Path.GetFileName(items[0].Path) : "—");
        UpdateExtractionDashboardAsset("Waiting...");
        UpdateExtractionDashboardOutput("—");
        UpdateExtractionDashboardAssetCount(0);
        UpdateExtractionDashboardElapsedText("0s");
        UpdateExtractionDashboardNextPackageText(ResolveNextPackageName(items, -1));
        UpdateExtractionDashboardQueueBadge(Math.Max(0, items.Count - 1));

        if (_extractionDashboardProgressBar is not null)
        {
            _extractionDashboardProgressBar.IsIndeterminate = true;
            _extractionDashboardProgressBar.Value = 0;
            _extractionDashboardProgressBar.Maximum = 1;
        }

        _extractionStopwatch?.Stop();
        _extractionStopwatch = null;
        _extractionElapsedTimer.Stop();
    }

    private void BeginExtractionDashboardForPackage(
        string packagePath,
        string outputDirectory,
        IReadOnlyList<ExtractionItem> items,
        int currentIndex)
    {
        var fileName = Path.GetFileName(packagePath);
        var nextPackage = ResolveNextPackageName(items, currentIndex);
        var remaining = Math.Max(0, items.Count - currentIndex - 1);

        OnExtractionOverviewPackageStarted();
        UpdateExtractionDashboardSubtitle("Extracting assets...");
        UpdateExtractionDashboardPackageText(fileName);
        UpdateExtractionDashboardAsset("Starting...");
        UpdateExtractionDashboardOutput(outputDirectory);
        UpdateExtractionDashboardAssetCount(0);
        UpdateExtractionDashboardElapsedText("0s");
        UpdateExtractionDashboardNextPackageText(nextPackage);
        UpdateExtractionDashboardQueueBadge(remaining);
        QueueDiscordPresenceUpdate(
            $"Extracting {fileName}",
            currentPackage: fileName,
            nextPackage: nextPackage,
            extractionActive: true,
            queueCountOverride: remaining);

        if (_extractionDashboardProgressBar is not null)
        {
            _extractionDashboardProgressBar.IsIndeterminate = true;
            _extractionDashboardProgressBar.Value = 0;
            _extractionDashboardProgressBar.Maximum = 1;
        }

        _extractionStopwatch?.Stop();
        _extractionStopwatch = Stopwatch.StartNew();
        _extractionElapsedTimer.Stop();
        _extractionElapsedTimer.Start();
        OnExtractionElapsedTick(this, EventArgs.Empty);
    }

    private void UpdateExtractionDashboardProgress(string? assetPath, int assetsExtracted)
    {
        UpdateExtractionDashboardAssetCount(Math.Max(0, assetsExtracted));
        UpdateExtractionOverviewProgress(assetsExtracted);
        if (!string.IsNullOrWhiteSpace(assetPath))
            UpdateExtractionDashboardAsset(assetPath);
    }

    private void CompleteCurrentPackageOnDashboard(
        string packagePath,
        bool success,
        int assetsExtracted,
        bool isCancelled = false,
        int remaining = 0,
        string? nextPackage = null)
    {
        var fileName = Path.GetFileName(packagePath);

        _extractionStopwatch?.Stop();
        _extractionElapsedTimer.Stop();

        var statusText = success
            ? $"Completed {fileName}"
            : isCancelled
                ? $"Cancelled {fileName}"
                : $"Failed {fileName}";
        UpdateExtractionDashboardSubtitle(statusText);

        if (_extractionDashboardAssetText is not null)
            _extractionDashboardAssetText.Text = success
                ? "All assets extracted."
                : isCancelled
                    ? "Extraction cancelled."
                    : "Extraction failed.";

        UpdateExtractionDashboardAssetCount(Math.Max(0, assetsExtracted));
        UpdateExtractionDashboardQueueBadge(Math.Max(0, remaining));
        UpdateExtractionDashboardNextPackageText(nextPackage);

        if (_extractionDashboardProgressBar is not null)
        {
            _extractionDashboardProgressBar.IsIndeterminate = false;
            _extractionDashboardProgressBar.Maximum = 1;
            _extractionDashboardProgressBar.Value = success ? 1 : 0;
        }

        var detailOverride = success
            ? remaining > 0
                ? $"Moving to next package - {remaining} remaining"
                : "All assets extracted"
            : isCancelled
                ? "Extraction cancelled by user"
                : "Extraction failed - Check notifications";

        QueueDiscordPresenceUpdate(
            statusText,
            detailOverride,
            success ? null : fileName,
            nextPackage,
            remaining > 0,
            remaining);
    }

    private void FinishExtractionDashboard(TimeSpan delay)
    {
        _extractionStopwatch?.Stop();
        _extractionElapsedTimer.Stop();

        _dropZoneVisibilityReset?.Dispose();
        _dropZoneVisibilityReset = null;

        if (_extractionDashboard is null)
        {
            SetDropZoneSectionVisibility(true);
            return;
        }

        _extractionDashboardHideReset?.Dispose();
        _extractionDashboardHideReset = DispatcherTimer.RunOnce(() =>
        {
            if (_extractionDashboard is null)
            {
                SetDropZoneSectionVisibility(true);
                return;
            }

            _extractionDashboard.Opacity = 0;
            _dropZoneVisibilityReset?.Dispose();
            _dropZoneVisibilityReset = DispatcherTimer.RunOnce(() =>
            {
                if (_extractionDashboard is not null)
                    _extractionDashboard.IsVisible = false;

                SetDropZoneSectionVisibility(true);

                _dropZoneVisibilityReset?.Dispose();
                _dropZoneVisibilityReset = null;

                _extractionDashboardHideReset?.Dispose();
                _extractionDashboardHideReset = null;
            }, TimeSpan.FromMilliseconds(280));
        }, delay);
    }

    private void UpdateExtractionDashboardSubtitle(string text)
    {
        if (_extractionDashboardSubtitle is not null)
            _extractionDashboardSubtitle.Text = text;
    }

    private void UpdateExtractionDashboardQueueBadge(int pendingCount)
    {
        if (_extractionDashboardQueueCount is null)
            return;

        _extractionDashboardQueueCount.Text = pendingCount > 0
            ? $"Queue: {pendingCount}"
            : "Queue clear";
    }

    private void UpdateExtractionDashboardPackageText(string text)
    {
        if (_extractionDashboardPackageText is not null)
            _extractionDashboardPackageText.Text = text;
    }

    private void UpdateExtractionDashboardAsset(string text)
    {
        if (_extractionDashboardAssetText is not null)
            _extractionDashboardAssetText.Text = text;
    }

    private void UpdateExtractionDashboardOutput(string text)
    {
        if (_extractionDashboardOutputText is null)
            return;

        _extractionDashboardOutputText.Text = string.IsNullOrWhiteSpace(text)
            ? "—"
            : text;
    }

    private void UpdateExtractionDashboardAssetCount(int value)
    {
        if (_extractionDashboardAssetCount is not null)
            _extractionDashboardAssetCount.Text = value.ToString("N0", CultureInfo.CurrentCulture);
    }

    private void UpdateExtractionDashboardElapsedText(string text)
    {
        if (_extractionDashboardElapsed is not null)
            _extractionDashboardElapsed.Text = text;
    }

    private void UpdateExtractionDashboardNextPackageText(string? nextPackage)
    {
        if (_extractionDashboardNextPackage is null)
            return;

        _extractionDashboardNextPackage.Text = string.IsNullOrWhiteSpace(nextPackage)
            ? "All caught up"
            : nextPackage!;
    }

    private string ResolveNextPackageName(IReadOnlyList<ExtractionItem> items, int currentIndex)
    {
        var nextIndex = currentIndex + 1;
        if (nextIndex >= 0 && nextIndex < items.Count)
            return Path.GetFileName(items[nextIndex].Path);

        var nextQueued = _queueItems.FirstOrDefault(display => !display.IsExtracting);
        return nextQueued?.FileName ?? "All caught up";
    }

    private void OnExtractionElapsedTick(object? sender, EventArgs e)
    {
        if (_extractionStopwatch is null || !_extractionStopwatch.IsRunning)
            return;

        UpdateExtractionDashboardElapsedText(FormatElapsed(_extractionStopwatch.Elapsed));
    }

    private void BeginExtractionOverviewSession()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(BeginExtractionOverviewSession);
            return;
        }

        _extractionOverviewPackageBaseline = Math.Max(0, _settings.TotalExtracted);
        _extractionOverviewAssetBaseline = Math.Max(0, _settings.TotalFilesExtracted);
        _extractionOverviewSessionPackages = 0;
        _extractionOverviewSessionAssets = 0;
        _extractionOverviewCurrentPackageAssets = 0;
        _extractionOverviewStartTime = DateTimeOffset.Now;
        _isExtractionOverviewLive = true;
        UpdateExtractionOverviewStatusInProgress();
        UpdateExtractionOverviewLiveCounts();
    }

    private void OnExtractionOverviewPackageStarted()
    {
        if (!_isExtractionOverviewLive)
            return;

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(OnExtractionOverviewPackageStarted);
            return;
        }

        _extractionOverviewCurrentPackageAssets = 0;
        UpdateExtractionOverviewLiveCounts();
    }

    private void UpdateExtractionOverviewProgress(int assetsExtracted)
    {
        if (!_isExtractionOverviewLive)
            return;

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => UpdateExtractionOverviewProgress(assetsExtracted));
            return;
        }

        var sanitized = Math.Max(0, assetsExtracted);
        if (sanitized <= _extractionOverviewCurrentPackageAssets)
            return;

        var delta = sanitized - _extractionOverviewCurrentPackageAssets;
        _extractionOverviewCurrentPackageAssets = sanitized;
        _extractionOverviewSessionAssets += delta;
        UpdateExtractionOverviewLiveCounts();
    }

    private void OnExtractionOverviewPackageCompleted(int assetsExtracted)
    {
        if (!_isExtractionOverviewLive)
            return;

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnExtractionOverviewPackageCompleted(assetsExtracted));
            return;
        }

        var sanitized = Math.Max(0, assetsExtracted);
        if (sanitized > _extractionOverviewCurrentPackageAssets)
        {
            _extractionOverviewSessionAssets += sanitized - _extractionOverviewCurrentPackageAssets;
            _extractionOverviewCurrentPackageAssets = sanitized;
        }

        _extractionOverviewSessionPackages++;
        UpdateExtractionOverviewLiveCounts();
    }

    private void UpdateExtractionOverviewLiveCounts()
    {
        if (!_isExtractionOverviewLive)
            return;

        var packages = _extractionOverviewPackageBaseline + _extractionOverviewSessionPackages;
        var assets = _extractionOverviewAssetBaseline + _extractionOverviewSessionAssets;
        UpdateExtractionOverviewCounts(packages, assets);
    }

    private void UpdateExtractionOverviewCounts(int packages, int assets)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => UpdateExtractionOverviewCounts(packages, assets));
            return;
        }

        if (_extractionOverviewPackagesCompletedText is not null)
            _extractionOverviewPackagesCompletedText.Text = packages.ToString("N0", CultureInfo.CurrentCulture);

        if (_extractionOverviewAssetsExtractedText is not null)
            _extractionOverviewAssetsExtractedText.Text = assets.ToString("N0", CultureInfo.CurrentCulture);
    }

    private void UpdateExtractionOverviewStatus(string text)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => UpdateExtractionOverviewStatus(text));
            return;
        }

        if (_extractionOverviewLastExtractionText is not null)
            _extractionOverviewLastExtractionText.Text = text;
    }

    private void UpdateExtractionOverviewStatusInProgress()
    {
        var statusText = _extractionOverviewStartTime.HasValue
            ? $"Started at {_extractionOverviewStartTime.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)}"
            : "In progress�";

        UpdateExtractionOverviewStatus(statusText);
    }

    private void UpdateExtractionOverviewDisplay()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(UpdateExtractionOverviewDisplay);
            return;
        }

        var packages = Math.Max(0, _settings.TotalExtracted);
        var assets = Math.Max(0, _settings.TotalFilesExtracted);
        UpdateExtractionOverviewCounts(packages, assets);

        if (_extractionOverviewLastExtractionText is null)
            return;

        _extractionOverviewLastExtractionText.Text = _settings.LastExtractionTime.HasValue
            ? _settings.LastExtractionTime.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
            : "Not started";
    }

    private void EndExtractionOverviewSession()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(EndExtractionOverviewSession);
            return;
        }

        _isExtractionOverviewLive = false;
        _extractionOverviewSessionPackages = 0;
        _extractionOverviewSessionAssets = 0;
        _extractionOverviewCurrentPackageAssets = 0;
        _extractionOverviewStartTime = null;
        UpdateExtractionOverviewDisplay();
    }

    private static string? TryResolveLocalPath(IStorageFile storageFile)
    {
        if (storageFile is null)
            return null;

        return storageFile.Path?.LocalPath;
    }

    private static string TryNormalizeFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
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

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.FromSeconds(1))
            return $"{elapsed.TotalMilliseconds:0} ms";

        if (elapsed < TimeSpan.FromMinutes(1))
            return $"{elapsed.TotalSeconds:0}s";

        if (elapsed < TimeSpan.FromHours(1))
            return $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds:D2}s";

        return $"{(int)elapsed.TotalHours}h {elapsed.Minutes:D2}m";
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

    private void ShowDropStatusMessage(string primary, string? secondary, TimeSpan? resetAfter = null)
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
    }

    private void ResetDropStatusMessage()
    {
        if (_dropZonePrimaryTextBlock is not null)
            _dropZonePrimaryTextBlock.Text = _defaultDropPrimaryText;

        if (_dropZoneSecondaryTextBlock is not null)
            _dropZoneSecondaryTextBlock.Text = _defaultDropSecondaryText;

        _dropStatusReset = null;
    }

    private async void CheckUpdatesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_isCheckingForUpdates || _isUpdateDownloadInProgress)
            return;

        if (_isExtractionRunning)
        {
            ShowDropStatusMessage(
                "Extraction in progress",
                "Finish the current extraction before installing an update.",
                TimeSpan.FromSeconds(6));
            return;
        }

        _isCheckingForUpdates = true;

        var button = _checkUpdatesButton ?? sender as Button;
        if (button is not null)
        {
            _checkUpdatesButtonOriginalContent ??= button.Content;
            button.IsEnabled = false;
            button.Content = "Checking...";
        }

        await Dispatcher.UIThread.InvokeAsync(() => SetVersionStatusMessage("Checking for updates..."));

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        try
        {
            var currentVersion = ResolveCurrentVersionForUpdates();
            var settings = _settings.Update ?? new UpdateSettings();
            var result = await _updateService
                .CheckForUpdatesAsync(settings, currentVersion, cts.Token)
                .ConfigureAwait(false);

            if (!result.IsUpdateAvailable || result.Manifest is null)
            {
                var message = string.IsNullOrWhiteSpace(result.Message) ? "You're up to date" : result.Message!;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SetVersionStatusMessage(message, TimeSpan.FromSeconds(6));
                    ShowDropStatusMessage("No updates available", message, TimeSpan.FromSeconds(6));
                });
                return;
            }

            await HandleUpdateAvailabilityAsync(result.Manifest, false, cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SetVersionStatusMessage("Update check cancelled", TimeSpan.FromSeconds(6));
                ShowDropStatusMessage("Update check cancelled", "Try again in a moment.", TimeSpan.FromSeconds(6));
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SetVersionStatusMessage("Update check failed", TimeSpan.FromSeconds(6));
                ShowDropStatusMessage("Update check failed", ex.Message, TimeSpan.FromSeconds(8));
            });
        }
        finally
        {
            if (button is not null)
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_checkUpdatesButtonOriginalContent is not null)
                        button.Content = _checkUpdatesButtonOriginalContent;
                    button.IsEnabled = true;
                });

            _isCheckingForUpdates = false;
        }
    }

    private Version? ResolveCurrentVersionForUpdates()
    {
        var currentVersionText = GetCurrentVersionForComparison();
        return TryParseVersion(currentVersionText, out var version) ? version : null;
    }

    private async Task HandleUpdateAvailabilityAsync(
        UpdateManifest manifest,
        bool isAutomatic,
        CancellationToken cancellationToken)
    {
        if (_isUpdateDownloadInProgress)
        {
            _pendingUpdateManifest = manifest;
            return;
        }

        if (_isExtractionRunning)
        {
            _pendingUpdateManifest = manifest;
            if (!isAutomatic)
                await Dispatcher.UIThread.InvokeAsync(() =>
                    ShowDropStatusMessage(
                        "Extraction still running",
                        "Update will start automatically when extraction finishes.",
                        TimeSpan.FromSeconds(6)));

            return;
        }

        _isUpdateDownloadInProgress = true;
        _activeUpdateManifest = manifest;
        _pendingUpdateManifest = null;

        var progress = new Progress<UpdateProgress>(update => ReportUpdateProgress(manifest, update));

        var preparationResult = await _updateService
            .DownloadAndPrepareUpdateAsync(manifest, progress, cancellationToken)
            .ConfigureAwait(false);

        if (!preparationResult.Success || preparationResult.Preparation is null)
        {
            ResetUpdateProgressState();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SetVersionStatusMessage("Update failed", TimeSpan.FromSeconds(6));
                var detail = preparationResult.ErrorMessage ?? "Update could not be prepared.";
                ShowDropStatusMessage("Update failed", detail, TimeSpan.FromSeconds(8));
            });
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var status = isAutomatic
                ? $"Installing update {manifest.Version}"
                : $"Ready to install update {manifest.Version}";
            SetVersionStatusMessage(status, TimeSpan.FromSeconds(6));

            var detail = isAutomatic
                ? "EasyExtract will restart automatically to finish installing."
                : "EasyExtract will restart to finish installing.";
            ShowDropStatusMessage(status, detail, TimeSpan.FromSeconds(6));
        });

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            ResetUpdateProgressState();
            throw;
        }

        var launchSucceeded = _updateService.TryLaunchPreparedUpdate(preparationResult.Preparation);
        if (!launchSucceeded)
        {
            ResetUpdateProgressState();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SetVersionStatusMessage("Update failed", TimeSpan.FromSeconds(6));
                ShowDropStatusMessage("Update failed", "Installer could not be started.", TimeSpan.FromSeconds(8));
            });
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            SetVersionStatusMessage("Restarting to finish update");
            Close();
        });
    }

    private void ReportUpdateProgress(UpdateManifest manifest, UpdateProgress progress)
    {
        var now = DateTime.UtcNow;
        var phaseChanged = _lastUpdatePhase != progress.Phase;
        var percentChanged = progress.Percentage.HasValue &&
                             (!_lastUpdatePercentage.HasValue ||
                              Math.Abs(progress.Percentage.Value - _lastUpdatePercentage.Value) >= 0.01);
        var intervalElapsed = now - _lastUpdateUiRefresh > TimeSpan.FromMilliseconds(500);

        if (!phaseChanged && !percentChanged && !intervalElapsed)
            return;

        _lastUpdatePhase = progress.Phase;
        _lastUpdatePercentage = progress.Percentage;
        _lastUpdateUiRefresh = now;

        var statusText = BuildUpdateStatusText(progress);
        var announcePhase = phaseChanged;

        Dispatcher.UIThread.Post(() =>
        {
            SetVersionStatusMessage(statusText);

            if (!announcePhase)
                return;

            var phaseMessage = progress.Phase switch
            {
                UpdatePhase.Downloading => $"Downloading version {manifest.Version}",
                UpdatePhase.Extracting => $"Extracting version {manifest.Version}",
                UpdatePhase.Preparing => $"Preparing version {manifest.Version}",
                UpdatePhase.WaitingForRestart => $"Ready to install version {manifest.Version}",
                UpdatePhase.Completed => $"Version {manifest.Version} ready",
                _ => $"Updating to version {manifest.Version}"
            };

            ShowDropStatusMessage(phaseMessage, manifest.ReleaseName ?? $"Tag {manifest.TagName}",
                TimeSpan.FromSeconds(4));
        });
    }

    private static string BuildUpdateStatusText(UpdateProgress progress)
    {
        var phaseText = progress.Phase switch
        {
            UpdatePhase.Downloading => "Downloading update",
            UpdatePhase.Extracting => "Extracting update",
            UpdatePhase.Preparing => "Preparing update",
            UpdatePhase.WaitingForRestart => "Update ready",
            UpdatePhase.Completed => "Update ready",
            _ => "Updating"
        };

        if (progress.Percentage is { } percent)
            phaseText = $"{phaseText} {percent.ToString("P0", CultureInfo.CurrentCulture)}";

        return phaseText;
    }

    private void ResetUpdateProgressState()
    {
        _isUpdateDownloadInProgress = false;
        _activeUpdateManifest = null;
        _lastUpdatePhase = null;
        _lastUpdatePercentage = null;
        _lastUpdateUiRefresh = DateTime.MinValue;
    }

    private void QueueAutomaticUpdateCheck()
    {
        if (_settings.Update?.AutoUpdate != true)
            return;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(8)).ConfigureAwait(false);
                await PerformAutomaticUpdateCheckAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Automatic update task failed: {ex}");
            }
        });
    }

    private async Task PerformAutomaticUpdateCheckAsync()
    {
        if (_isCheckingForUpdates || _isUpdateDownloadInProgress)
            return;

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        try
        {
            _isCheckingForUpdates = true;
            var currentVersion = ResolveCurrentVersionForUpdates();
            var settings = _settings.Update ?? new UpdateSettings();

            var result = await _updateService
                .CheckForUpdatesAsync(settings, currentVersion, cts.Token)
                .ConfigureAwait(false);

            if (!result.IsUpdateAvailable || result.Manifest is null)
                return;

            await HandleUpdateAvailabilityAsync(result.Manifest, true, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The check was cancelled or timed out; ignore silently.
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Automatic update check encountered an error: {ex}");
        }
        finally
        {
            _isCheckingForUpdates = false;
        }
    }

    private void TryLaunchPendingUpdateAfterExtraction()
    {
        if (_pendingUpdateManifest is null || _isUpdateDownloadInProgress)
            return;

        var manifest = _pendingUpdateManifest;
        _pendingUpdateManifest = null;

        _ = Task.Run(async () =>
        {
            try
            {
                await HandleUpdateAvailabilityAsync(manifest, true, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to resume pending update: {ex}");
            }
        });
    }

    private void SettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        var window = new SettingsWindow();
        window.SettingsSaved += OnSettingsWindowSaved;
        window.Closed += OnSettingsWindowClosed;

        _settingsWindow = window;
        window.Show(this);
        QueueDiscordPresenceUpdate("Settings", "Tuning preferences");
    }

    private void FeedbackButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_feedbackWindow is { IsVisible: true })
        {
            _feedbackWindow.Activate();
            return;
        }

        var window = new FeedbackWindow(GetCurrentVersionForComparison());
        window.FeedbackSent += OnFeedbackWindowSent;
        window.Closed += OnFeedbackWindowClosed;

        _feedbackWindow = window;
        window.Show(this);
        QueueDiscordPresenceUpdate("Feedback", "Drafting feedback");
    }

    private void OnSettingsWindowSaved(object? sender, AppSettings settings)
    {
        if (sender is not SettingsWindow)
            return;

        _settings = settings;
        ApplySettings(_settings);
        SetVersionText();
    }

    private void OnSettingsWindowClosed(object? sender, EventArgs e)
    {
        if (sender is not SettingsWindow window)
            return;

        window.SettingsSaved -= OnSettingsWindowSaved;
        window.Closed -= OnSettingsWindowClosed;

        if (ReferenceEquals(_settingsWindow, window))
            _settingsWindow = null;

        RestorePresenceAfterOverlay();
    }

    private void OnFeedbackWindowSent(object? sender, EventArgs e)
    {
        ShowDropStatusMessage("Feedback sent", "Thanks for helping us improve EasyExtract.", TimeSpan.FromSeconds(4));
    }

    private void OnFeedbackWindowClosed(object? sender, EventArgs e)
    {
        if (sender is not FeedbackWindow window)
            return;

        window.FeedbackSent -= OnFeedbackWindowSent;
        window.Closed -= OnFeedbackWindowClosed;

        if (ReferenceEquals(_feedbackWindow, window))
            _feedbackWindow = null;

        RestorePresenceAfterOverlay();
    }

    private async Task ShowOverlayAsync(Control view)
    {
        if (_overlayHost is null || _overlayContent is null)
            return;

        if (_activeOverlayContent is not null)
            await CloseOverlayAsync();

        _activeOverlayContent = view;
        _overlayContent.Content = view;
        await RunOverlayAnimationAsync(true);
        QueueDiscordPresenceUpdate(
            ResolveOverlayPresenceState(view),
            ResolveOverlayPresenceDetail(view));
    }

    private async Task CloseOverlayAsync(Control? requestingView = null)
    {
        if (_overlayHost is null || _overlayContent is null)
            return;

        if (_activeOverlayContent is null)
            return;

        if (requestingView is not null && !ReferenceEquals(_activeOverlayContent, requestingView))
            return;

        await RunOverlayAnimationAsync(false);

        DetachOverlayHandlers(_activeOverlayContent);

        _overlayContent.Content = null;
        _activeOverlayContent = null;
        RestorePresenceAfterOverlay();
    }

    private void DetachOverlayHandlers(Control control)
    {
    }

    private async Task RunOverlayAnimationAsync(bool showing)
    {
        if (_overlayHost is null)
            return;

        _overlayAnimationCts?.Cancel();
        _overlayAnimationCts?.Dispose();

        var cts = new CancellationTokenSource();
        _overlayAnimationCts = cts;
        var token = cts.Token;

        var scaleTransform = _overlayCardScaleTransform;
        if (scaleTransform is null && _overlayCard is not null)
        {
            scaleTransform = new ScaleTransform(1, 1);
            _overlayCard.RenderTransform = scaleTransform;
            _overlayCardScaleTransform = scaleTransform;
        }

        const double collapsedScale = 0.94;
        const double hideScale = 0.96;
        var showDuration = TimeSpan.FromMilliseconds(240);
        var hideDuration = TimeSpan.FromMilliseconds(180);

        if (!showing && !_overlayHost.IsVisible)
        {
            _overlayHost.IsHitTestVisible = false;
            _overlayHost.Opacity = 0;
            if (scaleTransform is not null)
            {
                scaleTransform.ScaleX = collapsedScale;
                scaleTransform.ScaleY = collapsedScale;
            }

            cts.Dispose();
            if (ReferenceEquals(_overlayAnimationCts, cts))
                _overlayAnimationCts = null;
            return;
        }

        try
        {
            if (showing)
            {
                _overlayHost.IsVisible = true;
                _overlayHost.IsHitTestVisible = true;
                _overlayHost.Opacity = 0;
                if (scaleTransform is not null)
                {
                    scaleTransform.ScaleX = collapsedScale;
                    scaleTransform.ScaleY = collapsedScale;
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _overlayHost.Opacity = 1;
                    if (scaleTransform is not null)
                    {
                        scaleTransform.ScaleX = 1;
                        scaleTransform.ScaleY = 1;
                    }
                }, DispatcherPriority.Render);

                await Task.Delay(showDuration, token);
                if (token.IsCancellationRequested)
                    return;
            }
            else
            {
                _overlayHost.IsHitTestVisible = false;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _overlayHost.Opacity = 0;
                    if (scaleTransform is not null)
                    {
                        scaleTransform.ScaleX = hideScale;
                        scaleTransform.ScaleY = hideScale;
                    }
                }, DispatcherPriority.Render);

                await Task.Delay(hideDuration, token);
                if (token.IsCancellationRequested)
                    return;

                _overlayHost.IsVisible = false;
                _overlayHost.Opacity = 0;
                if (scaleTransform is not null)
                {
                    scaleTransform.ScaleX = collapsedScale;
                    scaleTransform.ScaleY = collapsedScale;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellations triggered by a new animation request.
        }
        finally
        {
            if (ReferenceEquals(_overlayAnimationCts, cts))
            {
                _overlayAnimationCts.Dispose();
                _overlayAnimationCts = null;
            }
        }
    }

    private void LoadSettings()
    {
        _settings = AppSettingsService.Load();
        ApplyWindowPlacement(_settings);
        ApplySettings(_settings);
    }

    private void ApplyWindowPlacement(AppSettings settings)
    {
        var savedWidth = settings.WindowWidth;
        var savedHeight = settings.WindowHeight;

        if (savedWidth.HasValue && savedWidth.Value > 0 && !double.IsNaN(savedWidth.Value))
            Width = savedWidth.Value;

        if (savedHeight.HasValue && savedHeight.Value > 0 && !double.IsNaN(savedHeight.Value))
            Height = savedHeight.Value;

        if (savedWidth.HasValue && savedHeight.HasValue &&
            savedWidth.Value > 0 && savedHeight.Value > 0 &&
            !double.IsNaN(savedWidth.Value) && !double.IsNaN(savedHeight.Value))
            _lastNormalSize = new Size(savedWidth.Value, savedHeight.Value);

        if (settings.WindowPositionX.HasValue && settings.WindowPositionY.HasValue)
        {
            var requestedPosition = new PixelPoint(settings.WindowPositionX.Value, settings.WindowPositionY.Value);
            var adjustedPosition = EnsureWindowIsVisible(requestedPosition, new Size(Width, Height));
            Position = adjustedPosition;
            _lastNormalPosition = adjustedPosition;
        }

        var restoredState = settings.WindowState == WindowState.Minimized
            ? WindowState.Normal
            : settings.WindowState;

        WindowState = Enum.IsDefined(typeof(WindowState), restoredState)
            ? restoredState
            : WindowState.Normal;

        CaptureCurrentBoundsIfNormal();
    }

    private PixelPoint EnsureWindowIsVisible(PixelPoint desiredPosition, Size windowSize)
    {
        if (Screens is null)
            return desiredPosition;

        var pixelWidth = Math.Max(1, (int)Math.Round(windowSize.Width));
        var pixelHeight = Math.Max(1, (int)Math.Round(windowSize.Height));
        var targetScreen = Screens.ScreenFromPoint(desiredPosition) ?? Screens.Primary;

        if (targetScreen is null)
            return desiredPosition;

        var workingArea = targetScreen.WorkingArea;
        var left = workingArea.X;
        var top = workingArea.Y;
        var maxX = left + workingArea.Width - pixelWidth;
        var maxY = top + workingArea.Height - pixelHeight;
        var clampedX = Math.Clamp(desiredPosition.X, left, Math.Max(left, maxX));
        var clampedY = Math.Clamp(desiredPosition.Y, top, Math.Max(top, maxY));

        return new PixelPoint(clampedX, clampedY);
    }

    private void CaptureCurrentBoundsIfNormal()
    {
        if (WindowState != WindowState.Normal)
            return;

        _lastNormalSize = Bounds.Size;
        _lastNormalPosition = Position;
    }

    private void OnMainWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty)
        {
            CaptureCurrentBoundsIfNormal();
            return;
        }

        if (e.Property == BoundsProperty)
            CaptureCurrentBoundsIfNormal();
    }

    private void OnMainWindowPositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (WindowState == WindowState.Normal)
            _lastNormalPosition = e.Point;
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        SaveWindowPlacement();
        if (SearchViewModel is not null)
            SearchViewModel.PropertyChanged -= OnSearchViewModelPropertyChanged;

        _extractionElapsedTimer.Stop();
        _extractionElapsedTimer.Tick -= OnExtractionElapsedTick;
        AppSettingsService.Save(_settings);
    }

    private void SaveWindowPlacement()
    {
        var sizeToPersist = WindowState == WindowState.Normal
            ? Bounds.Size
            : _lastNormalSize ?? Bounds.Size;
        var positionToPersist = WindowState == WindowState.Normal
            ? Position
            : _lastNormalPosition ?? Position;

        _settings.WindowWidth = sizeToPersist.Width;
        _settings.WindowHeight = sizeToPersist.Height;
        _settings.WindowPositionX = positionToPersist.X;
        _settings.WindowPositionY = positionToPersist.Y;
        _settings.WindowState = WindowState == WindowState.Minimized ? WindowState.Normal : WindowState;
    }

    private void ApplySettings(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.AppTitle))
            Title = settings.AppTitle;

        UpdateLicenseTierDisplay(settings);
        ReloadQueueFromSettings();
        ApplyTheme(settings.ApplicationTheme);
        _ = ApplyCustomBackgroundAsync(settings);
        UpdateExtractionOverviewDisplay();
        QueueDiscordPresenceUpdate("Dashboard", settingsOverride: settings);
        ContextMenuIntegrationService.UpdateContextMenuIntegration(settings.ContextMenuToggle);
    }

    private void InitializeStartupExtractionTargets()
    {
        if (_startupArguments.Length == 0)
            return;

        var targets = ResolveStartupExtractionTargets(_startupArguments);
        if (targets.Count == 0)
            return;

        _pendingStartupExtractions = targets;
    }

    private async void OnMainWindowOpened(object? sender, EventArgs e)
    {
        Opened -= OnMainWindowOpened;

        if (_pendingStartupExtractions is null || _pendingStartupExtractions.Count == 0)
            return;

        var targets = _pendingStartupExtractions;
        _pendingStartupExtractions = null;

        try
        {
            QueueUnityPackages(targets);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to queue startup packages: {ex}");
            return;
        }

        var extractionItems = BuildExtractionItems(targets);
        if (extractionItems.Count == 0)
            return;

        try
        {
            await RunExtractionSequenceAsync(extractionItems);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Startup extraction failed: {ex}");
        }
    }

    private List<ExtractionItem> BuildExtractionItems(IEnumerable<string> targets)
    {
        var items = new List<ExtractionItem>();

        foreach (var target in targets)
        {
            var normalized = TryNormalizeFilePath(target);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            if (!File.Exists(normalized))
                continue;

            if (!IsUnityPackage(normalized))
                continue;

            var queueEntry = FindQueueEntryByPath(normalized);
            items.Add(new ExtractionItem(normalized, queueEntry));
        }

        return items;
    }

    private UnityPackageFile? FindQueueEntryByPath(string normalizedPath)
    {
        if (_settings.UnitypackageFiles is not { Count: > 0 })
            return null;

        foreach (var entry in _settings.UnitypackageFiles)
        {
            if (entry is null)
                continue;

            var entryPath = TryNormalizeFilePath(entry.FilePath);
            if (string.IsNullOrWhiteSpace(entryPath))
                continue;

            if (string.Equals(entryPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        return null;
    }

    private static List<string> ResolveStartupExtractionTargets(IReadOnlyList<string> arguments)
    {
        var results = new List<string>();
        var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var capturePaths = false;

        foreach (var argument in arguments)
        {
            if (string.IsNullOrWhiteSpace(argument))
                continue;

            if (IsExtractFlag(argument))
            {
                capturePaths = true;
                continue;
            }

            if (!capturePaths && !IsUnityPackage(argument))
                continue;

            var normalized = TryNormalizeFilePath(argument);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            if (!File.Exists(normalized))
                continue;

            if (!IsUnityPackage(normalized))
                continue;

            if (uniquePaths.Add(normalized))
                results.Add(normalized);
        }

        return results;
    }

    private static bool IsExtractFlag(string argument)
    {
        return argument.Equals("--extract", StringComparison.OrdinalIgnoreCase) ||
               argument.Equals("-extract", StringComparison.OrdinalIgnoreCase) ||
               argument.Equals("/extract", StringComparison.OrdinalIgnoreCase);
    }

    private void QueueDiscordPresenceUpdate(
        string state,
        string? detailsOverride = null,
        string? currentPackage = null,
        string? nextPackage = null,
        bool extractionActive = false,
        int? queueCountOverride = null,
        AppSettings? settingsOverride = null)
    {
        var targetSettings = settingsOverride ?? _settings;
        if (targetSettings is null)
            return;

        if (!targetSettings.DiscordRpc)
        {
            if (_lastDiscordPresenceEnabled)
            {
                _ = DiscordRpcService.Instance.UpdatePresenceAsync(targetSettings, DiscordPresenceContext.Disabled());
                _lastDiscordPresenceEnabled = false;
            }

            return;
        }

        _lastDiscordPresenceEnabled = true;

        var queueCount = Math.Max(0, queueCountOverride ?? GetActiveQueueCount());
        var normalizedState = string.IsNullOrWhiteSpace(state) ? "Dashboard" : state.Trim();
        var normalizedCurrentPackage = NormalizePackageLabel(currentPackage);
        var normalizedNextPackage = NormalizePackageLabel(nextPackage);
        var details = string.IsNullOrWhiteSpace(detailsOverride)
            ? BuildPresenceDetails(normalizedState, queueCount, extractionActive, normalizedCurrentPackage,
                normalizedNextPackage)
            : detailsOverride.Trim();

        var context = new DiscordPresenceContext(
            normalizedState,
            details,
            normalizedCurrentPackage,
            normalizedNextPackage,
            queueCount,
            extractionActive);

        _ = DiscordRpcService.Instance.UpdatePresenceAsync(targetSettings, context);
    }

    private static string ResolveOverlayPresenceState(Control view)
    {
        return view.GetType().Name;
    }

    private static string ResolveOverlayPresenceDetail(Control view)
    {
        return "Exploring EasyExtract";
    }

    private int GetActiveQueueCount()
    {
        if (_queueItems.Count == 0)
            return 0;

        var queued = _queueItems.Count(display => !display.IsExtracting);
        return Math.Max(0, queued);
    }

    private string? GetNextQueuedPackageName()
    {
        var next = _queueItems.FirstOrDefault(display => !display.IsExtracting);
        if (next is not null)
            return next.FileName;

        return _queueItems.FirstOrDefault()?.FileName;
    }

    private static string BuildPresenceDetails(
        string state,
        int queueCount,
        bool extractionActive,
        string? currentPackage,
        string? nextPackage)
    {
        if (extractionActive)
        {
            if (!string.IsNullOrWhiteSpace(currentPackage))
                return queueCount > 0
                    ? $"Extracting {currentPackage} - {queueCount} remaining"
                    : $"Finishing {currentPackage}";

            return queueCount > 0
                ? $"Processing extraction queue - {queueCount} remaining"
                : "Processing extraction queue";
        }

        if (string.Equals(state, "Settings", StringComparison.OrdinalIgnoreCase))
            return "Tuning EasyExtract preferences";

        if (string.Equals(state, "Feedback", StringComparison.OrdinalIgnoreCase))
            return "Sharing feedback with the team";

        if (queueCount > 0)
        {
            if (!string.IsNullOrWhiteSpace(nextPackage) &&
                !string.Equals(nextPackage, "All caught up", StringComparison.OrdinalIgnoreCase))
                return $"Queue ready - Next: {nextPackage}";

            return queueCount == 1
                ? "1 package queued"
                : $"{queueCount} packages queued";
        }

        return string.Equals(state, "Dashboard", StringComparison.OrdinalIgnoreCase)
            ? "Ready to extract Unitypackages"
            : "Exploring EasyExtract";
    }

    private void RestorePresenceAfterOverlay()
    {
        if (_isExtractionRunning)
        {
            var currentPackage = NormalizePackageLabel(_extractionDashboardPackageText?.Text);
            var nextPackage = NormalizePackageLabel(_extractionDashboardNextPackage?.Text);
            var remaining = Math.Max(0, GetActiveQueueCount());

            QueueDiscordPresenceUpdate(
                string.IsNullOrWhiteSpace(currentPackage) ? "Extraction in progress" : $"Extracting {currentPackage}",
                currentPackage: currentPackage,
                nextPackage: nextPackage,
                extractionActive: true,
                queueCountOverride: remaining);
            return;
        }

        var queueCount = GetActiveQueueCount();
        var nextQueued = queueCount > 0 ? GetNextQueuedPackageName() : null;
        QueueDiscordPresenceUpdate(
            queueCount > 0 ? "Queue ready" : "Dashboard",
            nextPackage: nextQueued,
            queueCountOverride: queueCount);
    }

    private static string? NormalizePackageLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed == "-" || string.Equals(trimmed, "All caught up", StringComparison.OrdinalIgnoreCase))
            return null;

        return trimmed;
    }

    private void UpdateLicenseTierDisplay(AppSettings? settings = null)
    {
        if (_licenseTierTextBlock is null)
            return;

        settings ??= _settings;
        var tier = settings?.LicenseTier;
        var normalizedTier = string.IsNullOrWhiteSpace(tier) ? "Free" : tier.Trim();
        var isPro = string.Equals(normalizedTier, "Pro", StringComparison.OrdinalIgnoreCase);

        _licenseTierTextBlock.Text = isPro ? "Pro Version" : "Free Version";

        if (_licenseTierBadge is not null)
            _licenseTierBadge.Classes.Set("pro-tier", isPro);

        if (_upgradeButton is not null)
        {
            if (isPro)
            {
                _upgradeButton.Content = "Pro features unlocked";
                _upgradeButton.IsEnabled = false;
                ToolTip.SetTip(_upgradeButton, "Premium features are already enabled");
            }
            else
            {
                _upgradeButton.Content = "Upgrade to Pro";
                _upgradeButton.IsEnabled = true;
                ToolTip.SetTip(_upgradeButton, "Unlock upcoming premium features");
            }
        }
    }

    private void UpgradeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_upgradeButton?.IsEnabled == false)
            return;

        e.Handled = true;

        try
        {
            Process.Start(new ProcessStartInfo(ProUpgradeInfoUrl) { UseShellExecute = true });
            ShowDropStatusMessage(
                "Opening upgrade details",
                "We launched your browser with information about EasyExtract Pro.",
                TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            ShowDropStatusMessage(
                "Couldn't open upgrade page",
                ex.Message,
                TimeSpan.FromSeconds(6));
        }
    }

    private async Task ApplyCustomBackgroundAsync(AppSettings settings)
    {
        var backgroundSettings = settings.CustomBackgroundImage;
        if (backgroundSettings is null)
        {
            await Dispatcher.UIThread.InvokeAsync(() => SetBackgroundBrush(_defaultBackgroundBrush, null));
            return;
        }

        if (!backgroundSettings.IsEnabled)
        {
            await Dispatcher.UIThread.InvokeAsync(() => SetBackgroundBrush(_defaultBackgroundBrush, null));
            return;
        }

        var backgroundPath = backgroundSettings.BackgroundPath;
        if (string.IsNullOrWhiteSpace(backgroundPath))
        {
            await Dispatcher.UIThread.InvokeAsync(() => SetBackgroundBrush(_defaultBackgroundBrush, null));
            return;
        }

        var opacity = Math.Clamp(backgroundSettings.BackgroundOpacity, 0.0, 1.0);
        var bitmap = await LoadBackgroundBitmapAsync(backgroundPath);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (bitmap is null)
            {
                SetBackgroundBrush(_defaultBackgroundBrush, null);
                return;
            }

            var imageBrush = new ImageBrush(bitmap)
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center,
                Opacity = opacity
            };

            SetBackgroundBrush(imageBrush, bitmap);
        });
    }

    private void SetBackgroundBrush(IBrush brush, Bitmap? associatedBitmap)
    {
        var previousBitmap = _currentBackgroundBitmap;
        _currentBackgroundBitmap = associatedBitmap;

        Background = brush;

        if (!ReferenceEquals(previousBitmap, associatedBitmap))
            previousBitmap?.Dispose();
    }

    private void ApplyTheme(int themeIndex)
    {
        var targetVariant = themeIndex switch
        {
            1 => ThemeVariant.Light,
            2 => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };

        if (Application.Current is { } app && app.RequestedThemeVariant != targetVariant)
            app.RequestedThemeVariant = targetVariant;

        if (Application.Current is App easyApp)
            easyApp.ApplyThemeResources(targetVariant);

        if (RequestedThemeVariant != targetVariant)
            RequestedThemeVariant = targetVariant;
    }

    private static async Task<Bitmap?> LoadBackgroundBitmapAsync(string path)
    {
        try
        {
            if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
            {
                if (uri.IsFile)
                {
                    var localPath = uri.LocalPath;
                    if (!File.Exists(localPath))
                        return null;

                    return await Task.Run(() => new Bitmap(localPath));
                }

                if (uri.Scheme is "http" or "https")
                {
                    var bytes = await BackgroundHttpClient.GetByteArrayAsync(uri);
                    return await Task.Run(() => new Bitmap(new MemoryStream(bytes)));
                }

                return null;
            }

            if (!File.Exists(path))
                return null;

            return await Task.Run(() => new Bitmap(path));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load background image from '{path}': {ex}");
            return null;
        }
    }

    private void SetVersionText()
    {
        if (_versionTextBlock is null)
            return;

        CancelVersionStatusReset();

        var version = VersionProvider.GetApplicationVersion();
        if (string.IsNullOrWhiteSpace(version))
            version = _settings.Update?.CurrentVersion;

        version = version?.Trim();

        if (string.IsNullOrWhiteSpace(version))
        {
            _currentVersionDisplay = null;
            _versionTextBlock.Text = UnknownVersionLabel;
            return;
        }

        _currentVersionDisplay = version;
        if (_settings.Update is null)
            _settings.Update = new UpdateSettings();
        _settings.Update.CurrentVersion = version;
        _versionTextBlock.Text = $"Version {version}";
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        DiscordRpcService.Instance.Dispose();
        _currentBackgroundBitmap?.Dispose();
        _currentBackgroundBitmap = null;
    }

    private void CancelVersionStatusReset()
    {
        if (_versionStatusReset is null)
            return;

        _versionStatusReset.Dispose();
        _versionStatusReset = null;
    }

    private void SetVersionStatusMessage(string? status, TimeSpan? resetAfter = null)
    {
        if (_versionTextBlock is null)
            return;

        string label;
        if (string.IsNullOrWhiteSpace(_currentVersionDisplay))
        {
            label = UnknownVersionLabel;
            _versionTextBlock.Text = string.IsNullOrWhiteSpace(status)
                ? label
                : $"{label} - {status}";
        }
        else
        {
            label = $"Version {_currentVersionDisplay}";
            _versionTextBlock.Text = string.IsNullOrWhiteSpace(status)
                ? label
                : $"{label} - {status}";
        }

        CancelVersionStatusReset();

        if (resetAfter is { } duration && duration > TimeSpan.Zero)
            _versionStatusReset = DispatcherTimer.RunOnce(() =>
            {
                if (_versionTextBlock is null)
                    return;

                if (string.IsNullOrWhiteSpace(_currentVersionDisplay))
                    _versionTextBlock.Text = UnknownVersionLabel;
                else
                    _versionTextBlock.Text = $"Version {_currentVersionDisplay}";

                _versionStatusReset = null;
            }, duration);
    }

    private string? GetCurrentVersionForComparison()
    {
        if (!string.IsNullOrWhiteSpace(_currentVersionDisplay))
            return _currentVersionDisplay;

        var settingsVersion = _settings.Update?.CurrentVersion;
        if (!string.IsNullOrWhiteSpace(settingsVersion))
            return settingsVersion.Trim();

        var assemblyVersion = VersionProvider.GetApplicationVersion();
        return string.IsNullOrWhiteSpace(assemblyVersion) ? null : assemblyVersion.Trim();
    }

    private static bool TryParseVersion(string? value, [NotNullWhen(true)] out Version? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();

        if (normalized.StartsWith("version", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[7..].Trim();

        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[1..].Trim();

        var separatorIndex = normalized.IndexOfAny(new[] { ' ', '-', '+', '_' });
        if (separatorIndex > 0)
            normalized = normalized[..separatorIndex].Trim();

        var length = 0;
        while (length < normalized.Length)
        {
            var c = normalized[length];
            if ((c >= '0' && c <= '9') || c == '.')
            {
                length++;
                continue;
            }

            break;
        }

        if (length <= 0)
            return false;

        normalized = normalized[..length].Trim('.');
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        return Version.TryParse(normalized, out result);
    }

    private IBrush ResolveDefaultBackgroundBrush()
    {
        if (Application.Current?.Resources.TryGetValue("EasyWindowBackgroundBrush", out var resource) == true &&
            resource is IBrush brush)
            return brush;

        return Background ?? new SolidColorBrush(Colors.Black);
    }

    private readonly record struct ExtractionItem(string Path, UnityPackageFile? QueueEntry);

    private sealed class QueueItemDisplay : INotifyPropertyChanged
    {
        private bool _isExtracting;

        public QueueItemDisplay(UnityPackageFile source, string normalizedPath)
        {
            NormalizedPath = normalizedPath;
            FilePath = string.IsNullOrWhiteSpace(source.FilePath) ? normalizedPath : source.FilePath;
            FileName = string.IsNullOrWhiteSpace(source.FileName)
                ? Path.GetFileName(FilePath)
                : source.FileName;
            FileSizeBytes = ParseFileSize(source.FileSize);
            LastUpdated = ParseLastUpdated(source.FileDate);
            UpdateFrom(source);
        }

        public string NormalizedPath { get; }

        public string FilePath { get; }

        public string FileName { get; }

        public long FileSizeBytes { get; }

        public DateTimeOffset? LastUpdated { get; }

        public string SizeText => FormatFileSize(FileSizeBytes);

        public string StatusText => IsExtracting ? "Extracting..." : "Queued";

        public string LocationText => string.IsNullOrWhiteSpace(FilePath)
            ? "Location unavailable"
            : Path.GetDirectoryName(FilePath) is { Length: > 0 } directory
                ? directory
                : FilePath;

        public bool IsExtracting
        {
            get => _isExtracting;
            private set
            {
                if (_isExtracting == value)
                    return;

                _isExtracting = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void UpdateFrom(UnityPackageFile source)
        {
            IsExtracting = source.IsExtracting;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static long ParseFileSize(string? input)
        {
            if (long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 0)
                return value;

            return 0;
        }

        private static DateTimeOffset? ParseLastUpdated(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            if (DateTimeOffset.TryParse(
                    input,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var result))
                return result;

            if (DateTimeOffset.TryParse(input, CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal,
                    out var fallback))
                return fallback;

            return null;
        }
    }

    private readonly record struct QueueResult(int AddedCount, int AlreadyQueuedCount);
}