using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EasyExtract.Config;
using EasyExtract.CustomDesign;
using EasyExtract.Discord;
using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace EasyExtract.UserControls;

public partial class Settings : UserControl
{
    private readonly BetterLogger _logger = new();
    private readonly ConfigHelper ConfigHelper = new();
    private string _configTempPath = "";

    public Settings()
    {
        InitializeComponent();
    }

    private ConfigModel Config { get; set; } = new();

    private async void DefaultTempPathBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var folderDialog = new OpenFolderDialog();
        var result = folderDialog.ShowDialog();
        if (result != true) return;
        DefaultTempPath.Text = folderDialog.FolderName;
        _configTempPath = folderDialog.FolderName;

        var config = await ConfigHelper.ReadConfigAsync();
        if (config != null)
        {
            config.DefaultTempPath = folderDialog.FolderName;
            await ConfigHelper.UpdateConfigAsync(config);
        }

        await _logger.LogAsync($"Default temp path set to: {folderDialog.FolderName}", "Settings.xaml.cs",
            Importance.Info);
    }

    private async void DiscordRPCToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        var config = await ConfigHelper.ReadConfigAsync();
        if (config != null)
        {
            config.DiscordRpc = true;
            await ConfigHelper.UpdateConfigAsync(config);
        }

        DiscordRpcToggle.Content = "On";
        DiscordRpcManager.Instance.DiscordStart();
        await _logger.LogAsync("Discord RPC enabled", "Settings.xaml.cs", Importance.Info);
    }

    private async void DiscordRPCToggle_OnUnchecked(object sender, RoutedEventArgs e)
    {
        var config = await ConfigHelper.ReadConfigAsync();
        if (config != null)
        {
            config.DiscordRpc = false;
            await ConfigHelper.UpdateConfigAsync(config);
        }

        DiscordRpcToggle.Content = "Off";
        DiscordRpcManager.Instance.Dispose();
        await _logger.LogAsync("Discord RPC disabled", "Settings.xaml.cs", Importance.Info);
    }

    private async void UpdateCheckToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        var config = await Task.Run(() => ConfigHelper.ReadConfigAsync());
        if (config != null)
        {
            config.AutoUpdate = true;
            await Task.Run(() => ConfigHelper.UpdateConfigAsync(config));
        }

        UpdateCheckToggle.Content = "On";
        await _logger.LogAsync("Auto-update enabled", "Settings.xaml.cs", Importance.Info);
    }

    private async void UpdateCheckToggle_OnUnchecked(object sender, RoutedEventArgs e)
    {
        var config = await Task.Run(() => ConfigHelper.ReadConfigAsync());
        if (config != null)
        {
            config.AutoUpdate = false;
            await Task.Run(() => ConfigHelper.UpdateConfigAsync(config));
        }

        UpdateCheckToggle.Content = "Off";
        await _logger.LogAsync("Auto-update disabled", "Settings.xaml.cs", Importance.Info);
    }

    private async void UwUModeToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        var config = await Task.Run(() => ConfigHelper.ReadConfigAsync());
        if (config != null)
        {
            config.UwUModeActive = true;
            config.AppTitle = "EasyExtractUwUnitypackage";
            await Task.Run(() => ConfigHelper.UpdateConfigAsync(config));
        }

        UwUModeToggle.Content = "On";
        await _logger.LogAsync("UwU mode enabled", "Settings.xaml.cs", Importance.Info);
    }

    private async void UwUModeToggle_OnUnchecked(object sender, RoutedEventArgs e)
    {
        var config = await Task.Run(() => ConfigHelper.ReadConfigAsync());
        if (config != null)
        {
            config.UwUModeActive = false;
            config.AppTitle = "EasyExtractUnitypackage";
            await Task.Run(() => ConfigHelper.UpdateConfigAsync(config));
        }

        UwUModeToggle.Content = "Off";
        var window = Window.GetWindow(this);
        if (window != null) window.Title = "EasyExtractUnitypackage";
        await _logger.LogAsync("UwU mode disabled", "Settings.xaml.cs", Importance.Info);
    }

    private async void Settings_OnLoaded(object sender, RoutedEventArgs e)
    {
        Config = await Task.Run(() => ConfigHelper.ReadConfigAsync()) ?? new ConfigModel();

        // Update UI elements on the UI thread
        await Dispatcher.InvokeAsync(() =>
        {
            DiscordRpcToggle.Checked += DiscordRPCToggle_OnChecked;
            DiscordRpcToggle.Unchecked += DiscordRPCToggle_OnUnchecked;
            UpdateCheckToggle.Checked += UpdateCheckToggle_OnChecked;
            UpdateCheckToggle.Unchecked += UpdateCheckToggle_OnUnchecked;
            UwUModeToggle.Checked += UwUModeToggle_OnChecked;
            UwUModeToggle.Unchecked += UwUModeToggle_OnUnchecked;

            switch (ThemeComboBox.SelectedIndex)
            {
                case 0:
                    ApplicationThemeManager.Apply(ApplicationTheme.Light);
                    break;
                case 1:
                    ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                    break;
                case 2:
                    ApplicationThemeManager.Apply(ApplicationTheme.HighContrast);
                    break;
                default:
                    ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                    break;
            }

            DiscordRpcToggle.IsChecked = Config.DiscordRpc;
            UpdateCheckToggle.IsChecked = Config.AutoUpdate;
            UwUModeToggle.IsChecked = Config.UwUModeActive;
            DefaultTempPath.Text = Config.DefaultTempPath;

            ThemeComboBox.SelectedIndex = Config.ApplicationTheme switch
            {
                ApplicationTheme.Light => 0,
                ApplicationTheme.Dark => 1,
                ApplicationTheme.HighContrast => 2,
                _ => 1
            };

            _configTempPath = Config.DefaultTempPath;
        });

        if (Config.DiscordRpc)
            try
            {
                await DiscordRpcManager.Instance.UpdatePresenceAsync("Settings");
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                await _logger.LogAsync($"Error updating Discord presence: {exception.Message}", "Settings.xaml.cs",
                    Importance.Error);
            }

        await _logger.LogAsync("Settings UserControl loaded", "Settings.xaml.cs", Importance.Info);
    }

    private async void DefaultTempPath_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        _configTempPath = DefaultTempPath.Text;
        await _logger.LogAsync($"Default temp path text changed: {_configTempPath}", "Settings.xaml.cs",
            Importance.Info);
    }

    private async void DefaultTempPathBtnReset_OnClick(object sender, RoutedEventArgs e)
    {
        DefaultTempPath.Text = Config.DefaultTempPath;
        _configTempPath = Config.DefaultTempPath;
        await _logger.LogAsync("Default temp path reset", "Settings.xaml.cs", Importance.Info);
    }

    private async void UwUCard_OnClick(object sender, RoutedEventArgs e)
    {
        UwUModeToggle.IsChecked = !UwUModeToggle.IsChecked;
        await _logger.LogAsync("UwU mode card clicked", "Settings.xaml.cs", Importance.Info);
    }

    private async void DiscordCard_OnClick(object sender, RoutedEventArgs e)
    {
        DiscordRpcToggle.IsChecked = !DiscordRpcToggle.IsChecked;
        await _logger.LogAsync("Discord card clicked", "Settings.xaml.cs", Importance.Info);
    }

    private async void UpdateCard_OnClick(object sender, RoutedEventArgs e)
    {
        UpdateCheckToggle.IsChecked = !UpdateCheckToggle.IsChecked;
        await _logger.LogAsync("Update card clicked", "Settings.xaml.cs", Importance.Info);
    }

    private async void ThemeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var currentSelection = ThemeComboBox.SelectedIndex;
        var config = await Task.Run(() => ConfigHelper.ReadConfigAsync());
        if (config != null)
        {
            config.ApplicationTheme = currentSelection switch
            {
                0 => ApplicationTheme.Light,
                1 => ApplicationTheme.Dark,
                2 => ApplicationTheme.HighContrast,
                _ => ApplicationTheme.Dark
            };
            await Task.Run(() => ConfigHelper.UpdateConfigAsync(config));
        }

        await Dispatcher.InvokeAsync(() =>
        {
            switch (currentSelection)
            {
                case 0:
                    ApplicationThemeManager.Apply(ApplicationTheme.Light);
                    break;
                case 1:
                    ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                    break;
                case 2:
                    ApplicationThemeManager.Apply(ApplicationTheme.HighContrast);
                    break;
                default:
                    ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                    break;
            }
        });

        await _logger.LogAsync($"Theme changed to {currentSelection}", "Settings.xaml.cs", Importance.Info);
    }

    private async void BackgroundWallpaperChange_OnClick(object sender, RoutedEventArgs e)
    {
        var fileDialog = new OpenFileDialog
        {
            Filter = "Image Files|*.jpg;*.jpeg;*.png;",
            Title = "Select a background image"
        };
        var result = fileDialog.ShowDialog();
        if (result != true) return;

        var config = await Task.Run(() => ConfigHelper.ReadConfigAsync());
        if (config != null)
        {
            var backgroundHandler = new BackgroundHandler(config.Backgrounds);
            backgroundHandler.SetBackground(fileDialog.FileName);
            BackgroundManager.Instance.UpdateBackground(fileDialog.FileName);
            await _logger.LogAsync($"Background wallpaper changed to {fileDialog.FileName}", "Settings.xaml.cs",
                Importance.Info);
        }
    }

    private async void BackgroundWallpaperReset_OnClick(object sender, RoutedEventArgs e)
    {
        var config = await Task.Run(() => ConfigHelper.ReadConfigAsync());
        if (config != null)
        {
            var backgroundHandler = new BackgroundHandler(config.Backgrounds);
            backgroundHandler.SetBackground(null);
            var defaultBackground = backgroundHandler.GetDefaultBackground()?.ToString();
            if (!string.IsNullOrEmpty(defaultBackground))
                BackgroundManager.Instance.ResetBackground(defaultBackground);
            else
                BackgroundManager.Instance.CurrentBackground = new ImageBrush
                {
                    Opacity = BackgroundManager.Instance.BackgroundOpacity
                };
            await _logger.LogAsync("Background wallpaper reset", "Settings.xaml.cs", Importance.Info);
        }
    }

    private async void WallpaperOpacitySlider_OnLoaded(object sender, RoutedEventArgs e)
    {
        var config = await Task.Run(() => ConfigHelper.ReadConfigAsync());
        if (config != null)
        {
            var backgroundHandler = new BackgroundHandler(config.Backgrounds);
            WallpaperOpacitySlider.Value = await backgroundHandler.GetBackgroundOpacity();
            await _logger.LogAsync("Wallpaper opacity slider loaded", "Settings.xaml.cs", Importance.Info);
        }
    }

    private async void WallpaperOpacitySlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        BackgroundManager.Instance.UpdateOpacity(WallpaperOpacitySlider.Value);
        await _logger.LogAsync($"Wallpaper opacity changed to {WallpaperOpacitySlider.Value}", "Settings.xaml.cs",
            Importance.Info);
    }
}