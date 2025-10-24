using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
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

namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private const string UnityPackageExtension = ".unitypackage";
    private static readonly HttpClient BackgroundHttpClient = new();
    private readonly IBrush _defaultBackgroundBrush;
    private readonly string _defaultDropPrimaryText = "Drag & drop files here";
    private readonly string _defaultDropSecondaryText = "Supports batch extraction and live progress updates.";
    private readonly Border? _dropZoneBorder;
    private readonly TextBlock? _dropZonePrimaryTextBlock;
    private readonly TextBlock? _dropZoneSecondaryTextBlock;
    private readonly Border? _overlayCard;
    private readonly ContentControl? _overlayContent;
    private readonly Border? _overlayHost;
    private readonly TextBlock? _versionTextBlock;
    private Control? _activeOverlayContent;
    private Bitmap? _currentBackgroundBitmap;
    private IDisposable? _dropStatusReset;
    private IDisposable? _dropSuccessReset;
    private PixelPoint? _lastNormalPosition;
    private Size? _lastNormalSize;
    private CancellationTokenSource? _overlayAnimationCts;
    private ScaleTransform? _overlayCardScaleTransform;
    private AppSettings _settings = new();

    public MainWindow()
    {
        InitializeComponent();
        _defaultBackgroundBrush = ResolveDefaultBackgroundBrush();
        _dropZoneBorder = this.FindControl<Border>("DropZoneBorder");
        _dropZonePrimaryTextBlock = this.FindControl<TextBlock>("DropZonePrimaryTextBlock");
        _dropZoneSecondaryTextBlock = this.FindControl<TextBlock>("DropZoneSecondaryTextBlock");
        if (_dropZonePrimaryTextBlock?.Text is { Length: > 0 } primaryText)
            _defaultDropPrimaryText = primaryText;
        if (_dropZoneSecondaryTextBlock?.Text is { Length: > 0 } secondaryText)
            _defaultDropSecondaryText = secondaryText;
        _versionTextBlock = this.FindControl<TextBlock>("VersionTextBlock");
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

        Closing += OnMainWindowClosing;
        PositionChanged += OnMainWindowPositionChanged;
        PropertyChanged += OnMainWindowPropertyChanged;

        LoadSettings();
        SetVersionText();
    }

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
            _settings.UnitypackageFiles.Add(new UnityPackageFile
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
            });

            if (historySet.Add(normalizedPath))
                _settings.History.Add(normalizedPath);

            addedCount++;
        }

        if (addedCount > 0)
            AppSettingsService.Save(_settings);

        return new QueueResult(addedCount, alreadyQueuedCount);
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

    private async void DetailsBtn_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_overlayContent?.Content is ReleaseNotesView)
            return;

        var releaseNotesView = new ReleaseNotesView();
        releaseNotesView.CloseRequested += OnReleaseNotesCloseRequested;

        await ShowOverlayAsync(releaseNotesView);
    }

    private async void SettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_overlayContent?.Content is SettingsView)
            return;

        var settingsView = new SettingsView();
        settingsView.SettingsSaved += OnSettingsSaved;
        settingsView.Cancelled += OnSettingsCancelled;

        await ShowOverlayAsync(settingsView);
    }

    private async void OnReleaseNotesCloseRequested(object? sender, EventArgs e)
    {
        if (sender is ReleaseNotesView releaseNotesView)
            await CloseOverlayAsync(releaseNotesView);
    }

    private async void OnSettingsSaved(object? sender, AppSettings settings)
    {
        if (sender is not SettingsView settingsView)
            return;

        _settings = settings;
        ApplySettings(_settings);
        SetVersionText();

        await CloseOverlayAsync(settingsView);
    }

    private async void OnSettingsCancelled(object? sender, EventArgs e)
    {
        if (sender is SettingsView settingsView)
            await CloseOverlayAsync(settingsView);
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
    }

    private void DetachOverlayHandlers(Control control)
    {
        switch (control)
        {
            case ReleaseNotesView releaseNotesView:
                releaseNotesView.CloseRequested -= OnReleaseNotesCloseRequested;
                break;
            case SettingsView settingsView:
                settingsView.SettingsSaved -= OnSettingsSaved;
                settingsView.Cancelled -= OnSettingsCancelled;
                break;
        }
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

        ApplyTheme(settings.ApplicationTheme);
        _ = ApplyCustomBackgroundAsync(settings);
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

        var version = VersionProvider.GetApplicationVersion();

        if (!string.IsNullOrWhiteSpace(version))
            _versionTextBlock.Text = $"Version {version}";
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _currentBackgroundBitmap?.Dispose();
        _currentBackgroundBitmap = null;
    }

    private IBrush ResolveDefaultBackgroundBrush()
    {
        if (Application.Current?.Resources.TryGetValue("EasyWindowBackgroundBrush", out var resource) == true &&
            resource is IBrush brush)
            return brush;

        return Background ?? new SolidColorBrush(Colors.Black);
    }

    private readonly record struct QueueResult(int AddedCount, int AlreadyQueuedCount);
}