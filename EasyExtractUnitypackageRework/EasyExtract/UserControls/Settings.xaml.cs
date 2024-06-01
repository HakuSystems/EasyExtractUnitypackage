using System.Windows;
using System.Windows.Controls;
using EasyExtract.Config;
using EasyExtract.Discord;
using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace EasyExtract.UserControls;

public partial class Settings : UserControl
{
    private string _configTempPath = "";

    public Settings()
    {
        InitializeComponent();
    }

    private async void DefaultTempPathBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var folderDialog = new OpenFolderDialog();
        var result = folderDialog.ShowDialog();
        if (result != true) return;
        DefaultTempPath.Text = folderDialog.FolderName;
        _configTempPath = folderDialog.FolderName;
        await ConfigHelper.LoadConfigAsync().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            ConfigModel.DefaultTempPath = folderDialog.FolderName;
            ConfigHelper.UpdateConfigAsync(config);
        });
    }

    private async void DiscordRPCToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        await ConfigHelper.LoadConfigAsync().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            config.DiscordRpc = true;
            ConfigHelper.UpdateConfigAsync(config);
        });
        DiscordRpcToggle.Content = "On";
        DiscordRpcManager.Instance.DiscordStart();
    }

    private async void DiscordRPCToggle_OnUnchecked(object sender, RoutedEventArgs e)
    {
        await ConfigHelper.LoadConfigAsync().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            config.DiscordRpc = false;
            ConfigHelper.UpdateConfigAsync(config);
        });
        DiscordRpcToggle.Content = "Off";
        DiscordRpcManager.Instance.Dispose();
    }

    private async void WindowsNotificationToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        await ConfigHelper.LoadConfigAsync().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            config.WindowsNotification = true;
            ConfigHelper.UpdateConfigAsync(config);
        });
        WindowsNotificationToggle.Content = "On";
    }

    private async void WindowsNotificationToggle_OnUnchecked(object sender, RoutedEventArgs e)
    {
        await ConfigHelper.LoadConfigAsync().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            config.WindowsNotification = false;
            ConfigHelper.UpdateConfigAsync(config);
        });
        WindowsNotificationToggle.Content = "Off";
    }

    private async void UpdateCheckToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        await ConfigHelper.LoadConfigAsync().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            config.AutoUpdate = true;
            ConfigHelper.UpdateConfigAsync(config);
        });
        UpdateCheckToggle.Content = "On";
    }

    private async void UpdateCheckToggle_OnUnchecked(object sender, RoutedEventArgs e)
    {
        await ConfigHelper.LoadConfigAsync().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            config.AutoUpdate = false;
            ConfigHelper.UpdateConfigAsync(config);
        });
        UpdateCheckToggle.Content = "Off";
    }

    private async void UwUModeToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        await ConfigHelper.LoadConfigAsync().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            config.UwUModeActive = true;
            config.AppTitle = "EasyExtractUwUnitypackage";
            ConfigHelper.UpdateConfigAsync(config);
        });
        UwUModeToggle.Content = "On";
    }

    private async void UwUModeToggle_OnUnchecked(object sender, RoutedEventArgs e)
    {
        await ConfigHelper.LoadConfigAsync().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            config.UwUModeActive = false;
            config.AppTitle = "EasyExtractUnitypackage";
            ConfigHelper.UpdateConfigAsync(config);
        });
        UwUModeToggle.Content = "Off";
        var window = Window.GetWindow(this);
        window.Title = "EasyExtractUnitypackage";
    }

    private async void Settings_OnLoaded(object sender, RoutedEventArgs e)
    {
        DiscordRpcToggle.Checked += DiscordRPCToggle_OnChecked;
        DiscordRpcToggle.Unchecked += DiscordRPCToggle_OnUnchecked;
        WindowsNotificationToggle.Checked += WindowsNotificationToggle_OnChecked;
        WindowsNotificationToggle.Unchecked += WindowsNotificationToggle_OnUnchecked;
        UpdateCheckToggle.Checked += UpdateCheckToggle_OnChecked;
        UpdateCheckToggle.Unchecked += UpdateCheckToggle_OnUnchecked;
        UwUModeToggle.Checked += UwUModeToggle_OnChecked;
        UwUModeToggle.Unchecked += UwUModeToggle_OnUnchecked;

        //0 = Light, 1 = Dark, 2 = High Contrast
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

        DefaultTempPath.Text = _configTempPath;

        await ConfigHelper.LoadConfigAsync().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            Dispatcher.Invoke(() =>
            {
                DiscordRpcToggle.IsChecked = config.DiscordRpc;
                WindowsNotificationToggle.IsChecked = config.WindowsNotification;
                UpdateCheckToggle.IsChecked = config.AutoUpdate;
                UwUModeToggle.IsChecked = config.UwUModeActive;
                DefaultTempPath.Text = ConfigModel.DefaultTempPath;

                ThemeComboBox.SelectedIndex = config.ApplicationTheme switch
                {
                    ApplicationTheme.Light => 0,
                    ApplicationTheme.Dark => 1,
                    ApplicationTheme.HighContrast => 2,
                    _ => 1
                };

                _configTempPath = ConfigModel.DefaultTempPath;
            });
            ConfigHelper.UpdateConfigAsync(config);
        });
        var isDiscordEnabled = false;
        try
        {
            isDiscordEnabled = (await ConfigHelper.LoadConfigAsync()).DiscordRpc;
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            throw;
        }

        if (isDiscordEnabled)
            try
            {
                await DiscordRpcManager.Instance.UpdatePresenceAsync("Settings");
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }
    }

    private void DefaultTempPath_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        _configTempPath = DefaultTempPath.Text;
    }

    private void DefaultTempPathBtnReset_OnClick(object sender, RoutedEventArgs e)
    {
        DefaultTempPath.Text = ConfigModel.DefaultTempPath;
        _configTempPath = ConfigModel.DefaultTempPath;
    }

    private void UwUCard_OnClick(object sender, RoutedEventArgs e)
    {
        UwUModeToggle.IsChecked = !UwUModeToggle.IsChecked;
    }

    private void DiscordCard_OnClick(object sender, RoutedEventArgs e)
    {
        DiscordRpcToggle.IsChecked = !DiscordRpcToggle.IsChecked;
    }

    private void WindowsNotiCard_OnClick(object sender, RoutedEventArgs e)
    {
        WindowsNotificationToggle.IsChecked = !WindowsNotificationToggle.IsChecked;
    }

    private void UpdateCard_OnClick(object sender, RoutedEventArgs e)
    {
        UpdateCheckToggle.IsChecked = !UpdateCheckToggle.IsChecked;
    }


    private async void ThemeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var currentSelection = ThemeComboBox.SelectedIndex;
        //1 = dark, 2 = light, 3 = high contrast
        await ConfigHelper.LoadConfigAsync().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            config.ApplicationTheme = currentSelection switch
            {
                0 => ApplicationTheme.Light,
                1 => ApplicationTheme.Dark,
                2 => ApplicationTheme.HighContrast,
                _ => ApplicationTheme.Dark
            };
            ConfigHelper.UpdateConfigAsync(config);
        });
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
    }
}