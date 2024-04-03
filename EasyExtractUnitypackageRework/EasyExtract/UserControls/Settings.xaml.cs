using System.Windows;
using System.Windows.Controls;
using EasyExtract.Config;
using EasyExtract.Discord;
using Microsoft.Win32;

namespace EasyExtract.UserControls;

public partial class Settings : UserControl
{
    private string _configLastExtractedPath = "";

    //todo: make change title work properly
    
    //todo: Fix discord rpc change when off
    //also on card click the toggle changes back or on toggle click idk
    //card click == checkbox off
    // config wird geupdate ja, aber ui changed falsch.
    //discord rpc wird nicht gelöscht aka disposed wenn die checkbox aus ist fix das
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
        await ConfigHelper.LoadConfig().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            ConfigModel.DefaultTempPath = folderDialog.FolderName;
            ConfigHelper.UpdateConfig(config);
        });
    }

    private async void LastExtractedPathBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var folderDialog = new OpenFolderDialog();
        var result = folderDialog.ShowDialog();
        if (result != true) return;
        LastExtractedPath.Text = folderDialog.FolderName;
        _configLastExtractedPath = folderDialog.FolderName;
        await ConfigHelper.LoadConfig().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            ConfigModel.LastExtractedPath = folderDialog.FolderName;
            ConfigHelper.UpdateConfig(config);
        });
    }

    private async void DiscordRPCToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        await ConfigHelper.LoadConfig().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            config.DiscordRpc = true;
            ConfigHelper.UpdateConfig(config);
        });
        DiscordRpcToggle.Content = "On";
    }

    private async void DiscordRPCToggle_OnUnchecked(object sender, RoutedEventArgs e)
    {
        await ConfigHelper.LoadConfig().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            config.DiscordRpc = false;
            ConfigHelper.UpdateConfig(config);
        });
        DiscordRpcToggle.Content = "Off";
    }

    private async void WindowsNotificationToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        await ConfigHelper.LoadConfig().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            config.WindowsNotification = true;
            ConfigHelper.UpdateConfig(config);
        });
        WindowsNotificationToggle.Content = "On";
    }

    private async void WindowsNotificationToggle_OnUnchecked(object sender, RoutedEventArgs e)
    {
        await ConfigHelper.LoadConfig().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            config.WindowsNotification = false;
            ConfigHelper.UpdateConfig(config);
        });
        WindowsNotificationToggle.Content = "Off";
    }

    private async void UpdateCheckToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        await ConfigHelper.LoadConfig().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            config.AutoUpdate = true;
            ConfigHelper.UpdateConfig(config);
        });
        UpdateCheckToggle.Content = "On";
    }

    private async void UpdateCheckToggle_OnUnchecked(object sender, RoutedEventArgs e)
    {
        await ConfigHelper.LoadConfig().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            config.AutoUpdate = false;
            ConfigHelper.UpdateConfig(config);
        });
        UpdateCheckToggle.Content = "Off";
    }

    private async void UwUModeToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        await ConfigHelper.LoadConfig().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            config.UwUModeActive = true;
            config.AppTitle = "EasyExtractUwUnitypackage";
            ConfigHelper.UpdateConfig(config);
        });
        UwUModeToggle.Content = "On";
    }

    private async void UwUModeToggle_OnUnchecked(object sender, RoutedEventArgs e)
    {
        await ConfigHelper.LoadConfig().ContinueWith(task =>
        {
            var config = task.Result;
            if (config == null) return;
            config.UwUModeActive = false;
            config.AppTitle = "EasyExtractUnitypackage";
            ConfigHelper.UpdateConfig(config);
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

        DefaultTempPath.Text = _configTempPath;
        LastExtractedPath.Text = _configLastExtractedPath;

        await ConfigHelper.LoadConfig().ContinueWith(task =>
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
                LastExtractedPath.Text = ConfigModel.LastExtractedPath;


                _configTempPath = ConfigModel.DefaultTempPath;
                _configLastExtractedPath = ConfigModel.LastExtractedPath;
            });
            ConfigHelper.UpdateConfig(config);
        });

        var isDiscordEnabled = false;
        try
        {
            isDiscordEnabled = (await ConfigHelper.LoadConfig()).DiscordRpc;
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            throw;
        }

        if (isDiscordEnabled)
            try
            {
                await DiscordRpcManager.Instance.UpdatePresenceAsync("Viewing Settings Page");
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }
    }

    private void LastExtractedPath_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        _configLastExtractedPath = LastExtractedPath.Text;
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

    private void LastExtractedPathReset_OnClick(object sender, RoutedEventArgs e)
    {
        LastExtractedPath.Text = ConfigModel.LastExtractedPath;
        _configLastExtractedPath = ConfigModel.LastExtractedPath;
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
}