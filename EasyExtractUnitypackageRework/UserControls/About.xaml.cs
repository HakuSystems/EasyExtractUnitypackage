using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace EasyExtractUnitypackageRework.UserControls
{
    /// <summary>
    /// Interaction logic for About.xaml
    /// </summary>
    public partial class About : UserControl
    {
        public About()
        {
            InitializeComponent();
        }

        private void WebsiteBtn_Click(object sender, RoutedEventArgs e)
        {

            Process.Start("https://nanosdk.net/");
        }

        private void CloseBtn_Click_1(object sender, RoutedEventArgs e)
        {
            ((MainWindow)Window.GetWindow(this)).currentPage2.Source = null;
        }
    }
}
