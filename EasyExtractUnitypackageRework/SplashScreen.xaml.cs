using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;
using System.Windows.Threading;

namespace EasyExtractUnitypackageRework
{
    /// <summary>
    /// Interaction logic for SplashScreen.xaml
    /// </summary>
    public partial class SplashScreen : Window
    {
        //Fuck Json we gonna use .txt Files
        public static string splashText = "https://nanosdk.net/EasyExtractUnitypackage/SplashText.txt";
        public SplashScreen()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //Change SplashText
            var randomizer = new Random();
            using (var client = new WebClient())
            {
                var webData = client.DownloadString(splashText);
                var lines = webData.Split('\n');
                var randomLine = lines[randomizer.Next(0, lines.Length - 1)];
                SplashText.Text = randomLine;
            }


            await Task.Delay(3000); //3 Secs
            MainWindow window = new MainWindow();
            window.Show();
            Close();
        }
    }
}
