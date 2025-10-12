using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private const string UnityPackageExtension = ".unitypackage";
    private readonly Border? _dropZoneBorder;
    private IDisposable? _dropSuccessReset;

    public MainWindow()
    {
        InitializeComponent();
        _dropZoneBorder = this.FindControl<Border>("DropZoneBorder");
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
}