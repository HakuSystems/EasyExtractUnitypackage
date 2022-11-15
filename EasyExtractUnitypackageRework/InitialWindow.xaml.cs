using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Windows;
using EasyExtractUnitypackageRework.Methods;

namespace EasyExtractUnitypackageRework;

public partial class InitialWindow : Window
{
    private const string SecondVideoUrl = "https://nanosdk.net/EasyExtractUnitypackage/LogoAnimation2.mp4"; //Small one

    public InitialWindow()
    {
        InitializeComponent();
    }

    private void InitialWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        Config.Config.InitializeConfig();
        Updater.CheckUpdateAsync();
        DownloadNeededFiles();
    }

    private static void OpenModernWindow()
    {
        var mainWindow = new ModernMainWindow();
        mainWindow.Show();
        Application.Current.MainWindow?.Close();
    }

    private static void DownloadNeededFiles()
    {
        var currentDirectory = Directory.GetCurrentDirectory();

        if (File.Exists($"{currentDirectory}\\LogoAnimation2.mp4"))
        {
            OpenModernWindow();
            return;
        }

        DownloadFile(SecondVideoUrl, $"{currentDirectory}\\LogoAnimation2.mp4");
    }

    private static void DownloadFile(string url, string path)
    {
        var client = new WebClient();
        client.DownloadFileAsync(new Uri(url), path);
        client.DownloadFileCompleted += Client_DownloadFileCompleted;
        client.Dispose();
    }

    private static void Client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
    {
        OpenModernWindow();
    }
}