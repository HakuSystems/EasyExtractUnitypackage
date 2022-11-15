using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using EasyExtractUnitypackageRework.Methods;

namespace EasyExtractUnitypackageRework;

public partial class ModernMainWindow : Window
{
    public ModernMainWindow()
    {
        InitializeComponent();
    }


    private void ModernMainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        //bool ? true : false
        UpdateInfo.Text =
            Updater.IsUpdateAvailable
                ? "Update available!"
                : "Welcome Back"; //Todo: INFORMATION currently the program will always be updated
        //so this will always be false
        Frame.Navigate(new Uri("UserControls/ExtractUserControlModern.xaml", UriKind.Relative));
        Config.Config.GoFrame = "UserControls/ExtractUserControlModern.xaml";
        Config.Config.UpdateConfig();
        versionTxt.Text = $"V{Application.ResourceAssembly.GetName().Version}";
        TotalFilesExLabeltrac.Content = Config.Config.TotalFilesExtracted.ToString();
        TotalUnityExLabeltrac.Content = Config.Config.TotalUnitypackgesExtracted.ToString();
        Config.Config.HeartERPEasterEgg = false;
    }

    private void SettingsBtn_OnClick(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(new Uri("UserControls/SettingsUserControlModern.xaml", UriKind.Relative));
        Config.Config.GoFrame = "UserControls/SettingsUserControlModern.xaml";
        Config.Config.UpdateConfig();
    }

    private void ClosBtn_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MinBtn_OnClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void ModernMainWindow_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void ExtractBtn_OnClick(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(new Uri("UserControls/ExtractUserControlModern.xaml", UriKind.Relative));
        Config.Config.GoFrame = "UserControls/ExtractUserControlModern.xaml";
        Config.Config.UpdateConfig();
    }

    private void PatreonBtn_OnClick(object sender, RoutedEventArgs e)
    {
        Process.Start("https://www.patreon.com/naxokit");
    }

    private void DiscordBtn_OnClick(object sender, RoutedEventArgs e)
    {
        Process.Start("https://discord.com/invite/Wn7XfhPCyD");
    }

    private void EasterEggBtn_OnClick(object sender, RoutedEventArgs e)
    {
        Process.Start("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
    }

    private void HeartBtnEasterEgg_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        heartBtnEasterEgg.Foreground = new SolidColorBrush(Color.FromRgb(255, 0, 0));
        Config.Config.HeartERPEasterEgg = true;
    }
}