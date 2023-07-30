using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Windows;
using EasyExtractUnitypackageRework.Methods;
using EasyExtractUnitypackageRework.Theme.MessageBox;

namespace EasyExtractUnitypackageRework;

public partial class InitialWindow : Window
{
    public InitialWindow()
    {
        InitializeComponent();
    }

    private void InitialWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        Config.Config.InitializeConfig();
        OpenModernWindow();
    }

    private static void OpenModernWindow()
    {
        var mainWindow = new ModernMainWindow();
        mainWindow.Show();
        Application.Current.MainWindow?.Close();
    }
}