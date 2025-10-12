using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using EasyExtractCrossPlatform.Models;
using EasyExtractCrossPlatform.Services;
using EasyExtractCrossPlatform.ViewModels;

namespace EasyExtractCrossPlatform;

public partial class SettingsView : UserControl
{
    private readonly TextBox? _defaultOutputPathBox;
    private readonly TextBox? _defaultTempPathBox;
    private readonly TextBlock? _statusTextBlock;
    private readonly SettingsViewModel _viewModel;

    public SettingsView()
    {
        InitializeComponent();

        _viewModel = SettingsViewModel.CreateFromStorage();
        DataContext = _viewModel;

        _defaultOutputPathBox = this.FindControl<TextBox>("DefaultOutputPathBox");
        _defaultTempPathBox = this.FindControl<TextBox>("DefaultTempPathBox");
        _statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock");
    }

    public event EventHandler<AppSettings>? SettingsSaved;
    public event EventHandler? Cancelled;

    private async void BrowseOutputPath_OnClick(object? sender, RoutedEventArgs e)
    {
        var folder = await PickSingleFolderAsync();
        if (folder?.TryGetLocalPath() is { } localPath && !string.IsNullOrWhiteSpace(localPath))
        {
            _viewModel.Settings.DefaultOutputPath = localPath;
            _defaultOutputPathBox?.SetCurrentValue(TextBox.TextProperty, localPath);
        }
    }

    private async void BrowseTempPath_OnClick(object? sender, RoutedEventArgs e)
    {
        var folder = await PickSingleFolderAsync();
        if (folder?.TryGetLocalPath() is { } localPath && !string.IsNullOrWhiteSpace(localPath))
        {
            _viewModel.Settings.DefaultTempPath = localPath;
            _defaultTempPathBox?.SetCurrentValue(TextBox.TextProperty, localPath);
        }
    }

    private void SaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            AppSettingsService.Save(_viewModel.Settings);
            SettingsSaved?.Invoke(this, _viewModel.Settings);
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to save settings: {ex.Message}", true);
        }
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private async Task<IStorageFolder?> PickSingleFolderAsync()
    {
        var storageProvider = GetStorageProvider();
        if (storageProvider is null)
            return null;

        var result = await storageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                AllowMultiple = false
            });

        return result?.FirstOrDefault();
    }

    private IStorageProvider? GetStorageProvider()
    {
        return TopLevel.GetTopLevel(this)?.StorageProvider;
    }

    private void ShowStatus(string message, bool isError = false)
    {
        if (_statusTextBlock is null)
            return;

        _statusTextBlock.Text = message;

        if (isError)
        {
            _statusTextBlock.Foreground = Brushes.OrangeRed;
        }
        else
        {
            _statusTextBlock.ClearValue(TextBlock.ForegroundProperty);
        }
    }
}