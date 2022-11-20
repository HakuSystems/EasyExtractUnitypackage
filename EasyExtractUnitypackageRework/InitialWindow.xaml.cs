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
    private const string SecondVideoUrl = "https://nanosdk.net/EasyExtractUnitypackage/LogoAnimation2.mp4";
    private const string EverythingUrl = "https://nanosdk.net/EasyExtractUnitypackage/Everything64.dll";
    

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

        if (File.Exists($"{currentDirectory}\\LogoAnimation2.mp4") && File.Exists($"{currentDirectory}\\Everything64.dll"))
        {
            OpenModernWindow();
        }
        else
        {
            if (new EasyMessageBox("This software Downloads a Video on First start, and also a dll (so its recommended to put it in a separate folder)" +
                                   " do you want to Continue?",
                    MessageType.Warning, MessageButtons.YesNo).ShowDialog() == false)
            {
                Environment.Exit(0);
            }
        }

        DownloadFile(SecondVideoUrl, $"{currentDirectory}\\LogoAnimation2.mp4");
        DownloadFile(EverythingUrl, $"{currentDirectory}\\Everything64.dll");
    }

    private static void DownloadFile(string url, string path)
    {
        var client = new WebClient();
        using (client)
        {
            client.DownloadFileCompleted += (sender, args) =>
            {
                if (args.Error != null)
                {
                    new EasyMessageBox("Error while downloading a file, please try again later", MessageType.Error, MessageButtons.Ok).ShowDialog();
                    Environment.Exit(0);
                }
                else
                {
                    OpenModernWindow();
                }
            };
            client.DownloadFileAsync(new Uri(url), path);
        }
    }
}