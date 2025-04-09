using System.Windows.Media;
using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Services;
using EasyExtract.Utilities;
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
            await BetterLogger.LogAsync($"Exception in BetterSettings_OnLoaded: {ex.Message}", Importance.Error);
        }
    }

    private async Task ChangeUiToMatchConfigAsync()
    {
        if (ConfigHandler.Instance.Config.UwUModeActive) BetterUwUifyer.ApplyUwUModeToVisualTree(this);
        try
        {
            var dynamicScalingMode = Enum.GetValues(typeof(DynamicScalingModes))
                .Cast<DynamicScalingModes>()
                .ToList();
            DynamicScalingComboBox.ItemsSource = dynamicScalingMode;

            ContextMenuSwitch.IsChecked = ConfigHandler.Instance.Config.ContextMenuToggle;
            UwUToggleSwitch.IsChecked = ConfigHandler.Instance.Config.UwUModeActive;

            var themes = Enum.GetValues(typeof(AvailableThemes))
                .Cast<AvailableThemes>()
                .Where(theme => theme != AvailableThemes.None)
                .ToList();

            ThemeComboBox.ItemsSource = themes;

            foreach (var theme in themes)
                await BetterLogger.LogAsync($"Available Theme: {theme}", Importance.Info);


            CheckForUpdatesOnStartUpToggleSwitch.IsChecked = ConfigHandler.Instance.Config.Update.AutoUpdate;
            DiscordRpcToggleSwitch.IsChecked = ConfigHandler.Instance.Config.DiscordRpc;
            DefaultTempPathTextBox.Text = ConfigHandler.Instance.Config.DefaultTempPath;
            DefaultOutputPathTextBox.Text = ConfigHandler.Instance.Config.DefaultOutputPath;
            BackgroundOpacitySlider.Value = ConfigHandler.Instance.Config.CustomBackgroundImage.BackgroundOpacity;
            await BetterLogger.LogAsync("UI updated to match config", Importance.Info);
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Exception in ChangeUiToMatchConfigAsync: {ex.Message}",
                Importance.Error);
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
            await BetterLogger.LogAsync($"Exception in UwUToggleSwitch_OnChecked: {ex.Message}", Importance.Error);
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
            await BetterLogger.LogAsync($"Exception in UwUToggleSwitch_OnUnchecked: {ex.Message}", Importance.Error);
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
            await BetterLogger.LogAsync($"Exception in CheckForUpdatesOnStartUpToggleSwitch_OnChecked: {ex.Message}",
                Importance.Error);
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
            await BetterLogger.LogAsync($"Exception in CheckForUpdatesOnStartUpToggleSwitch_OnUnchecked: {ex.Message}",
                Importance.Error);
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
            await BetterLogger.LogAsync($"Exception in DiscordRpcToggleSwitch_OnChecked: {ex.Message}",
                Importance.Error);
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
            await BetterLogger.LogAsync($"Theme changed to: {selectedTheme}", Importance.Info);
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Exception in ThemeComboBox_OnSelectionChanged: {ex.Message}",
                Importance.Error);
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
            await BetterLogger.LogAsync($"Default temp path set to: {folderDialog.FolderName}",
                Importance.Info);
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Exception in DefaultTempPathChangeButton_OnClick: {ex.Message}",
                Importance.Error);
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
            await BetterLogger.LogAsync("Default temp path reset", Importance.Info);
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Exception in DefaultTempPathResetButton_OnClick: {ex.Message}",
                Importance.Error);
        }
    }

    private async void ContextMenuSwitch_OnChecked(object sender, RoutedEventArgs e)
    {
        try
        {
            await UpdateContextMenuToggleSettingAsync();
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Exception in ContextMenuSwitch_OnChecked: {ex.Message}", Importance.Error);
        }
    }

    private async void ContextMenuSwitch_OnUnchecked(object sender, RoutedEventArgs e)
    {
        try
        {
            await UpdateContextMenuToggleSettingAsync();
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Exception in ContextMenuSwitch_OnUnchecked: {ex.Message}", Importance.Error);
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

            await BetterLogger.LogAsync($"Default output path set to: {folderDialog.FolderName}", Importance.Info);
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Exception in DefaultOutputPathChangeButton_OnClick: {ex.Message}",
                Importance.Error);
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

            await BetterLogger.LogAsync("Default output path reset to default location", Importance.Info);
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Exception in DefaultOutputPathResetButton_OnClick: {ex.Message}",
                Importance.Error);
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
}