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
using System.Diagnostics;

namespace EasyExtractUnitypackage
{
    /// <summary>
    /// Interaktionslogik für initWindow.xaml
    /// </summary>
    public partial class About : Window
    {
        private string tfiles = "Total Files Extracted: " + (Properties.Settings.Default.files).ToString();
        private string ufiles = ".unitypackage Files Extracted: " + (Properties.Settings.Default.packages).ToString();

        public string TFiles
        {
            get { return tfiles; } 
        }
        public string UFiles
        {
            get { return ufiles; }
        }

        public About()
        {
            this.DataContext = this;
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            currentVersionText.Text = Application.ResourceAssembly.GetName().Version.ToString() + " - 2022";
        }

        private void Closebtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void websiteBtn_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://nanosdk.net/");
        }
    }
}
