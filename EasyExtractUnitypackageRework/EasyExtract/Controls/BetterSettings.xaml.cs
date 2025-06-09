using System.Windows.Media;
using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Services;
using EasyExtract.Utilities.Logger;
using EasyExtract.Views;

namespace EasyExtract.Controls;

public partial class BetterSettings
{
    private readonly BackgroundManager _backgroundManager = BackgroundManager.Instance;

    public BetterSettings()
    {
        InitializeComponent();
        DataContext = ConfigHandler.Instance.Config;
    }

    private async void BetterSettings_OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Dashboard.Instance.NavigateBackBtn.Visibility = Visibility.Visible;
            await ChangeUiToMatchConfigAsync();
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Failed to load BetterSettings", "UI");
        }
    }

    private async Task ChangeUiToMatchConfigAsync()
    {
        try
        {
            if (ConfigHandler.Instance.Config.UwUModeActive) BetterUwUifyer.ApplyUwUModeToVisualTree(this);

            var context = new Dictionary<string, object>();

            var dynamicScalingMode = Enum.GetValues(typeof(DynamicScalingModes))
                .Cast<DynamicScalingModes>()
                .ToList();
            DynamicScalingComboBox.ItemsSource = dynamicScalingMode;
            context.Add("DynamicScalingModes", dynamicScalingMode);

            ContextMenuSwitch.IsChecked = ConfigHandler.Instance.Config.ContextMenuToggle;
            UwUToggleSwitch.IsChecked = ConfigHandler.Instance.Config.UwUModeActive;
            context.Add("ContextMenuEnabled", ConfigHandler.Instance.Config.ContextMenuToggle);
            context.Add("UwUModeActive", ConfigHandler.Instance.Config.UwUModeActive);

            var themes = Enum.GetValues(typeof(AvailableThemes))
                .Cast<AvailableThemes>()
                .Where(theme => theme != AvailableThemes.None)
                .ToList();

            ThemeComboBox.ItemsSource = themes;
            context.Add("AvailableThemes", themes);

            // Initialize logger configuration
            EnableStackTraceToggle.IsChecked = ConfigHandler.Instance.Config.EnableStackTrace;
            EnablePerformanceLoggingToggle.IsChecked = ConfigHandler.Instance.Config.EnablePerformanceLogging;
            EnableMemoryTrackingToggle.IsChecked = ConfigHandler.Instance.Config.EnableMemoryTracking;
            EnableAsyncLoggingToggle.IsChecked = ConfigHandler.Instance.Config.EnableAsyncLogging;

            context.Add("EnableStackTrace", ConfigHandler.Instance.Config.EnableStackTrace);
            context.Add("EnablePerformanceLogging", ConfigHandler.Instance.Config.EnablePerformanceLogging);
            context.Add("EnableMemoryTracking", ConfigHandler.Instance.Config.EnableMemoryTracking);
            context.Add("EnableAsyncLogging", ConfigHandler.Instance.Config.EnableAsyncLogging);

            CheckForUpdatesOnStartUpToggleSwitch.IsChecked = ConfigHandler.Instance.Config.Update.AutoUpdate;
            DiscordRpcToggleSwitch.IsChecked = ConfigHandler.Instance.Config.DiscordRpc;
            DefaultTempPathTextBox.Text = ConfigHandler.Instance.Config.DefaultTempPath;
            DefaultOutputPathTextBox.Text = ConfigHandler.Instance.Config.DefaultOutputPath;
            BackgroundOpacitySlider.Value = ConfigHandler.Instance.Config.CustomBackgroundImage.BackgroundOpacity;

            context.Add("AutoUpdate", ConfigHandler.Instance.Config.Update.AutoUpdate);
            context.Add("DiscordRpcEnabled", ConfigHandler.Instance.Config.DiscordRpc);
            context.Add("TempPath", ConfigHandler.Instance.Config.DefaultTempPath);
            context.Add("OutputPath", ConfigHandler.Instance.Config.DefaultOutputPath);
            context.Add("BackgroundOpacity", ConfigHandler.Instance.Config.CustomBackgroundImage.BackgroundOpacity);

            BetterLogger.LogWithContext("UI updated to match config", context, LogLevel.Info, "UI");
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Failed to update UI to match config", "UI");
        }
    }

    private async void UwUToggleSwitch_OnChecked(object sender, RoutedEventArgs e)
    {
        try
        {
            await UwUToggleSwitchCheckUnCheck();
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Failed to toggle UwU mode", "UI");
        }
    }

    private Task UwUToggleSwitchCheckUnCheck()
    {
        ConfigHandler.Instance.Config.UwUModeActive = UwUToggleSwitch.IsChecked ?? false;
        return Task.CompletedTask;
    }

    private async void UwUToggleSwitch_OnUnchecked(object sender, RoutedEventArgs e)
    {
        try
        {
            await UwUToggleSwitchCheckUnCheck();
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Failed to toggle UwU mode", "UI");
        }
    }

    private void BackgroundOpacitySlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var currentBackground = _backgroundManager.CurrentBackground;
        ConfigHandler.Instance.Config.CustomBackgroundImage.BackgroundOpacity = (float)BackgroundOpacitySlider.Value;
        ConfigHandler.Instance.Config.CustomBackgroundImage.BackgroundPath =
            currentBackground.ImageSource
                .ToString(); // Save the current background path
        // since it's not saved in the config when changing opacity.
        _ = _backgroundManager.UpdateOpacity(ConfigHandler.Instance.Config.CustomBackgroundImage.BackgroundOpacity);
    }

    private async void CheckForUpdatesOnStartUpToggleSwitch_OnChecked(object sender, RoutedEventArgs e)
    {
        try
        {
            await CheckForUpdatesUpdateToggle();
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Failed to update auto-update setting", "UI");
        }
    }

    private Task CheckForUpdatesUpdateToggle()
    {
        ConfigHandler.Instance.Config.Update.AutoUpdate = CheckForUpdatesOnStartUpToggleSwitch.IsChecked ?? false;
        return Task.CompletedTask;
    }

    private async void CheckForUpdatesOnStartUpToggleSwitch_OnUnchecked(object sender, RoutedEventArgs e)
    {
        try
        {
            await CheckForUpdatesUpdateToggle();
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Failed to update auto-update setting", "UI");
        }
    }

    private async void DiscordRpcToggleSwitch_OnChecked(object sender, RoutedEventArgs e)
    {
        try
        {
            ConfigHandler.Instance.Config.DiscordRpc = DiscordRpcToggleSwitch.IsChecked ?? false;
            await DiscordRpcManager.Instance.TryUpdatePresenceAsync("Settings");
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Failed to update Discord RPC status", "UI");
        }
    }

    private void DiscordRpcToggleSwitch_OnUnchecked(object sender, RoutedEventArgs e)
    {
        ConfigHandler.Instance.Config.DiscordRpc = DiscordRpcToggleSwitch.IsChecked ?? false;
        DiscordRpcManager.Instance.Dispose();
    }

    private async void ThemeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (ThemeComboBox.SelectedItem == null) return;
            var selectedTheme = (AvailableThemes)ThemeComboBox.SelectedItem;
            ConfigHandler.Instance.Config.ApplicationTheme = selectedTheme;

            var context = new Dictionary<string, object>
            {
                ["OldTheme"] = e.RemovedItems.Count > 0 ? e.RemovedItems[0] : null,
                ["NewTheme"] = selectedTheme
            };
            BetterLogger.LogWithContext("Theme changed", context, LogLevel.Info, "UI");
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Failed to change theme", "UI");
        }
    }


    private void BackgroundChangeButton_OnClick(object sender, RoutedEventArgs e)
    {
        ConfigHandler.Instance.Config.CustomBackgroundImage.BackgroundPath = string.Empty;
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Image Files (*.jpg, *.jpeg, *.png)|*.jpg;*.jpeg;*.png",
            Title = "Select a background image"
        };
        var result = openFileDialog.ShowDialog();
        if (result != true) return;
        ConfigHandler.Instance.Config.CustomBackgroundImage.BackgroundPath = openFileDialog.FileName;

        _ = _backgroundManager.UpdateBackground(ConfigHandler.Instance.Config.CustomBackgroundImage.BackgroundPath);
        _ = _backgroundManager.UpdateOpacity(ConfigHandler.Instance.Config.CustomBackgroundImage.BackgroundOpacity);
    }

    private void BackgroundResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        ConfigHandler.Instance.Config.CustomBackgroundImage.BackgroundPath = string.Empty;
        _ = _backgroundManager.ResetBackground();
    }

    private async void DefaultTempPathChangeButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var folderDialog = new OpenFolderDialog();
            var result = folderDialog.ShowDialog();
            if (result != true) return;
            DefaultTempPathTextBox.Text = folderDialog.FolderName;

            ConfigHandler.Instance.Config.DefaultTempPath = folderDialog.FolderName;
            BetterLogger.LogWithContext("Default temp path changed", new Dictionary<string, object>
            {
                ["NewPath"] = folderDialog.FolderName
            }, LogLevel.Info, "Settings");
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Failed to change default temp path", "Settings");
        }
    }

    private async void DefaultTempPathResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ConfigHandler.Instance.Config.DefaultTempPath =
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract",
                    "Temp");
            DefaultTempPathTextBox.Text = ConfigHandler.Instance.Config.DefaultTempPath;
            BetterLogger.LogWithContext("Default temp path reset", new Dictionary<string, object>
            {
                ["DefaultTempPath"] = ConfigHandler.Instance.Config.DefaultTempPath
            }, LogLevel.Info, "Settings");
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Failed to reset default temp path", "Settings");
        }
    }

    private async void ContextMenuSwitch_OnChecked(object sender, RoutedEventArgs e)
    {
        try
        {
            await UpdateContextMenuToggleSettingAsync();
            BetterLogger.LogWithContext("Context menu enabled", new Dictionary<string, object>
            {
                ["Enabled"] = ContextMenuSwitch.IsChecked ?? false
            }, LogLevel.Info, "Settings");
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Failed to update context menu setting", "Settings");
        }
    }

    private async void ContextMenuSwitch_OnUnchecked(object sender, RoutedEventArgs e)
    {
        try
        {
            await UpdateContextMenuToggleSettingAsync();
            BetterLogger.LogWithContext("Context menu disabled", new Dictionary<string, object>
            {
                ["Enabled"] = ContextMenuSwitch.IsChecked ?? false
            }, LogLevel.Info, "Settings");
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Failed to update context menu setting", "Settings");
        }
    }

    private Task UpdateContextMenuToggleSettingAsync()
    {
        ConfigHandler.Instance.Config.ContextMenuToggle = ContextMenuSwitch.IsChecked ?? false;
        return Task.CompletedTask;
    }

    private void BetterSettings_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        switch (ConfigHandler.Instance.Config.DynamicScalingMode)
        {
            case DynamicScalingModes.Off:
                break;

            case DynamicScalingModes.Simple:
            {
                break;
            }
            case DynamicScalingModes.Experimental:
            {
                var scaleFactor = e.NewSize.Width / 1100.0;
                switch (scaleFactor)
                {
                    case < 0.5:
                        scaleFactor = 0.5;
                        break;
                    case > 2.0:
                        scaleFactor = 2.0;
                        break;
                }

                MainGrid.LayoutTransform = new ScaleTransform(scaleFactor, scaleFactor);
                break;
            }
        }
    }


    private void DynamicScalingComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DynamicScalingComboBox.SelectedItem == null) return;
        var selectedMode = (DynamicScalingModes)DynamicScalingComboBox.SelectedItem;
        ConfigHandler.Instance.Config.DynamicScalingMode = selectedMode;
    }

    private async void DefaultOutputPathChangeButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var folderDialog = new OpenFolderDialog();
            var result = folderDialog.ShowDialog();
            if (result != true) return;

            DefaultOutputPathTextBox.Text = folderDialog.FolderName;
            ConfigHandler.Instance.Config.DefaultOutputPath = folderDialog.FolderName;

            BetterLogger.LogWithContext("Default output path changed", new Dictionary<string, object>
            {
                ["NewPath"] = folderDialog.FolderName
            }, LogLevel.Info, "Settings");
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Failed to change default output path", "Settings");
        }
    }

    private async void DefaultOutputPathResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ConfigHandler.Instance.Config.DefaultOutputPath =
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract",
                    "Extracted");

            DefaultOutputPathTextBox.Text = ConfigHandler.Instance.Config.DefaultOutputPath;

            BetterLogger.LogWithContext("Default output path reset", new Dictionary<string, object>
            {
                ["DefaultPath"] = ConfigHandler.Instance.Config.DefaultOutputPath
            }, LogLevel.Info, "Settings");
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Failed to reset default output path", "Settings");
        }
    }

    private void SoundCardToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        ConfigHandler.Instance.Config.EnableSound = SoundCardToggle.IsChecked ?? false;
    }

    private void SoundCardToggle_OnUnchecked(object sender, RoutedEventArgs e)
    {
        ConfigHandler.Instance.Config.EnableSound = SoundCardToggle.IsChecked ?? false;
    }

    private void SoundSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        ConfigHandler.Instance.Config.SoundVolume = (float)e.NewValue;

        if (SoundSlider.Value <= 0)
            SoundCardToggle.IsChecked = false;
        else if (SoundCardToggle.IsChecked == false)
            SoundCardToggle.IsChecked = true;
    }

    // Logger configuration event handlers
    private void EnableStackTraceToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        try
        {
            ConfigHandler.Instance.Config.EnableStackTrace = true;
            BetterLogger.EnableStackTrace(true);
            BetterLogger.Info("Stack trace logging enabled", "Settings");
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Failed to enable stack trace logging", "Settings");
        }
    }

    private void EnableStackTraceToggle_OnUnchecked(object sender, RoutedEventArgs e)
    {
        try
        {
            ConfigHandler.Instance.Config.EnableStackTrace = false;
            BetterLogger.EnableStackTrace(false);
            BetterLogger.Info("Stack trace logging disabled", "Settings");
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Failed to disable stack trace logging", "Settings");
        }
    }

    private void EnablePerformanceLoggingToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        try
        {
            ConfigHandler.Instance.Config.EnablePerformanceLogging = true;
            BetterLogger.EnablePerformanceLogging(true);
            BetterLogger.Info("Performance logging enabled", "Settings");
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Failed to enable performance logging", "Settings");
        }
    }

    private void EnablePerformanceLoggingToggle_OnUnchecked(object sender, RoutedEventArgs e)
    {
        try
        {
            ConfigHandler.Instance.Config.EnablePerformanceLogging = false;
            BetterLogger.EnablePerformanceLogging(false);
            BetterLogger.Info("Performance logging disabled", "Settings");
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Failed to disable performance logging", "Settings");
        }
    }

    private void EnableMemoryTrackingToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        try
        {
            ConfigHandler.Instance.Config.EnableMemoryTracking = true;
            BetterLogger.EnableMemoryTracking(true);
            BetterLogger.Info("Memory tracking enabled", "Settings");
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Failed to enable memory tracking", "Settings");
        }
    }

    private void EnableMemoryTrackingToggle_OnUnchecked(object sender, RoutedEventArgs e)
    {
        try
        {
            ConfigHandler.Instance.Config.EnableMemoryTracking = false;
            BetterLogger.EnableMemoryTracking(false);
            BetterLogger.Info("Memory tracking disabled", "Settings");
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Failed to disable memory tracking", "Settings");
        }
    }

    private void EnableAsyncLoggingToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        try
        {
            ConfigHandler.Instance.Config.EnableAsyncLogging = true;
            BetterLogger.EnableAsyncLogging(true);
            BetterLogger.Info("Asynchronous logging enabled", "Settings");
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Failed to enable asynchronous logging", "Settings");
        }
    }

    private void EnableAsyncLoggingToggle_OnUnchecked(object sender, RoutedEventArgs e)
    {
        try
        {
            ConfigHandler.Instance.Config.EnableAsyncLogging = false;
            BetterLogger.EnableAsyncLogging(false);
            BetterLogger.Info("Asynchronous logging disabled", "Settings");
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Failed to disable asynchronous logging", "Settings");
        }
    }

    private void OpenLogFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasyExtract", "Logs");

            if (!Directory.Exists(logPath)) Directory.CreateDirectory(logPath);

            Process.Start(new ProcessStartInfo
            {
                FileName = logPath,
                UseShellExecute = true,
                Verb = "open"
            });

            BetterLogger.Info($"Opened log folder: {logPath}", "Settings");
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Failed to open log folder", "Settings");
        }
    }
}