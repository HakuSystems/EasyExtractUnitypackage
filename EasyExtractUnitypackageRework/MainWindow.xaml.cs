using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace EasyExtractUnitypackageRework
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /* Custom MessageBox
            if (new Theme.MessageBox.EasyMessageBox("Message",
                   Theme.MessageBox.MessageType.Info,
                   Theme.MessageBox.MessageButtons.YesNo).ShowDialog().Value)
               {
               }
        */


        public string serverVersion = "https://nanosdk.net/EasyExtractUnitypackage/version.txt"; //website - .txt bc fuck json this is just a small project this is why i use .txt instead of json :)
        public string serverApplicationLoc = "https://nanosdk.net/EasyExtractUnitypackage/EasyExtractUnitypackage.exe"; //website exe
        public static string serverVersNumber;
        public string serverApplicationName; //output name
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            currentVersion.Text = Application.ResourceAssembly.GetName().Version.ToString() + " - 2022";
            currentPage.Navigate(new Uri("UserControls/Unpack.xaml", UriKind.RelativeOrAbsolute));
            UpdateCheck();
        }

        private async void UpdateCheck()
        {
            HttpClient httpClient = new HttpClient();
            var result = await httpClient.GetAsync(serverVersion);
            var strServerVersion = await result.Content.ReadAsStringAsync();
            var serverVersionParsed = Version.Parse(strServerVersion);
            var currentVersion = Application.ResourceAssembly.ManifestModule.Assembly.GetName().Version;

            serverVersNumber = serverVersionParsed.ToString();
            serverApplicationName = $"EasyExtractUnitypackage V{serverVersNumber}.exe";

            if (serverVersionParsed > currentVersion)
            {
                if (new Theme.MessageBox.EasyMessageBox("EasyExtractUnitypackage V" + serverVersNumber+" Ready to Download"+Environment.NewLine+"Do you want to download it?",
                    Theme.MessageBox.MessageType.Info,
                    Theme.MessageBox.MessageButtons.YesNo).ShowDialog().Value)
                {
                    using (WebClient client = new WebClient())
                    {
                        client.DownloadFileAsync(new Uri(serverApplicationLoc), serverApplicationName);
                        client.DownloadProgressChanged += Client_DownloadProgressChanged;
                        client.DownloadFileCompleted += Client_DownloadFileCompleted;
                    };
                }
                else
                {
                    UpdateStatusTxt.Text = "Running on old Version!";
                    CheckUpdateIcon.Kind = (MahApps.Metro.IconPacks.PackIconMaterialKind)MahApps.Metro.IconPacks.PackIconMaterialDesignKind.OpenInNew;
                    CheckUpdateTxt.Text = "Open new Version";
                    ProgBar.Visibility = Visibility.Collapsed;
                }
            }
            ProgBar.Visibility = Visibility.Collapsed;
        }

        private void Client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            UpdateStatusTxt.Text = "Download Complete!";
            if (new Theme.MessageBox.EasyMessageBox("Download Complete do you want to Open it?",
                   Theme.MessageBox.MessageType.Info,
                   Theme.MessageBox.MessageButtons.OkCancel).ShowDialog().Value)
            {
                Process.Start(serverApplicationName, Directory.GetCurrentDirectory());
                Window.GetWindow(this).Close();
            }
            else
            {
                UpdateStatusTxt.Text = "Running on old Version!";
                CheckUpdateIcon.Kind = (MahApps.Metro.IconPacks.PackIconMaterialKind)MahApps.Metro.IconPacks.PackIconMaterialDesignKind.OpenInNew;
                CheckUpdateTxt.Text = "Open new Version";
                ProgBar.Visibility = Visibility.Collapsed;
            }
        }

        private void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            ProgBar.Visibility = Visibility.Visible;
            UpdateStatusTxt.Text = e.ProgressPercentage.ToString() + "%";
            ProgBar.Value = e.ProgressPercentage;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MinBtn_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            if (UpdateStatusTxt.Text.Contains("old Version"))
            {
                Process.Start(serverApplicationName, Directory.GetCurrentDirectory());
                Window.GetWindow(this).Close();
            }
            UpdateCheck();
        }

        private void AbtBtn_Click(object sender, RoutedEventArgs e)
        {
            currentPage2.Navigate(new Uri("UserControls/About.xaml", UriKind.RelativeOrAbsolute));
        }

        private void CoolEmote_MouseDown(object sender, MouseButtonEventArgs e)
        {
            //EasterEgg Suggested by ᴍʏsᴛɪᴄ#8876 - Known as erp Button (EroticRoleplay Button)
            if (new Theme.MessageBox.EasyMessageBox("do you want to have ERP? (EroticRoleplay)",
                   Theme.MessageBox.MessageType.EasterEgg,
                   Theme.MessageBox.MessageButtons.YesNo).ShowDialog().Value)
            {
                if (new Theme.MessageBox.EasyMessageBox("ERP Patrol is coming after you.",
                   Theme.MessageBox.MessageType.EasterEgg,
                   Theme.MessageBox.MessageButtons.Ok).ShowDialog().Value)
                {
                    WNotificationEasterEgg("Send to Jail", "Achievement unlocked!");
                    Environment.Exit(0);
                }
            }
            else
            {
                if (new Theme.MessageBox.EasyMessageBox("ERP Patrol is proud of you for picking no",
                   Theme.MessageBox.MessageType.EasterEgg,
                   Theme.MessageBox.MessageButtons.Ok).ShowDialog().Value)
                {
                    //opening the same window again bc otherwise program would crash for some reason
                    WNotificationEasterEgg("no ERP Zone", "Achievement unlocked!");
                    MainWindow main = new MainWindow();
                    main.Show();
                    Close();

                }
            }

        }

        private void WNotificationEasterEgg(string message, string title)
        {
            new ToastContentBuilder()
                .AddArgument("action", "viewConversation")
                .AddArgument("conversationId", 9813)
                .AddText(title)
                .AddText(message)
                .Show();
        }
    }
}
