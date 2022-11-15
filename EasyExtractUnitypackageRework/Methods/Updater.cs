using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using EasyExtractUnitypackageRework.Theme.MessageBox;

namespace EasyExtractUnitypackageRework.Methods;

public class Updater
{
    private const string
        ServerVersion =
            "https://nanosdk.net/EasyExtractUnitypackage/version.txt"; //website - .txt bc fuck json this is just a small project this is why i use .txt instead of json :)

    private const string ServerApplicationLoc =
        "https://nanosdk.net/EasyExtractUnitypackage/EasyExtractUnitypackage.exe"; //website exe

    private static string _serverVerNumber;
    private static string _serverApplicationName; //output name
    public static bool IsUpdateAvailable;


    public static async void CheckUpdateAsync()
    {
        var httpClient = new HttpClient();
        var result = await httpClient.GetAsync(ServerVersion);
        var strServerVersion = await result.Content.ReadAsStringAsync();
        var serverVersionParsed = Version.Parse(strServerVersion);
        var currentVersion = Application.ResourceAssembly.GetName().Version;

        _serverVerNumber = serverVersionParsed.ToString();
        _serverApplicationName = $"EasyExtractUnitypackage V{_serverVerNumber}.exe";
        IsUpdateAvailable = false;
        if (serverVersionParsed <= currentVersion) return;
        IsUpdateAvailable = true;
        using var client = new WebClient();
        client.DownloadFileAsync(new Uri(ServerApplicationLoc), _serverApplicationName);
        client.DownloadFileCompleted += Client_DownloadFileCompleted;
        httpClient.Dispose();
        client.Dispose();
    }

    private static void Client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
    {
        if (e.Error != null)
        {
            new EasyMessageBox("Error while downloading new Version!", MessageType.Error, MessageButtons.Ok)
                .ShowDialog();
            return;
        }

        Config.Config.Version = _serverVerNumber;
        Config.Config.UpdateConfig();
        Process.Start(_serverApplicationName, Directory.GetCurrentDirectory());
        Environment.Exit(0);
    }
}