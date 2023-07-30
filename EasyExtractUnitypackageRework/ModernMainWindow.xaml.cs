using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using EasyExtractUnitypackageRework.Methods;
using EasyExtractUnitypackageRework.Theme.MessageBox;

namespace EasyExtractUnitypackageRework;

public partial class ModernMainWindow : Window
{
    public ModernMainWindow()
    {
        InitializeComponent();
    }

    public List<string> _TempQueue = new List<string>();


    private void ModernMainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        _TempQueue.Clear();
        Frame.Navigate(new Uri("UserControls/ExtractUserControlModern.xaml", UriKind.Relative));
        Config.Config.GoFrame = "UserControls/ExtractUserControlModern.xaml";
        versionTxt.Text = $"V{Application.ResourceAssembly.GetName().Version}";
        TotalFilesExLabeltrac.Content = Config.Config.TotalFilesExtracted.ToString();
        TotalUnityExLabeltrac.Content = Config.Config.TotalUnitypackgesExtracted.ToString();
        Config.Config.HeartERPEasterEgg = false;
        Config.Config.UpdateConfig();
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

    private void SearchComputerBtn_OnClick(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(new Uri("UserControls/SearchEverything.xaml", UriKind.Relative));
        Config.Config.GoFrame = "UserControls/SearchEverything.xaml";
        Config.Config.UpdateConfig();
    }

    private void UpdateInfo_OnClick(object sender, RoutedEventArgs e)
    {
            if (new EasyMessageBox(
                    "EasyExtractUnitypackage is currently not able to perform updates, neither play videos/audios. keep yourself updated on the official discord server for more information.",
                    MessageType.Info, MessageButtons.Ok).ShowDialog() == true)
            {
                Process.Start("https://discord.com/invite/Wn7XfhPCyD");
            }
    }
}