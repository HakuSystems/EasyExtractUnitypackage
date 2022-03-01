using System;
using System.Collections.Generic;
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

namespace EasyExtractUnitypackage
{
    /// <summary>
    /// Interaction logic for UpdateCheckUserControl.xaml
    /// </summary>
    public partial class UpdateCheckUserControl : UserControl
    {
        public string serverVersion = "https://nanosdk.net/EasyExtractUnitypackage/version.txt"; //website - .txt bc fuck json this is just a small project this is why i use .txt instead of json :)
        public string serverApplicationLoc = "https://nanosdk.net/EasyExtractUnitypackage/EasyExtractUnitypackage.exe"; //website exe
        public static string serverVersNumber;
        public string serverApplicationName; //output name
        public UpdateCheckUserControl()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            RunDownloadProgress();
        }

        private async void RunDownloadProgress()
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
                using (WebClient client = new WebClient())
                {
                    client.DownloadFileAsync(new Uri(serverApplicationLoc), serverApplicationName);
                    client.DownloadProgressChanged += Client_DownloadProgressChanged;
                    client.DownloadFileCompleted += Client_DownloadFileCompleted;
                };
            }
            statusTxt.Text = "up to date";
            openNewCard.Visibility = Visibility.Collapsed;
            progBar.Visibility = Visibility.Collapsed;
        }

        private void Client_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            statusTxt.Text = "Download Complete!";
            openNewCard.Visibility = Visibility.Collapsed;
            MessageBoxResult msgboxres = MessageBox.Show("Download Complete do you want to Open it?", "EasyExtractUnitypackage", MessageBoxButton.YesNo);
            switch (msgboxres)
            {
                case MessageBoxResult.None:
                    statusTxt.Visibility = Visibility.Collapsed;
                    progBar.Visibility = Visibility.Collapsed;
                    cardName.Visibility = Visibility.Collapsed;
                    openNewCard.Visibility = Visibility.Collapsed;
                    break;
                case MessageBoxResult.OK:
                    statusTxt.Visibility = Visibility.Collapsed;
                    progBar.Visibility = Visibility.Collapsed;
                    cardName.Visibility = Visibility.Collapsed;
                    openNewCard.Visibility = Visibility.Collapsed;
                    break;
                case MessageBoxResult.Cancel:
                    statusTxt.Visibility = Visibility.Collapsed;
                    progBar.Visibility = Visibility.Collapsed;
                    cardName.Visibility = Visibility.Collapsed;
                    openNewCard.Visibility = Visibility.Collapsed;
                    break;
                case MessageBoxResult.Yes:
                    Process.Start(serverApplicationName, Directory.GetCurrentDirectory());
                    Window.GetWindow(this).Close();
                    break;
                case MessageBoxResult.No:
                    statusTxt.Text = "Running on old Version!";
                    progBar.Visibility = Visibility.Collapsed;
                    openNewCard.Visibility = Visibility.Visible;
                    break;
                default:
                    break;
            }

        }

        private void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            statusTxt.Text = e.ProgressPercentage.ToString() +"%";
            progBar.Value = e.ProgressPercentage;
        }

        private void openNewCard_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Process.Start(serverApplicationName, Directory.GetCurrentDirectory());
            Window.GetWindow(this).Close();
        }
    }
}
