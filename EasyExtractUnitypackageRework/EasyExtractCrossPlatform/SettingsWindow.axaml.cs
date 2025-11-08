using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using EasyExtractCrossPlatform.Localization;
using EasyExtractCrossPlatform.Models;
using EasyExtractCrossPlatform.Services;
using EasyExtractCrossPlatform.Utilities;
using EasyExtractCrossPlatform.ViewModels;

namespace EasyExtractCrossPlatform;

public partial class SettingsWindow : Window
{
    private readonly TextBox? _backgroundPathBox;
    private readonly TextBox? _defaultOutputPathBox;
    private readonly TextBox? _defaultTempPathBox;
    private readonly TextBlock? _statusTextBlock;
    private readonly ComboBox? _themeComboBox;
    private readonly SettingsViewModel _viewModel;
    private bool _autoSaveHandlersAttached;
    private bool _autoSaveReady;
    private bool _lastSaveFailed;

    public SettingsWindow()
    {
        InitializeComponent();
        LinuxUiHelper.ApplyWindowTweaks(this);

        _viewModel = SettingsViewModel.CreateFromStorage();
        DataContext = _viewModel;

        _defaultOutputPathBox = this.FindControl<TextBox>("DefaultOutputPathBox");
        _defaultTempPathBox = this.FindControl<TextBox>("DefaultTempPathBox");
        _backgroundPathBox = this.FindControl<TextBox>("BackgroundPathBox");
        _themeComboBox = this.FindControl<ComboBox>("ThemeComboBox");
        _statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock");

        Opened += OnOpened;
    }

    public event EventHandler<AppSettings>? SettingsSaved;

    private void OnOpened(object? sender, EventArgs e)
    {
        AttachAutoSaveHandlers();
    }

    private void AttachAutoSaveHandlers()
    {
        if (_autoSaveHandlersAttached)
            return;

        _autoSaveHandlersAttached = true;
        _autoSaveReady = false;

        AttachTextBoxHandler(_defaultOutputPathBox);
        AttachTextBoxHandler(_defaultTempPathBox);
        AttachTextBoxHandler(_backgroundPathBox);

        AttachToggleHandler("ContextMenuToggle");
        AttachToggleHandler("DiscordRpcToggle");
        AttachToggleHandler("CategoryStructureToggle");
        AttachToggleHandler("SoundToggle");
        AttachToggleHandler("CustomBackgroundToggle");
        AttachToggleHandler("StackTraceToggle");
        AttachToggleHandler("PerformanceLoggingToggle");
        AttachToggleHandler("MemoryTrackingToggle");
        AttachToggleHandler("AsyncLoggingToggle");
        AttachToggleHandler("AutoUpdateToggle");

        AttachSliderHandler("SoundVolumeSlider");
        AttachSliderHandler("BackgroundOpacitySlider");

        AttachComboBoxHandler(_themeComboBox);

        Dispatcher.UIThread.Post(() =>
        {
            _autoSaveReady = true;
            ShowStatus(LocalizationManager.Instance.GetString("SettingsWindow_Status_AutoSaveReady"));
        });
    }

    private void AttachTextBoxHandler(TextBox? textBox)
    {
        if (textBox is null)
            return;

        textBox.PropertyChanged += OnTextBoxPropertyChanged;
    }

    private void AttachToggleHandler(string controlName)
    {
        if (this.FindControl<ToggleSwitch>(controlName) is { } toggle)
            toggle.PropertyChanged += OnTogglePropertyChanged;
    }

    private void AttachSliderHandler(string controlName)
    {
        if (this.FindControl<Slider>(controlName) is { } slider) slider.PropertyChanged += OnSliderPropertyChanged;
    }

    private void AttachComboBoxHandler(ComboBox? comboBox)
    {
        if (comboBox is null)
            return;

        comboBox.SelectionChanged += OnComboBoxSelectionChanged;
    }

    private void OnTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty && !Equals(e.OldValue, e.NewValue))
            TriggerAutoSave();
    }

    private void OnTogglePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ToggleButton.IsCheckedProperty && !Equals(e.OldValue, e.NewValue))
            TriggerAutoSave();
    }

    private void OnSliderPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == RangeBase.ValueProperty && !Equals(e.OldValue, e.NewValue))
            TriggerAutoSave();
    }

    private void OnComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        TriggerAutoSave();
    }

    private void TriggerAutoSave()
    {
        if (!_autoSaveReady)
            return;

        PersistSettings();
    }

    private void PersistSettings()
    {
        try
        {
            AppSettingsService.Save(_viewModel.Settings);
            SettingsSaved?.Invoke(this, _viewModel.Settings);
            if (_lastSaveFailed)
            {
                ShowStatus(LocalizationManager.Instance.GetString("SettingsWindow_Status_Saved"));
                _lastSaveFailed = false;
            }
        }
        catch (Exception ex)
        {
            _lastSaveFailed = true;
            ShowStatus(LocalizationManager.Instance.GetString("SettingsWindow_Status_SaveFailed", ex.Message), true);
        }
    }

    private async void BrowseOutputPath_OnClick(object? sender, RoutedEventArgs e)
    {
        var folder = await PickSingleFolderAsync();
        if (folder?.TryGetLocalPath() is { } localPath && !string.IsNullOrWhiteSpace(localPath))
        {
            _viewModel.Settings.DefaultOutputPath = localPath;
            UpdateTextBoxText(_defaultOutputPathBox, localPath);
            TriggerAutoSave();
        }
    }

    private async void BrowseTempPath_OnClick(object? sender, RoutedEventArgs e)
    {
        var folder = await PickSingleFolderAsync();
        if (folder?.TryGetLocalPath() is { } localPath && !string.IsNullOrWhiteSpace(localPath))
        {
            _viewModel.Settings.DefaultTempPath = localPath;
            UpdateTextBoxText(_defaultTempPathBox, localPath);
            TriggerAutoSave();
        }
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

    private void OpenLogFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var logDirectory = LoggingService.LogDirectory;
            if (LoggingService.TryOpenLogFolder())
                ShowStatus($"Opened log folder: {logDirectory}");
            else
                ShowStatus($"Log folder: {logDirectory}", true);
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to open log folder: {ex.Message}", true);
        }
    }

    private IStorageProvider? GetStorageProvider()
    {
        return StorageProvider;
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

    private static void UpdateTextBoxText(TextBox? textBox, string? value)
    {
        if (textBox is null)
            return;

        var normalized = value ?? string.Empty;
        textBox.SetCurrentValue(TextBox.TextProperty, normalized);

        var caretIndex = normalized.Length;
        if (textBox.SelectionStart != caretIndex)
            textBox.SelectionStart = caretIndex;
        if (textBox.SelectionEnd != caretIndex)
            textBox.SelectionEnd = caretIndex;
        if (textBox.CaretIndex != caretIndex)
            textBox.CaretIndex = caretIndex;
    }
}