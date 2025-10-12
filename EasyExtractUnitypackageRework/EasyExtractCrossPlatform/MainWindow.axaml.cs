using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using EasyExtractCrossPlatform.Models;
using EasyExtractCrossPlatform.Services;

namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private const string UnityPackageExtension = ".unitypackage";
    private readonly Border? _dropZoneBorder;
    private readonly ContentControl? _overlayContent;
    private readonly Border? _overlayHost;
    private readonly TextBlock? _versionTextBlock;
    private Control? _activeOverlayContent;
    private IDisposable? _dropSuccessReset;
    private AppSettings _settings = new();

    public MainWindow()
    {
        InitializeComponent();
        _dropZoneBorder = this.FindControl<Border>("DropZoneBorder");
        _versionTextBlock = this.FindControl<TextBlock>("VersionTextBlock");
        _overlayHost = this.FindControl<Border>("OverlayHost");
        _overlayContent = this.FindControl<ContentControl>("OverlayContent");

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
        var hasUnityPackage = ContainsUnityPackage(e);
        if (hasUnityPackage)
        {
            SetDropZoneClass("drop-success", true);
            _dropSuccessReset?.Dispose();
            _dropSuccessReset = DispatcherTimer.RunOnce(
                () => SetDropZoneClass("drop-success", false),
                TimeSpan.FromMilliseconds(750));

            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }

        ResetDragClasses();
        e.Handled = true;
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

    private static bool ContainsUnityPackage(DragEventArgs e)
    {
        var storageItems = e.Data.GetFiles();
        if (storageItems is null)
            return false;

        return storageItems.Any(item =>
        {
            if (item is not IStorageFile file)
                return false;

            var name = file.Name;
            return !string.IsNullOrWhiteSpace(name) &&
                   string.Equals(Path.GetExtension(name), UnityPackageExtension, StringComparison.OrdinalIgnoreCase);
        });
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

    private void DetailsBtn_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_overlayContent?.Content is ReleaseNotesView)
            return;

        var releaseNotesView = new ReleaseNotesView();
        releaseNotesView.CloseRequested += OnReleaseNotesCloseRequested;

        ShowOverlay(releaseNotesView);
    }

    private void SettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_overlayContent?.Content is SettingsView)
            return;

        var settingsView = new SettingsView();
        settingsView.SettingsSaved += OnSettingsSaved;
        settingsView.Cancelled += OnSettingsCancelled;

        ShowOverlay(settingsView);
    }

    private void OnReleaseNotesCloseRequested(object? sender, EventArgs e)
    {
        if (sender is ReleaseNotesView releaseNotesView)
            CloseOverlay(releaseNotesView);
    }

    private void OnSettingsSaved(object? sender, AppSettings settings)
    {
        if (sender is not SettingsView settingsView)
            return;

        _settings = settings;
        ApplySettings(_settings);
        SetVersionText();

        CloseOverlay(settingsView);
    }

    private void OnSettingsCancelled(object? sender, EventArgs e)
    {
        if (sender is SettingsView settingsView)
            CloseOverlay(settingsView);
    }

    private void ShowOverlay(Control view)
    {
        if (_overlayHost is null || _overlayContent is null)
            return;

        if (_activeOverlayContent is not null)
            CloseOverlay();

        _activeOverlayContent = view;
        _overlayContent.Content = view;
        _overlayHost.IsVisible = true;
    }

    private void CloseOverlay(Control? requestingView = null)
    {
        if (_overlayHost is null || _overlayContent is null)
            return;

        if (_activeOverlayContent is null)
            return;

        if (requestingView is not null && !ReferenceEquals(_activeOverlayContent, requestingView))
            return;

        DetachOverlayHandlers(_activeOverlayContent);

        _overlayContent.Content = null;
        _overlayHost.IsVisible = false;
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

    private void LoadSettings()
    {
        _settings = AppSettingsService.Load();
        ApplySettings(_settings);
    }

    private void ApplySettings(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.AppTitle))
            Title = settings.AppTitle;
    }

    private void SetVersionText()
    {
        if (_versionTextBlock is null)
            return;

        var version = !string.IsNullOrWhiteSpace(_settings?.Update.CurrentVersion)
            ? _settings.Update.CurrentVersion
            : GetApplicationVersion();

        if (!string.IsNullOrWhiteSpace(version))
            _versionTextBlock.Text = $"Version {version}";
    }

    private static string? GetApplicationVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
            return informationalVersion;

        return assembly.GetName().Version?.ToString();
    }
}