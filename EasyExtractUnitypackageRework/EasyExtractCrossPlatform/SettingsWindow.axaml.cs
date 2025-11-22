using Avalonia.Controls.Primitives;

namespace EasyExtractCrossPlatform;

public partial class SettingsWindow : Window
{
    private readonly NumericUpDown? _assetCountNumeric;
    private readonly NumericUpDown? _assetLimitNumeric;
    private readonly Slider? _backgroundOpacitySlider;
    private readonly TextBox? _backgroundPathBox;
    private readonly TextBox? _defaultOutputPathBox;
    private readonly TextBox? _defaultTempPathBox;
    private readonly NumericUpDown? _packageLimitNumeric;
    private readonly IDisposable? _responsiveLayoutSubscription;
    private readonly Slider? _soundVolumeSlider;
    private readonly TextBlock? _statusTextBlock;
    private readonly ComboBox? _themeComboBox;
    private readonly SettingsViewModel _viewModel;
    private readonly IDisposable _windowPlacementTracker;
    private bool _autoSaveHandlersAttached;
    private bool _autoSaveReady;
    private bool _lastSaveFailed;
    private DateTime _lastSliderCueAt = DateTime.MinValue;
    private bool _suppressAutoSave;

    public SettingsWindow()
    {
        InitializeComponent();
        LinuxUiHelper.ApplyWindowTweaks(this);
        _responsiveLayoutSubscription = ResponsiveWindowHelper.Enable(this, 1000, 1500);
        _windowPlacementTracker = WindowPlacementService.Attach(this, nameof(SettingsWindow));

        _viewModel = SettingsViewModel.CreateFromStorage();
        DataContext = _viewModel;

        _defaultOutputPathBox = this.FindControl<TextBox>("DefaultOutputPathBox");
        _defaultTempPathBox = this.FindControl<TextBox>("DefaultTempPathBox");
        _backgroundPathBox = this.FindControl<TextBox>("BackgroundPathBox");
        _themeComboBox = this.FindControl<ComboBox>("ThemeComboBox");
        _statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock");
        _soundVolumeSlider = this.FindControl<Slider>("SoundVolumeSlider");
        _backgroundOpacitySlider = this.FindControl<Slider>("BackgroundOpacitySlider");
        _assetLimitNumeric = this.FindControl<NumericUpDown>("AssetLimitNumeric");
        _packageLimitNumeric = this.FindControl<NumericUpDown>("PackageLimitNumeric");
        _assetCountNumeric = this.FindControl<NumericUpDown>("AssetCountNumeric");

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
        AttachToggleHandler("UwUModeToggle");
        AttachToggleHandler("CategoryStructureToggle");
        AttachToggleHandler("SecurityScanToggle");
        AttachToggleHandler("SoundToggle");
        AttachToggleHandler("CustomBackgroundToggle");
        AttachToggleHandler("StackTraceToggle");
        AttachToggleHandler("PerformanceLoggingToggle");
        AttachToggleHandler("MemoryTrackingToggle");
        AttachToggleHandler("AsyncLoggingToggle");
        AttachToggleHandler("AutoUpdateToggle");

        AttachSliderHandler("SoundVolumeSlider");
        AttachSliderHandler("BackgroundOpacitySlider");
        AttachNumericUpDownHandler(_assetLimitNumeric);
        AttachNumericUpDownHandler(_packageLimitNumeric);
        AttachNumericUpDownHandler(_assetCountNumeric);

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

    private void AttachNumericUpDownHandler(NumericUpDown? numericUpDown)
    {
        if (numericUpDown is null)
            return;

        numericUpDown.PropertyChanged += OnNumericUpDownPropertyChanged;
    }

    private void OnTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty && !Equals(e.OldValue, e.NewValue))
            TriggerAutoSave();
    }

    private void OnTogglePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ToggleButton.IsCheckedProperty && !Equals(e.OldValue, e.NewValue))
        {
            if (!_autoSaveReady)
                return;
            TriggerAutoSave();
            if (sender is ToggleButton { IsChecked: not null } toggle)
                PlayToggleCue(toggle.IsChecked);
        }
    }

    private void OnSliderPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == RangeBase.ValueProperty && !Equals(e.OldValue, e.NewValue))
        {
            if (!_autoSaveReady)
                return;
            TriggerAutoSave();
            if (sender is Slider slider)
                PlaySliderCue(slider);
        }
    }

    private void OnComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_autoSaveReady)
            return;
        TriggerAutoSave();
        UiSoundService.Instance.Play(UiSoundEffect.Subtle);
    }

    private void OnNumericUpDownPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == NumericUpDown.ValueProperty && !Equals(e.OldValue, e.NewValue))
        {
            if (!_autoSaveReady)
                return;
            TriggerAutoSave();
            UiSoundService.Instance.Play(UiSoundEffect.Subtle);
        }
    }

    private void TriggerAutoSave()
    {
        if (!_autoSaveReady || _suppressAutoSave)
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

    private void ResetExtractionLimits_OnClick(object? sender, RoutedEventArgs e)
    {
        _suppressAutoSave = true;
        try
        {
            _viewModel.ResetExtractionLimits();
            SyncExtractionLimitInputs();
        }
        finally
        {
            _suppressAutoSave = false;
        }

        TriggerAutoSave();
        UiSoundService.Instance.Play(UiSoundEffect.Positive);
        ShowStatus(LocalizationManager.Instance.GetString("SettingsWindow_Status_LimitsReset"));
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

    private void OpenOutputPath_OnClick(object? sender, RoutedEventArgs e)
    {
        OpenFolderForSetting(_viewModel.Settings.DefaultOutputPath, "Extracted");
    }

    private void OpenTempPath_OnClick(object? sender, RoutedEventArgs e)
    {
        OpenFolderForSetting(_viewModel.Settings.DefaultTempPath, "Temp");
    }

    private void OpenFolderForSetting(string? configuredPath, string fallbackSubfolder)
    {
        var target = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(AppSettingsService.SettingsDirectory, fallbackSubfolder)
            : configuredPath;

        try
        {
            Directory.CreateDirectory(target);
            var startInfo = CreateOpenFolderStartInfo(target);
            Process.Start(startInfo);
            ShowStatus($"Opened folder: {target}");
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to open folder: {ex.Message}", true);
        }
    }

    private static ProcessStartInfo CreateOpenFolderStartInfo(string target)
    {
        if (OperatingSystem.IsWindows())
            return new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{target}\"",
                UseShellExecute = true
            };

        if (OperatingSystem.IsMacOS())
            return new ProcessStartInfo
            {
                FileName = "open",
                Arguments = target,
                UseShellExecute = false
            };

        return new ProcessStartInfo
        {
            FileName = "xdg-open",
            Arguments = target,
            UseShellExecute = false
        };
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

        if (string.IsNullOrWhiteSpace(message))
        {
            _statusTextBlock.ClearValue(TextBlock.ForegroundProperty);
            return;
        }

        if (isError)
        {
            _statusTextBlock.Foreground = Brushes.OrangeRed;
            UiSoundService.Instance.Play(UiSoundEffect.Negative);
        }
        else
        {
            _statusTextBlock.ClearValue(TextBlock.ForegroundProperty);
        }
    }

    private void PlayToggleCue(bool? isChecked)
    {
        var effect = isChecked switch
        {
            true => UiSoundEffect.Positive,
            false => UiSoundEffect.Subtle,
            _ => UiSoundEffect.None
        };

        if (effect != UiSoundEffect.None)
            UiSoundService.Instance.Play(effect);
    }

    private void PlaySliderCue(Slider slider)
    {
        var now = DateTime.UtcNow;
        if (now - _lastSliderCueAt < TimeSpan.FromMilliseconds(160))
            return;

        _lastSliderCueAt = now;

        var effect = ReferenceEquals(slider, _soundVolumeSlider)
            ? UiSoundEffect.Positive
            : UiSoundEffect.Subtle;

        UiSoundService.Instance.Play(effect);
    }

    private void SyncExtractionLimitInputs()
    {
        ApplyNumericValue(_assetLimitNumeric, _viewModel.ExtractionMaxAssetMegabytes);
        ApplyNumericValue(_packageLimitNumeric, _viewModel.ExtractionMaxPackageGigabytes);
        ApplyNumericValue(_assetCountNumeric, _viewModel.ExtractionMaxAssetCount);
    }

    private static void ApplyNumericValue(NumericUpDown? control, double rawValue)
    {
        if (control is null)
            return;

        var value = ConvertToDecimal(rawValue);

        var min = control.Minimum;
        var max = control.Maximum;
        if (max < min)
            (min, max) = (max, min);

        var clamped = decimal.Clamp(value, min, max);
        control.SetCurrentValue(NumericUpDown.ValueProperty, clamped);
    }

    private static decimal ConvertToDecimal(double value)
    {
        if (!double.IsFinite(value))
            return 0m;

        var capped = Math.Clamp(value, (double)decimal.MinValue, (double)decimal.MaxValue);
        return Convert.ToDecimal(capped);
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

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _responsiveLayoutSubscription?.Dispose();
        _windowPlacementTracker.Dispose();
    }
}