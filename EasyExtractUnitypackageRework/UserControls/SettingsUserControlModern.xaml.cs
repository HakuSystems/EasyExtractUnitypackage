using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using EasyExtractUnitypackageRework.Methods;

namespace EasyExtractUnitypackageRework.UserControls;

public partial class SettingsUserControlModern : UserControl
{
    public SettingsUserControlModern()
    {
        InitializeComponent();
    }

    private void SettingsUserControlModern_OnLoaded(object sender, RoutedEventArgs e)
    {
        UwUfyer.IsChecked = Config.Config.UwUifyer;
        DefaultTempPathCheckbox.IsChecked = Config.Config.UseDefaultTempPath;
        DefaultTempPathSettingText.Text = DefaultTempPathCheckbox.IsChecked == true
            ? $"Default Temp Path set to: {Path.GetTempPath()}"
            : $"Default Temp Path set to: {Directory.GetCurrentDirectory()}";
        LastExtractedPath.Text = $"Last Extracted Path: {Config.Config.lastTargetPath}";
        RandomTxt.Text = "Random Text is not available at the moment";
        if (Config.Config.HeartERPEasterEgg)
            ERPEasterEgg.Visibility = Visibility.Visible;
        
        WindowsDescription.Text = $"Current State: {(Config.Config.WindowsNotification ? "Enabled" : "Disabled")}";
    }

    private void DefaultTempPathCheckbox_OnChecked(object sender, RoutedEventArgs e)
    {
        DefaultTempPathSettingText.Text = $"Default Temp Path set to: {Path.GetTempPath()}";
        Config.Config.UseDefaultTempPath = true;
        Config.Config.UpdateConfig();
    }

    private void DefaultTempPathCheckbox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        DefaultTempPathSettingText.Text = $"Default Temp Path set to: {Directory.GetCurrentDirectory()}";
        Config.Config.UseDefaultTempPath = false;
        Config.Config.UpdateConfig();
    }

    private void OpenExtractedPathBtn_OnClick(object sender, RoutedEventArgs e)
    {
        Process.Start(Config.Config.lastTargetPath);
    }

    private void UwUfyer_OnUnchecked(object sender, RoutedEventArgs e)
    {
        ((ModernMainWindow)Window.GetWindow(this))!.WindowTitleEasy.Text = "EasyExtractUnitypacakge";
        Config.Config.UwUifyer = false;
        Config.Config.UpdateConfig();
    }

    private void UwUfyer_OnChecked(object sender, RoutedEventArgs e)
    {
        ((ModernMainWindow)Window.GetWindow(this))!.WindowTitleEasy.Text = "EasyExtractUwUnitypacakge";
        Config.Config.UwUifyer = true;
        Config.Config.UpdateConfig();
    }

    private void WindowsCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        WindowsDescription.Text = "Current State: Enabled";
        Config.Config.WindowsNotification = true;
        Config.Config.UpdateConfig();
    }

    private void WindowsCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        WindowsDescription.Text = "Current State: Disabled";
        Config.Config.WindowsNotification = false;
        Config.Config.UpdateConfig();
    }
}