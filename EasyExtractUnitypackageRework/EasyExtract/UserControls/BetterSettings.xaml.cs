using System.IO;
using System.Windows;
using System.Windows.Controls;
using EasyExtract.Config;
using EasyExtract.CustomDesign;
using EasyExtract.Discord;
using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace EasyExtract.UserControls;

public partial class BetterSettings : UserControl
{
    private readonly BackgroundManager _backgroundManager = BackgroundManager.Instance;
    private readonly ConfigHelper _configHelper = new();
    private readonly BetterLogger _logger = new();

    public BetterSettings()
    {
        InitializeComponent();
        DataContext = this;
    }

    private async void BetterSettings_OnLoaded(object sender, RoutedEventArgs e)
    {
        await _configHelper.ReadConfigAsync();
        await ChangeUiToMatchConfigAsync();
        await _configHelper.UpdateConfigAsync();
    }

    private async Task ChangeUiToMatchConfigAsync()
    {
        try
        {
            UwUToggleSwitch.IsChecked = _configHelper.Config.UwUModeActive;

            var themes = Enum.GetValues(typeof(ApplicationTheme))
                .Cast<ApplicationTheme>()
                .Where(theme => theme != ApplicationTheme.Unknown)
                .ToList();

            ThemeComboBox.ItemsSource = themes;

            foreach (var theme in themes)
                await _logger.LogAsync($"Available Theme: {theme}", "BetterSettings.xaml.cs", Importance.Info);

            if (themes.Contains(_configHelper.Config.ApplicationTheme))
            {
                ThemeComboBox.SelectedItem = _configHelper.Config.ApplicationTheme;
                await _logger.LogAsync($"Set ThemeComboBox.SelectedItem to: {_configHelper.Config.ApplicationTheme}",
                    "BetterSettings.xaml.cs", Importance.Info);
            }
            else
            {
                await _logger.LogAsync(
                    $"ThemeComboBox.ItemsSource does not contain {_configHelper.Config.ApplicationTheme}",
                    "BetterSettings.xaml.cs", Importance.Warning);
                ThemeComboBox.SelectedItem = ApplicationTheme.Dark; // Default to Dark
                await _logger.LogAsync("Set ThemeComboBox.SelectedItem to default: Dark", "BetterSettings.xaml.cs",
                    Importance.Info);
            }

            CheckForUpdatesOnStartUpToggleSwitch.IsChecked = _configHelper.Config.Update.AutoUpdate;
            DiscordRpcToggleSwitch.IsChecked = _configHelper.Config.DiscordRpc;
            DefaultTempPathTextBox.Text = _configHelper.Config.DefaultTempPath;
            BackgroundOpacitySlider.Value = _configHelper.Config.Backgrounds.BackgroundOpacity;
            await _logger.LogAsync("UI updated to match config", "BetterSettings.xaml.cs", Importance.Info);
        }
        catch (Exception ex)
        {
            await _logger.LogAsync($"Exception in ChangeUiToMatchConfigAsync: {ex.Message}", "BetterSettings.xaml.cs",
                Importance.Error);
        }
    }

    private async void UwUToggleSwitch_OnChecked(object sender, RoutedEventArgs e)
    {
        _configHelper.Config.UwUModeActive = UwUToggleSwitch.IsChecked ?? false;
        await _configHelper.UpdateConfigAsync();
    }

    private async void UwUToggleSwitch_OnUnchecked(object sender, RoutedEventArgs e)
    {
        _configHelper.Config.UwUModeActive = UwUToggleSwitch.IsChecked ?? false;
        await _configHelper.UpdateConfigAsync();
    }

    private async void BackgroundOpacitySlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var currentBackground = _backgroundManager.CurrentBackground;
        _configHelper.Config.Backgrounds.BackgroundOpacity = (float)BackgroundOpacitySlider.Value;
        _configHelper.Config.Backgrounds.BackgroundPath =
            currentBackground.ImageSource
                .ToString(); // Save current background path since it's not saved in the config when changing opacity.
        _backgroundManager.UpdateOpacity(_configHelper.Config.Backgrounds.BackgroundOpacity);
        await _configHelper.UpdateConfigAsync();
    }

    private async void CheckForUpdatesOnStartUpToggleSwitch_OnChecked(object sender, RoutedEventArgs e)
    {
        _configHelper.Config.Update.AutoUpdate = CheckForUpdatesOnStartUpToggleSwitch.IsChecked ?? false;
        await _configHelper.UpdateConfigAsync();
    }

    private async void CheckForUpdatesOnStartUpToggleSwitch_OnUnchecked(object sender, RoutedEventArgs e)
    {
        _configHelper.Config.Update.AutoUpdate = CheckForUpdatesOnStartUpToggleSwitch.IsChecked ?? false;
        await _configHelper.UpdateConfigAsync();
    }

    private async void DiscordRpcToggleSwitch_OnChecked(object sender, RoutedEventArgs e)
    {
        _configHelper.Config.DiscordRpc = DiscordRpcToggleSwitch.IsChecked ?? false;
        await _configHelper.UpdateConfigAsync();
        await DiscordRpcManager.Instance.UpdatePresenceAsync("Settings");
    }

    private async void DiscordRpcToggleSwitch_OnUnchecked(object sender, RoutedEventArgs e)
    {
        _configHelper.Config.DiscordRpc = DiscordRpcToggleSwitch.IsChecked ?? false;
        await _configHelper.UpdateConfigAsync();
        DiscordRpcManager.Instance.Dispose();
    }

    private async void ThemeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _configHelper.Config.ApplicationTheme = (ApplicationTheme)ThemeComboBox.SelectedItem;
        ApplicationThemeManager.Apply(_configHelper.Config.ApplicationTheme);
        await _configHelper.UpdateConfigAsync();
    }

    private async void BackgroundChangeButton_OnClick(object sender, RoutedEventArgs e)
    {
        _configHelper.Config.Backgrounds.BackgroundPath = string.Empty;
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Image Files (*.jpg, *.jpeg, *.png)|*.jpg;*.jpeg;*.png",
            Title = "Select a background image"
        };
        var result = openFileDialog.ShowDialog();
        if (result != true) return;
        _configHelper.Config.Backgrounds.BackgroundPath = openFileDialog.FileName;

        _backgroundManager.UpdateBackground(_configHelper.Config.Backgrounds.BackgroundPath);
        _backgroundManager.UpdateOpacity(_configHelper.Config.Backgrounds.BackgroundOpacity);
        await _configHelper.UpdateConfigAsync();
    }

    private async void BackgroundResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        _configHelper.Config.Backgrounds.BackgroundPath = string.Empty;
        _backgroundManager.ResetBackground();
        await _configHelper.UpdateConfigAsync();
    }

    private async void DefaultTempPathChangeButton_OnClick(object sender, RoutedEventArgs e)
    {
        var folderDialog = new OpenFolderDialog();
        var result = folderDialog.ShowDialog();
        if (result != true) return;
        DefaultTempPathTextBox.Text = folderDialog.FolderName;

        _configHelper.Config.DefaultTempPath = folderDialog.FolderName;
        await _configHelper.UpdateConfigAsync();
        await _logger.LogAsync($"Default temp path set to: {folderDialog.FolderName}", "Settings.xaml.cs",
            Importance.Info);
    }

    private async void DefaultTempPathResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        _configHelper.Config.DefaultTempPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract", "Temp");
        DefaultTempPathTextBox.Text = _configHelper.Config.DefaultTempPath;
        await _configHelper.UpdateConfigAsync();
        await _logger.LogAsync("Default temp path reset", "Settings.xaml.cs", Importance.Info);
    }
}