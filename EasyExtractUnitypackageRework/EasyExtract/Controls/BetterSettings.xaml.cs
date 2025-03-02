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
        Dashboard.Instance.NavigateBackBtn.Visibility = Visibility.Visible;
        await ChangeUiToMatchConfigAsync();
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
        await UwUToggleSwitchCheckUnCheck();
    }

    private async Task UwUToggleSwitchCheckUnCheck()
    {
        ConfigHandler.Instance.Config.UwUModeActive = UwUToggleSwitch.IsChecked ?? false;
    }

    private async void UwUToggleSwitch_OnUnchecked(object sender, RoutedEventArgs e)
    {
        await UwUToggleSwitchCheckUnCheck();
    }

    private async void BackgroundOpacitySlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var currentBackground = _backgroundManager.CurrentBackground;
        ConfigHandler.Instance.Config.CustomBackgroundImage.BackgroundOpacity = (float)BackgroundOpacitySlider.Value;
        ConfigHandler.Instance.Config.CustomBackgroundImage.BackgroundPath =
            currentBackground.ImageSource
                .ToString(); // Save the current background path
        // since it's not saved in the config when changing opacity.
        _backgroundManager.UpdateOpacity(ConfigHandler.Instance.Config.CustomBackgroundImage.BackgroundOpacity);
    }

    private async void CheckForUpdatesOnStartUpToggleSwitch_OnChecked(object sender, RoutedEventArgs e)
    {
        await CheckForUpdatesUpdateToggle();
    }

    private async Task CheckForUpdatesUpdateToggle()
    {
        ConfigHandler.Instance.Config.Update.AutoUpdate = CheckForUpdatesOnStartUpToggleSwitch.IsChecked ?? false;
    }

    private async void CheckForUpdatesOnStartUpToggleSwitch_OnUnchecked(object sender, RoutedEventArgs e)
    {
        await CheckForUpdatesUpdateToggle();
    }

    private async void DiscordRpcToggleSwitch_OnChecked(object sender, RoutedEventArgs e)
    {
        ConfigHandler.Instance.Config.DiscordRpc = DiscordRpcToggleSwitch.IsChecked ?? false;
        await DiscordRpcManager.Instance.TryUpdatePresenceAsync("Settings");
    }

    private async void DiscordRpcToggleSwitch_OnUnchecked(object sender, RoutedEventArgs e)
    {
        ConfigHandler.Instance.Config.DiscordRpc = DiscordRpcToggleSwitch.IsChecked ?? false;
        DiscordRpcManager.Instance.Dispose();
    }

    private async void ThemeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox.SelectedItem == null) return;
        var selectedTheme = (AvailableThemes)ThemeComboBox.SelectedItem;
        ConfigHandler.Instance.Config.ApplicationTheme = selectedTheme;
        await BetterLogger.LogAsync($"Theme changed to: {selectedTheme}", Importance.Info);
    }


    private async void BackgroundChangeButton_OnClick(object sender, RoutedEventArgs e)
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

        _backgroundManager.UpdateBackground(ConfigHandler.Instance.Config.CustomBackgroundImage.BackgroundPath);
        _backgroundManager.UpdateOpacity(ConfigHandler.Instance.Config.CustomBackgroundImage.BackgroundOpacity);
    }

    private async void BackgroundResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        ConfigHandler.Instance.Config.CustomBackgroundImage.BackgroundPath = string.Empty;
        _backgroundManager.ResetBackground();
    }

    private async void DefaultTempPathChangeButton_OnClick(object sender, RoutedEventArgs e)
    {
        var folderDialog = new OpenFolderDialog();
        var result = folderDialog.ShowDialog();
        if (result != true) return;
        DefaultTempPathTextBox.Text = folderDialog.FolderName;

        ConfigHandler.Instance.Config.DefaultTempPath = folderDialog.FolderName;
        await BetterLogger.LogAsync($"Default temp path set to: {folderDialog.FolderName}",
            Importance.Info);
    }

    private async void DefaultTempPathResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        ConfigHandler.Instance.Config.DefaultTempPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract", "Temp");
        DefaultTempPathTextBox.Text = ConfigHandler.Instance.Config.DefaultTempPath;
        await BetterLogger.LogAsync("Default temp path reset", Importance.Info);
    }

    private async void ContextMenuSwitch_OnChecked(object sender, RoutedEventArgs e)
    {
        await UpdateContextMenuToggleSettingAsync();
    }

    private async void ContextMenuSwitch_OnUnchecked(object sender, RoutedEventArgs e)
    {
        await UpdateContextMenuToggleSettingAsync();
    }

    private async Task UpdateContextMenuToggleSettingAsync()
    {
        ConfigHandler.Instance.Config.ContextMenuToggle = ContextMenuSwitch.IsChecked ?? false;
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


    private async void DynamicScalingComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DynamicScalingComboBox.SelectedItem == null) return;
        var selectedMode = (DynamicScalingModes)DynamicScalingComboBox.SelectedItem;
        ConfigHandler.Instance.Config.DynamicScalingMode = selectedMode;
    }
}