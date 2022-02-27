using System;
using System.Collections.Generic;
using System.Linq;
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
using System.IO;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.GZip;
using Microsoft.Win32;

namespace EasyExtractUnitypackage
{
    /// <summary>
    /// Interaktionslogik für initWindow.xaml
    /// </summary>
    public partial class initWindow : Window
    {

        private int assetCounter;

        public initWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            currentVersionText.Text = Application.ResourceAssembly.GetName().Version.ToString() + " - 2022";
            currentPage.Navigate(new Uri("UnpackUserControl.xaml", UriKind.RelativeOrAbsolute));
        }

        private void AboutBtn_Click(object sender, RoutedEventArgs e)
        {
            //currentPage.Navigate(new Uri("About.xaml", UriKind.RelativeOrAbsolute));

            About aboutWin = new About();
            aboutWin.Show();
        }

        private void checkUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            currentTabPage.Navigate(new Uri("UpdateCheckUserControl.xaml", UriKind.RelativeOrAbsolute));
        }
    }
}
