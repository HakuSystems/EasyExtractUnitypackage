using System.Windows;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;

namespace EasyExtract;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        Logo.Source =
            new BitmapImage(new Uri("pack://application:,,,/EasyExtract;component/Resources/Gifs/LogoAnimation.gif"));
    }
}